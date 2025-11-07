using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    public class ProveedorService : IProveedorService
    {
        private IProveedorRepository _proveedorRepository;

        public ProveedorService(IProveedorRepository proveedorRepository)
        {
            _proveedorRepository = proveedorRepository;
        }

        public Mensaje buscarProveedor(JObject jObject)
        {
            return _proveedorRepository.buscarProveedor(jObject);
        }

        public Mensaje registrarProveedor(JObject jObject)
        {
            return _proveedorRepository.registrarProveedor(jObject);
        }

        public Mensaje registrarCalificacion(List<Dictionary<string, object>> result)
        {
            return _proveedorRepository.registrarCalificacion(result);
        }

        public Mensaje comboCalificacion(int tipo, string codigo)
        {
            return _proveedorRepository.comboCalificacion(tipo, codigo);
        }

        public Mensaje obtenerCalificacion(int tipo, string codigo, string annio, string mes, string setCalificacion, string origen)
        {
            return _proveedorRepository.obtenerCalificacion(tipo, codigo, annio, mes, setCalificacion,  origen);
        }

      
    }
}
