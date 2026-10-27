using esupplier.Models.IA;
using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.Auth;
using esupplier.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace esupplier.Services.IA
{
    public interface IKushkiPlataformaService
    {
        /// <summary>"kushki":{...}|null, "suscripciones_vivas", "respaldo_configuracion".</summary>
        Mensaje obtener();

        /// <summary>
        /// Body: {"prueba":true,"llave_publica","llave_privada","secreto_webhook","clave"}. Sin llave_privada o
        /// secreto_webhook se mantienen los guardados (la privada solo si no cambia el modo).
        /// </summary>
        Task<Mensaje> guardarAsync(JObject jObject);

        Task<Mensaje> probarAsync();

        /// <summary>Body: {"clave"}.</summary>
        Task<Mensaje> eliminarAsync(JObject jObject);
    }

    /// <summary>
    /// Llaves de Kushki de la plataforma (Plataforma > Pago con tarjeta, rol 6). Igual que la clave de IA:
    /// la llave privada y el id de firma de webhooks van a AWS Secrets Manager y nunca vuelven al navegador
    /// (solo sus últimos 4); la llave pública y el modo quedan en tb_ia_config_kushki.
    /// Guardar y eliminar piden de nuevo la contraseña: cambian a dónde va el dinero de los planes.
    /// </summary>
    public class KushkiPlataformaService : IKushkiPlataformaService
    {
        private static readonly Regex PatronLlave = new("^[A-Za-z0-9_-]{16,100}$");
        private static readonly Regex PatronSecretoWebhook = new(@"^\S{8,200}$");

        private readonly IPlanIARepository _repositorio;
        private readonly IIARepository _ia;
        private readonly IdentidadSolicitud _usuario;
        private readonly IAlmacenSecretos _secretos;
        private readonly IKushkiCliente _kushki;
        private readonly IConfiguration _configuration;
        private readonly string _secretoRef;

        public KushkiPlataformaService(IPlanIARepository repositorio, IIARepository ia, IdentidadSolicitud usuario,
            IAlmacenSecretos secretos, IKushkiCliente kushki, IConfiguration configuration, IHostEnvironment entorno)
        {
            _repositorio = repositorio;
            _ia = ia;
            _usuario = usuario;
            _secretos = secretos;
            _kushki = kushki;
            _configuration = configuration;
            // Mismo prefijo que la clave de IA (el rol IAM de la Lambda ya tiene permiso sobre él)
            string prefijo = (configuration["IA:SecretPrefix"] ?? $"consenso/{entorno.EnvironmentName.ToLowerInvariant()}/ia").TrimEnd('/');
            _secretoRef = $"{prefijo}/{Constantes.AMBITO_PLATAFORMA}/kushki";
        }

        public Mensaje obtener() => new Mensaje("S", "", Fragmento());

        public async Task<Mensaje> guardarAsync(JObject jObject)
        {
            Mensaje error = ValidarClave(jObject);
            if (error != null) return error;

            bool prueba = jObject.Value<bool?>("prueba") ?? throw new CampoRequeridoException("prueba");
            string publica = (JsonUtil.Opcional(jObject, "llave_publica") ?? "").Trim();
            string privada = (JsonUtil.Opcional(jObject, "llave_privada") ?? "").Trim();
            string webhook = (JsonUtil.Opcional(jObject, "secreto_webhook") ?? "").Trim();

            if (!PatronLlave.IsMatch(publica)) return new Mensaje("E", "La llave pública no tiene un formato válido.", "");
            if (privada != "" && !PatronLlave.IsMatch(privada)) return new Mensaje("E", "La llave privada no tiene un formato válido.", "");
            if (webhook != "" && !PatronSecretoWebhook.IsMatch(webhook)) return new Mensaje("E", "El id de firma de webhooks no tiene un formato válido.", "");

            var (actual, vivas) = _repositorio.obtenerConfigKushki();
            bool cambiaModo = actual != null && actual.prueba != prueba;
            if (cambiaModo && vivas > 0)
            {
                return new Mensaje("E", $"Hay {vivas} suscripción(es) activa(s) creada(s) en modo {(actual.prueba ? "prueba" : "producción")}. "
                    + "Si cambia de modo, Kushki dejaría de informar sus cobros. Espere a que se cancelen antes de cambiar.", "");
            }

            // Lo que no se envía se mantiene (en el mismo modo: las llaves de prueba y producción son distintas)
            if (privada == "" || webhook == "")
            {
                if (actual == null || cambiaModo)
                {
                    if (privada == "") return new Mensaje("E", "Ingrese la llave privada.", "");
                }
                else
                {
                    var (privadaActual, webhookActual) = ConfiguracionKushki.LeerSecreto(await _secretos.LeerAsync(actual.secreto_ref));
                    if (privada == "") privada = privadaActual;
                    if (webhook == "") webhook = webhookActual ?? "";
                    if (string.IsNullOrEmpty(privada)) return new Mensaje("E", "No se encontró la llave privada guardada. Ingrésela de nuevo.", "");
                }
            }

            error = MensajeVerificacion(await _kushki.VerificarAsync(prueba, publica, privada), prueba);
            if (error != null) return error;

            await _secretos.GuardarAsync(_secretoRef, ConfiguracionKushki.EscribirSecreto(privada, webhook == "" ? null : webhook));
            _repositorio.guardarConfigKushki(new ConfigKushkiIA
            {
                prueba = prueba,
                llave_publica = publica,
                secreto_ref = _secretoRef,
                sufijo_privada = privada[^4..],
                sufijo_webhook = webhook == "" ? null : webhook[^4..]
            }, _usuario.Usuario);

            return new Mensaje("S", webhook == ""
                ? "Llaves verificadas y guardadas. Falta el id de firma de webhooks: sin él no se acreditan los cobros."
                : "Llaves verificadas y guardadas. El pago con tarjeta ya está habilitado.", Fragmento());
        }

        public async Task<Mensaje> probarAsync()
        {
            ConfigKushkiIA actual = _repositorio.obtenerConfigKushki().config;
            if (actual == null) return new Mensaje("E", "No hay llaves de Kushki configuradas.", "");

            string privada = ConfiguracionKushki.LeerSecreto(await _secretos.LeerAsync(actual.secreto_ref)).privada;
            VerificacionKushki resultado = string.IsNullOrEmpty(privada)
                ? VerificacionKushki.PrivadaInvalida
                : await _kushki.VerificarAsync(actual.prueba, actual.llave_publica, privada);

            // Si Kushki no respondió no se sabe si las llaves sirven: el estado no cambia
            if (resultado != VerificacionKushki.NoDisponible)
            {
                _repositorio.actualizarEstadoConfigKushki(resultado == VerificacionKushki.Valida ? "verificada" : "error");
            }

            Mensaje error = MensajeVerificacion(resultado, actual.prueba);
            return error != null
                ? new Mensaje("E", error.mensaje, Fragmento())
                : new Mensaje("S", "Las llaves de Kushki funcionan correctamente.", Fragmento());
        }

        public async Task<Mensaje> eliminarAsync(JObject jObject)
        {
            Mensaje error = ValidarClave(jObject);
            if (error != null) return error;

            int vivas = _repositorio.obtenerConfigKushki().suscripcionesVivas;
            if (vivas > 0)
            {
                return new Mensaje("E", $"Hay {vivas} suscripción(es) activa(s): sin las llaves no se podrían acreditar sus cobros.", "");
            }

            string secretoRef = _repositorio.eliminarConfigKushki();
            if (secretoRef == null) return new Mensaje("E", "No hay llaves de Kushki configuradas.", "");
            await _secretos.EliminarAsync(secretoRef);
            return new Mensaje("S", "Llaves de Kushki eliminadas. Las empresas no podrán contratar planes con tarjeta.", Fragmento());
        }

        private static Mensaje MensajeVerificacion(VerificacionKushki resultado, bool prueba) => resultado switch
        {
            VerificacionKushki.Valida => null,
            VerificacionKushki.PublicaInvalida => new Mensaje("E",
                $"Kushki no reconoce la llave pública en modo {(prueba ? "prueba" : "producción")}. Revise la llave y el modo.", ""),
            VerificacionKushki.PrivadaInvalida => new Mensaje("E",
                $"Kushki no reconoce la llave privada en modo {(prueba ? "prueba" : "producción")}. Revise la llave y el modo.", ""),
            _ => new Mensaje("E", "No se pudo contactar a Kushki. Intente nuevamente en unos minutos.", "")
        };

        /// <summary>Igual que al validar recargas: mientras no haya JWT, la identidad viene de cabeceras.</summary>
        private Mensaje ValidarClave(JObject jObject)
        {
            string clave = JsonUtil.Opcional(jObject, "clave");
            if (string.IsNullOrEmpty(clave)) return new Mensaje("E", "Ingrese su contraseña para confirmar.", "");

            var almacenadas = _ia.obtenerClavesUsuario(_usuario.Cliente, _usuario.Sociedad, _usuario.Usuario);
            bool valida = almacenadas.Any(a => PasswordHasher.Verificar(clave, a, out _));
            return valida ? null : new Mensaje("E", "La contraseña no es correcta.", "");
        }

        /// <summary>"kushki":{...}|null (sin secreto_ref), "suscripciones_vivas", "respaldo_configuracion".</summary>
        private string Fragmento()
        {
            var (c, vivas) = _repositorio.obtenerConfigKushki();
            object kushki = c == null ? null : new
            {
                c.prueba,
                c.llave_publica,
                c.sufijo_privada,
                c.sufijo_webhook,
                c.estado,
                c.fecha_verificacion,
                c.modificado_por,
                c.fecha_modificacion
            };
            // Sin configuración de plataforma, ¿el backend tiene llaves de respaldo (Kushki:* en la configuración)?
            bool respaldo = !string.IsNullOrWhiteSpace(_configuration["Kushki:PrivateKey"]) && !string.IsNullOrWhiteSpace(_configuration["Kushki:PublicKey"]);
            return "\"kushki\":" + JsonConvert.SerializeObject(kushki)
                + ",\"suscripciones_vivas\":" + vivas
                + ",\"respaldo_configuracion\":" + (respaldo ? "true" : "false");
        }
    }
}
