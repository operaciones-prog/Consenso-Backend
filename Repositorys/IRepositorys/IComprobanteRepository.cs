using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IComprobanteRepository
    {
        public Task<Mensaje> buscarComprobantes(JObject jObject);
    }
}
