using esupplier.Utils;
using System.Globalization;
using esupplier.Models;
using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.Auth;
using esupplier.Services.IServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UsuarioService> _logger;

        public UsuarioService(IUsuarioRepository usuarioRepository, IConfiguration configuration, ILogger<UsuarioService> logger)
        {
            _usuarioRepository = usuarioRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public Mensaje validarUsuario(JObject jObject)
        {
            ResultadoLogin resultado = _usuarioRepository.validarUsuario(jObject);
            Mensaje mensaje = resultado.Mensaje;

            // Sin Jwt:Key configurada se responde exactamente como antes (sin token).
            if (resultado.Exitoso && GeneradorTokenJwt.EstaConfigurado(_configuration))
            {
                try
                {
                    var (token, expiraEnUtc) = GeneradorTokenJwt.Generar(_configuration, resultado);
                    mensaje.data = mensaje.data
                        + ",\"token\":" + JsonConvert.SerializeObject(token)
                        + ",\"expiraEn\":" + JsonConvert.SerializeObject(expiraEnUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                }
                catch (Exception ex)
                {
                    // Una clave mal configurada (p. ej. menor a 32 bytes) no debe impedir el login.
                    _logger.LogError(ex, "No se pudo generar el JWT para el usuario {Usuario}", resultado.Usuario);
                }
            }

            return mensaje;
        }

        public Mensaje listarUsuario(JObject jObject)
        {
            return _usuarioRepository.listarUsuario(jObject);
        }

        public Mensaje mantenerUsuario(JObject jObject)
        {
            // El rol de plataforma (proveedor del sistema) no se asigna ni se edita desde la pantalla
            // de usuarios de la empresa: así un administrador de empresa no puede darse ese rol
            // ni cambiarle la clave al administrador de plataforma.
            if (JsonUtil.Opcional(jObject, "rol_id").Trim() == Constantes.ROL_PLATAFORMA)
            {
                return new Mensaje("E", "El rol de administrador de plataforma no se puede asignar desde aquí.", "");
            }
            if (int.TryParse(JsonUtil.Opcional(jObject, "reg_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int regId)
                && regId > 0 && _usuarioRepository.esUsuarioPlataforma(regId))
            {
                return new Mensaje("E", "Este usuario es de la plataforma y no se puede modificar desde aquí.", "");
            }
            return _usuarioRepository.mantenerUsuario(jObject);
        }
    }
}
