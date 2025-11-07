using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    public interface IUsuarioService
    {
        public Mensaje validarUsuario(JObject jObject);
        public Mensaje listarUsuario(JObject jObject);
        public Mensaje mantenerUsuario(JObject jObject);
    }
}
