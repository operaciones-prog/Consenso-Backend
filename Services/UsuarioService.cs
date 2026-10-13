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
            return _usuarioRepository.mantenerUsuario(jObject);
        }
    }
}
