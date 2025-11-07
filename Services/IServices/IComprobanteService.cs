using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    public interface IComprobanteService
    {
        public Mensaje buscarComprobantes(JObject jObject);
    }
}
