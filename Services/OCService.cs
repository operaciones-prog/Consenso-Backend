using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    public class OCService : IOCService
    {
        private IOCRepository _ocRepository;

        public OCService(IOCRepository ocRepository)
        {
            _ocRepository = ocRepository;
        }

        public Mensaje buscarOC(JObject jObject)
        {
            return _ocRepository.buscarOC(jObject);
        }

        public Mensaje registrarOCHistorico(JObject jObject)
        {
            return _ocRepository.registrarOCHistorico(jObject);
        }

        public Mensaje registrarOC(JObject jObject)
        {
            return _ocRepository.registrarOC(jObject);
        }

        public Mensaje leerOC(JObject jObject)
        {
            return _ocRepository.leerOC(jObject);
        }

        public Mensaje obtenerTipoOC()
        {
            return _ocRepository.obtenerTipoOC();
        }

        public Mensaje registrarArchivoOc(string numero_oc, string ruta, string nombre_original, string nombre_registro, string tipo, string sociedad, string reg_usuario)
        {
            return _ocRepository.registrarArchivoOc(numero_oc, ruta, nombre_original, nombre_registro, tipo, sociedad, reg_usuario);
        }

        public Mensaje buscarArchivoOc(int id)
        {
            return _ocRepository.buscarArchivoOc(id);
        }

        public Mensaje listarArchivoOc(string oc, string sociedad, bool ruta)
        {
            return _ocRepository.listarArchivoOc(oc, sociedad,ruta);
        }

        public Mensaje eliminarArchivoOc(int id, string usuario)
        {
            return _ocRepository.eliminarArchivoOc(id, usuario);
        }

        public Mensaje notificarOCPendiente()
        {
            return _ocRepository.notificarOCPendiente();
        }

        public Mensaje obtenerOC(JObject jObject)
        {
            return _ocRepository.obtenerOC(jObject);
        }

        public Mensaje obtenerOCHistorico(JObject jObject)
        {
            return _ocRepository.obtenerOCHistorico(jObject);
        }
    }
}
