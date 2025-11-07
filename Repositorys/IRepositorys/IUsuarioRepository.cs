using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IUsuarioRepository
    {
        public Mensaje validarUsuario(JObject jObject);
        public Mensaje listarUsuario(JObject jObject);
        public Mensaje mantenerUsuario(JObject jObject);
    }
}
