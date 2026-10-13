using esupplier.Models;
using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IUsuarioRepository
    {
        public ResultadoLogin validarUsuario(JObject jObject);
        public Mensaje listarUsuario(JObject jObject);
        public Mensaje mantenerUsuario(JObject jObject);
    }
}
