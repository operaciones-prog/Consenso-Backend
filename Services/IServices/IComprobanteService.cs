using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    public interface IComprobanteService
    {
        public Task<Mensaje> buscarComprobantes(JObject jObject);
    }
}
