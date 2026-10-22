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

            if (activo)
            {
                var conexion = _repositorio.obtenerCredencial(Cliente, Sociedad);
                if (conexion == null || conexion.estado != EstadoCredencialIA.Verificada)
                {
                    return new Mensaje("E", "Configure y verifique primero la conexión con el proveedor de IA.", "");
                }
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

        public Mensaje obtenerConexion()
        {
            return new Mensaje("S", "", FragmentoConexion(_repositorio.obtenerCredencial(Cliente, Sociedad)));
        }

        public async Task<Mensaje> guardarConexionAsync(JObject jObject)
        {
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
            CredencialIA actual = _repositorio.obtenerCredencial(Cliente, Sociedad);
            string secretoRef = $"{_prefijoSecretos}/{Cliente}/{Sociedad}";

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

            _repositorio.mantenerCredencial(Cliente, Sociedad, nueva, _usuario.Usuario);
            return new Mensaje("S", "Conexión guardada.", FragmentoConexion(_repositorio.obtenerCredencial(Cliente, Sociedad)));
        }

        public async Task<Mensaje> probarConexionAsync()
        {
            CredencialIA actual = _repositorio.obtenerCredencial(Cliente, Sociedad);
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
                _repositorio.actualizarEstadoCredencial(Cliente, Sociedad, valida ? EstadoCredencialIA.Verificada : EstadoCredencialIA.Error);
            }

            string fragmento = FragmentoConexion(_repositorio.obtenerCredencial(Cliente, Sociedad));
            Mensaje error = MensajeVerificacion(resultado);
            // Se devuelve la conexión también en el error, para que la pantalla muestre el estado nuevo
            return error != null
                ? new Mensaje("E", error.mensaje, fragmento)
                : new Mensaje("S", "La conexión funciona correctamente.", fragmento);
        }

        public async Task<Mensaje> eliminarConexionAsync()
        {
            string secretoRef = _repositorio.eliminarCredencial(Cliente, Sociedad, _usuario.Usuario);
            if (secretoRef == null)
            {
                return new Mensaje("E", "No hay una conexión configurada.", "");
            }
            await _secretos.EliminarAsync(secretoRef);
            return new Mensaje("S", "Conexión eliminada. Los complementos dejarán de funcionar hasta configurar una nueva clave.", FragmentoConexion(null));
        }

        /// <summary>null si la clave sirve; si no, el mensaje de error para el usuario.</summary>
        private static Mensaje MensajeVerificacion(ResultadoVerificacion resultado) => resultado switch
        {
            ResultadoVerificacion.Valida or ResultadoVerificacion.ValidaConLimite => null,
            ResultadoVerificacion.Invalida => new Mensaje("E", "La API key no es válida o fue revocada.", ""),
            ResultadoVerificacion.SinPermisos => new Mensaje("E", "La API key no tiene permisos para usar la API.", ""),
            _ => new Mensaje("E", "No se pudo contactar al proveedor de IA. Intente nuevamente en unos minutos.", "")
        };

        /// <summary>"conexion":{...}|null,"modelos":[...] — sin la referencia del secreto.</summary>
        private static string FragmentoConexion(CredencialIA c)
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
            return "\"conexion\":" + JsonConvert.SerializeObject(conexion)
                + ",\"modelos\":" + JsonConvert.SerializeObject(ProveedoresIA.ModelosAnthropic);
        }
    }
}
