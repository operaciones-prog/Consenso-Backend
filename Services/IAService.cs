using esupplier.Models.IA;
using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.Auth;
using esupplier.Services.IA;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace esupplier.Services
{
    public class IAService : IIAService
    {
        /// <summary>Error de sp_mantenerAddonActivacion: complemento inexistente o no disponible.</summary>
        private const int ErrorAddonNoDisponible = 50701;
        private const decimal LimiteMensualMaximo = 100000m;

        private readonly IIARepository _repositorio;
        private readonly IdentidadSolicitud _usuario;
        private readonly IHeaderService _headerService;
        private readonly IAlmacenSecretos _secretos;
        private readonly IVerificadorClaveIA _verificador;
        private readonly string _prefijoSecretos;

        public IAService(IIARepository repositorio, IdentidadSolicitud usuario, IHeaderService headerService,
            IAlmacenSecretos secretos, IVerificadorClaveIA verificador, IConfiguration configuration, IHostEnvironment entorno)
        {
            _repositorio = repositorio;
            _usuario = usuario;
            _headerService = headerService;
            _secretos = secretos;
            _verificador = verificador;
            // Por entorno, para que QAS y producción no compartan secretos aunque estén en la misma cuenta AWS
            _prefijoSecretos = (configuration["IA:SecretPrefix"] ?? $"consenso/{entorno.EnvironmentName.ToLowerInvariant()}/ia").TrimEnd('/');
        }

        private string Cliente => _usuario.Cliente;
        private string Sociedad => _usuario.Sociedad;

        /// <summary>Cliente y sociedad donde se guarda la conexión: la de la empresa o la de la plataforma.</summary>
        private (string cliente, string sociedad) Ambito(bool plataforma) => plataforma
            ? (Constantes.AMBITO_PLATAFORMA, Constantes.AMBITO_PLATAFORMA)
            : (Cliente, Sociedad);

        public Mensaje listarAddons()
        {
            string idioma = _headerService.obtenerIdioma() == "EN" ? "EN" : "ES";
            var addons = _repositorio.listarAddon(Cliente, Sociedad, idioma);
            return new Mensaje("S", "", "\"addons\":" + JsonConvert.SerializeObject(addons));
        }

        public Mensaje mantenerAddon(JObject jObject)
        {
            string codigoAddon = JsonUtil.Requerido(jObject, "codigo_addon");
            bool activo = jObject.Value<bool?>("activo") ?? throw new CampoRequeridoException("activo");

            if (activo && !PuedeUsarIA())
            {
                return new Mensaje("E", "Configure su propia API key o recargue saldo para usar la clave de la plataforma.", "");
            }

            try
            {
                _repositorio.mantenerAddonActivacion(Cliente, Sociedad, codigoAddon, activo, _usuario.Usuario);
            }
            catch (SqlException ex) when (ex.Number == ErrorAddonNoDisponible)
            {
                return new Mensaje("E", "El complemento no existe o todavía no está disponible.", "");
            }
            return new Mensaje("S", activo ? "Complemento activado." : "Complemento desactivado.", "");
        }

        /// <summary>
        /// La empresa puede usar IA si tiene su propia clave verificada, o si tiene saldo
        /// y la plataforma tiene su clave verificada.
        /// </summary>
        private bool PuedeUsarIA()
        {
            if (EstaVerificada(_repositorio.obtenerCredencial(Cliente, Sociedad))) return true;
            return _repositorio.obtenerSaldo(Cliente, Sociedad).saldo_usd > 0 && PlataformaDisponible();
        }

        private bool PlataformaDisponible() =>
            EstaVerificada(_repositorio.obtenerCredencial(Constantes.AMBITO_PLATAFORMA, Constantes.AMBITO_PLATAFORMA));

        private static bool EstaVerificada(CredencialIA c) => c != null && c.estado == EstadoCredencialIA.Verificada;

        public Mensaje obtenerConexion(bool plataforma = false)
        {
            var (cliente, sociedad) = Ambito(plataforma);
            return new Mensaje("S", "", Fragmento(_repositorio.obtenerCredencial(cliente, sociedad), plataforma));
        }

        public async Task<Mensaje> guardarConexionAsync(JObject jObject, bool plataforma = false)
        {
            var (cliente, sociedad) = Ambito(plataforma);

            string proveedor = JsonUtil.Opcional(jObject, "proveedor");
            if (string.IsNullOrWhiteSpace(proveedor)) proveedor = ProveedoresIA.Anthropic;
            if (proveedor != ProveedoresIA.Anthropic)
            {
                return new Mensaje("E", "Proveedor de IA no soportado.", "");
            }

            string modelo = JsonUtil.Opcional(jObject, "modelo");
            if (string.IsNullOrWhiteSpace(modelo)) modelo = ProveedoresIA.ModelosAnthropic[0];
            if (!ProveedoresIA.ModelosAnthropic.Contains(modelo))
            {
                return new Mensaje("E", "Modelo no soportado.", "");
            }

            decimal? limite = null;
            JToken limiteToken = jObject["limite_mensual_usd"];
            if (limiteToken != null && limiteToken.Type != JTokenType.Null && limiteToken.ToString() != "")
            {
                // Número JSON o texto con punto decimal, sin depender de la cultura del servidor
                string limiteTexto = limiteToken.Type == JTokenType.String
                    ? limiteToken.ToString()
                    : limiteToken.ToString(Formatting.None);
                if (!decimal.TryParse(limiteTexto, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor)
                    || valor <= 0 || valor > LimiteMensualMaximo)
                {
                    return new Mensaje("E", "El límite mensual debe ser un monto mayor a 0.", "");
                }
                limite = Math.Round(valor, 2);
            }

            string apiKey = (JsonUtil.Opcional(jObject, "api_key") ?? "").Trim();
            CredencialIA actual = _repositorio.obtenerCredencial(cliente, sociedad);
            string secretoRef = $"{_prefijoSecretos}/{cliente}/{sociedad}";

            CredencialIA nueva;
            if (apiKey == "")
            {
                // Sin clave nueva solo se cambian modelo y límite de la conexión existente
                if (actual == null)
                {
                    return new Mensaje("E", "Ingrese la API key.", "");
                }
                nueva = actual;
                nueva.modelo = modelo;
                nueva.limite_mensual_usd = limite;
            }
            else
            {
                if (apiKey.Length < 20 || apiKey.Length > 300 || apiKey.Any(char.IsWhiteSpace))
                {
                    return new Mensaje("E", "La API key no tiene un formato válido.", "");
                }

                ResultadoVerificacion resultado = await _verificador.VerificarAsync(proveedor, apiKey);
                Mensaje error = MensajeVerificacion(resultado);
                if (error != null) return error;

                await _secretos.GuardarAsync(secretoRef, apiKey);
                nueva = new CredencialIA
                {
                    proveedor = proveedor,
                    modelo = modelo,
                    secreto_ref = secretoRef,
                    sufijo = apiKey[^4..],
                    estado = EstadoCredencialIA.Verificada,
                    limite_mensual_usd = limite
                };
            }

            _repositorio.mantenerCredencial(cliente, sociedad, nueva, _usuario.Usuario);
            return new Mensaje("S", "Conexión guardada.", Fragmento(_repositorio.obtenerCredencial(cliente, sociedad), plataforma));
        }

        public async Task<Mensaje> probarConexionAsync(bool plataforma = false)
        {
            var (cliente, sociedad) = Ambito(plataforma);
            CredencialIA actual = _repositorio.obtenerCredencial(cliente, sociedad);
            if (actual == null)
            {
                return new Mensaje("E", "No hay una conexión configurada.", "");
            }

            string apiKey = await _secretos.LeerAsync(actual.secreto_ref);
            ResultadoVerificacion resultado = apiKey == null
                ? ResultadoVerificacion.Invalida
                : await _verificador.VerificarAsync(actual.proveedor, apiKey);

            // Si el proveedor no respondió no se sabe si la clave sirve: el estado no cambia
            if (resultado != ResultadoVerificacion.NoDisponible)
            {
                bool valida = resultado == ResultadoVerificacion.Valida || resultado == ResultadoVerificacion.ValidaConLimite;
                _repositorio.actualizarEstadoCredencial(cliente, sociedad, valida ? EstadoCredencialIA.Verificada : EstadoCredencialIA.Error);
            }

            string fragmento = Fragmento(_repositorio.obtenerCredencial(cliente, sociedad), plataforma);
            Mensaje error = MensajeVerificacion(resultado);
            // Se devuelve la conexión también en el error, para que la pantalla muestre el estado nuevo
            return error != null
                ? new Mensaje("E", error.mensaje, fragmento)
                : new Mensaje("S", "La conexión funciona correctamente.", fragmento);
        }

        public async Task<Mensaje> eliminarConexionAsync(bool plataforma = false)
        {
            var (cliente, sociedad) = Ambito(plataforma);
            string secretoRef = _repositorio.eliminarCredencial(cliente, sociedad, _usuario.Usuario);
            if (secretoRef == null)
            {
                return new Mensaje("E", "No hay una conexión configurada.", "");
            }
            await _secretos.EliminarAsync(secretoRef);
            string aviso = plataforma
                ? "Conexión eliminada. Las empresas que usan saldo no podrán usar IA hasta configurar una nueva clave."
                : "Conexión eliminada. Los complementos dejarán de funcionar hasta configurar una nueva clave.";
            return new Mensaje("S", aviso, Fragmento(null, plataforma));
        }

        /// <summary>null si la clave sirve; si no, el mensaje de error para el usuario.</summary>
        private static Mensaje MensajeVerificacion(ResultadoVerificacion resultado) => resultado switch
        {
            ResultadoVerificacion.Valida or ResultadoVerificacion.ValidaConLimite => null,
            ResultadoVerificacion.Invalida => new Mensaje("E", "La API key no es válida o fue revocada.", ""),
            ResultadoVerificacion.SinPermisos => new Mensaje("E", "La API key no tiene permisos para usar la API.", ""),
            _ => new Mensaje("E", "No se pudo contactar al proveedor de IA. Intente nuevamente en unos minutos.", "")
        };

        /// <summary>
        /// "conexion":{...}|null,"modelos":[...] — sin la referencia del secreto. Para la empresa agrega
        /// "saldo":{...} y "plataforma_disponible" (si puede usar la clave de la plataforma con saldo).
        /// </summary>
        private string Fragmento(CredencialIA c, bool plataforma)
        {
            object conexion = c == null ? null : new
            {
                c.proveedor,
                c.modelo,
                c.sufijo,
                c.estado,
                c.fecha_verificacion,
                c.limite_mensual_usd,
                c.modificado_por,
                c.fecha_modificacion,
                c.consumo_mes_usd,
                c.consultas_mes
            };
            string fragmento = "\"conexion\":" + JsonConvert.SerializeObject(conexion)
                + ",\"modelos\":" + JsonConvert.SerializeObject(ProveedoresIA.ModelosAnthropic);
            if (!plataforma)
            {
                fragmento += ",\"saldo\":" + JsonConvert.SerializeObject(_repositorio.obtenerSaldo(Cliente, Sociedad))
                    + ",\"plataforma_disponible\":" + (PlataformaDisponible() ? "true" : "false");
            }
            return fragmento;
        }
    }
}
