using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IComprobanteRepository
    {
        public Mensaje buscarComprobantes(JObject jObject);
    }
}
