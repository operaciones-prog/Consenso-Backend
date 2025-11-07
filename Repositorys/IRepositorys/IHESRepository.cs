using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IHESRepository
    {
        public Mensaje buscarHES(JObject jObject);
    }
}
