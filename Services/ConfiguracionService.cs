using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    public class ConfiguracionService : IConfiguracionService
    {
        private IConfiguracionRepository _configuracionRepository;

        public ConfiguracionService(IConfiguracionRepository configuracionRepository)
        {
            _configuracionRepository = configuracionRepository;
        }

        public Mensaje listarConstantes(JObject jObject)
        {
            return _configuracionRepository.listarConstantes(jObject);
        }

        public Mensaje mantenerConstante(JObject jObject)
        {
            return _configuracionRepository.mantenerConstante(jObject);
        }

        public Mensaje listarSociedad(JObject jObject)
        {
            return _configuracionRepository.listarSociedad(jObject);
        }

        public Mensaje mantenerSociedad(JObject jObject)
        {
            return _configuracionRepository.mantenerSociedad(jObject);
        }

        public Mensaje registrarCondicionPago(JObject jObject)
        {
            return _configuracionRepository.registrarCondicionPago((JObject) jObject);
        }

        public Mensaje listarCondicionPago()
        {
            return _configuracionRepository.listarCondicionPago();
        }

        public Mensaje eliminarCondicionPago(int id)
        {
            return _configuracionRepository.eliminarCondicionPago(id);  
        }

        public Mensaje buscarCondicionPagoPorCodigo(string codigo)
        {
            return _configuracionRepository.buscarCondicionPagoPorCodigo(codigo);
        }
    }
}
