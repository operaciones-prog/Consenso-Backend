using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    public class HESService : IHESService
    {
        private IHESRepository _hesRepository;

        public HESService(IHESRepository hesRepository)
        {
            _hesRepository = hesRepository;
        }

        public Mensaje buscarHES(JObject jObject)
        {
            return _hesRepository.buscarHES(jObject);
        }
    }
}
