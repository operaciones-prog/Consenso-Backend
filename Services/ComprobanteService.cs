using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    public class ComprobanteService : IComprobanteService
    {
        private IComprobanteRepository _comprobanteRepository;

        public ComprobanteService(IComprobanteRepository comprobanteRepository)
        {
            _comprobanteRepository = comprobanteRepository;
        }

        public Mensaje buscarComprobantes(JObject jObject)
        {
            return _comprobanteRepository.buscarComprobantes(jObject);
        }
    }
}
