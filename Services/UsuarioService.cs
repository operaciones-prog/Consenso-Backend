using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    public class UsuarioService : IUsuarioService
    {
        private IUsuarioRepository _usuarioRepository;

        public UsuarioService(IUsuarioRepository usuarioRepository)
        {
            _usuarioRepository = usuarioRepository;
        }

        public Mensaje validarUsuario(JObject jObject)
        {
            return _usuarioRepository.validarUsuario(jObject);
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
