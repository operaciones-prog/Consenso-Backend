using esupplier.Models.IA;
using esupplier.Repositorys.IRepositorys;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IA
{
    /// <summary>Llaves de Kushki con las que se llama a la API (incluye las secretas: no salen del backend).</summary>
    public record CredencialesKushki(bool Prueba, string LlavePublica, string LlavePrivada, string SecretoWebhook)
    {
        public bool Completa => !string.IsNullOrWhiteSpace(LlavePublica) && !string.IsNullOrWhiteSpace(LlavePrivada);
        public string UrlBase => Prueba ? KushkiCliente.UrlPruebas : KushkiCliente.UrlProduccion;
    }

    /// <summary>Lo que puede ver el navegador: modo, llave pública y si el pago con tarjeta está habilitado.</summary>
    public record KushkiPublico(bool Prueba, string LlavePublica, bool Configurado);

    public interface IConfiguracionKushki
    {
        /// <summary>Sin leer secretos (rápido): para mostrar el formulario de tarjeta.</summary>
        KushkiPublico Publico();

        /// <summary>Con la llave privada y el id de firma de webhooks (AWS Secrets Manager, con caché).</summary>
        Task<CredencialesKushki> CredencialesAsync();
    }

    /// <summary>
    /// De dónde salen las llaves de Kushki:
    ///  1. Las que configura el administrador de plataforma (Plataforma > Pago con tarjeta): tb_ia_config_kushki
    ///     + el secreto JSON {"llave_privada","secreto_webhook"} en AWS Secrets Manager.
    ///  2. Si la plataforma no configuró nada, Kushki:* de la configuración (user secrets o variables de entorno).
    /// El secreto se guarda en caché por proceso; al guardar cambia reg_fecha_modificacion y con ella la clave de
    /// la caché, así que todas las instancias (Lambda) usan las llaves nuevas en la siguiente petición.
    /// </summary>
    public class ConfiguracionKushki : IConfiguracionKushki
    {
        private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(10);
        private static readonly object Candado = new();
        private static (string clave, CredencialesKushki valor, DateTime expira) _cache;

        private readonly IPlanIARepository _repositorio;
        private readonly IAlmacenSecretos _secretos;
        private readonly IConfiguration _configuration;

        public ConfiguracionKushki(IPlanIARepository repositorio, IAlmacenSecretos secretos, IConfiguration configuration)
        {
            _repositorio = repositorio;
            _secretos = secretos;
            _configuration = configuration;
        }

        public KushkiPublico Publico()
        {
            ConfigKushkiIA c = _repositorio.obtenerConfigKushki().config;
            if (c != null) return new KushkiPublico(c.prueba, c.llave_publica, Configurado: true);

            CredencialesKushki r = Respaldo();
            return new KushkiPublico(r.Prueba, r.LlavePublica ?? "", r.Completa);
        }

        public async Task<CredencialesKushki> CredencialesAsync()
        {
            ConfigKushkiIA c = _repositorio.obtenerConfigKushki().config;
            if (c == null) return Respaldo();

            string clave = c.secreto_ref + "|" + c.fecha_modificacion?.Ticks;
            lock (Candado)
            {
                if (_cache.clave == clave && _cache.expira > DateTime.UtcNow) return _cache.valor;
            }

            var (privada, webhook) = LeerSecreto(await _secretos.LeerAsync(c.secreto_ref));
            var valor = new CredencialesKushki(c.prueba, c.llave_publica, privada, webhook);
            lock (Candado)
            {
                _cache = (clave, valor, DateTime.UtcNow.Add(DuracionCache));
            }
            return valor;
        }

        /// <summary>Contenido del secreto: {"llave_privada","secreto_webhook"}. (null, null) si no existe.</summary>
        public static (string privada, string webhook) LeerSecreto(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (null, null);
            JObject o = JObject.Parse(json);
            return (o.Value<string>("llave_privada"), o.Value<string>("secreto_webhook"));
        }

        public static string EscribirSecreto(string privada, string webhook) =>
            new JObject { ["llave_privada"] = privada, ["secreto_webhook"] = webhook }.ToString(Newtonsoft.Json.Formatting.None);

        private CredencialesKushki Respaldo() => new(
            _configuration.GetValue("Kushki:Test", true),
            _configuration["Kushki:PublicKey"],
            _configuration["Kushki:PrivateKey"],
            _configuration["Kushki:WebhookSecret"]);
    }
}
