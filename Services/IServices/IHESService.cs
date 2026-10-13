using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    public interface IHESService
    {
        public Task<Mensaje> buscarHES(JObject jObject);
    }
}
