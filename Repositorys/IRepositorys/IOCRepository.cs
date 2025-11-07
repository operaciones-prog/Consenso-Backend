using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IOCRepository
    {
        public Mensaje buscarOC(JObject jObject);
        public Mensaje registrarOCHistorico(JObject jObject);
        public Mensaje registrarOC(JObject jObject);
        public Mensaje leerOC(JObject jObject);
        public Mensaje obtenerTipoOC();
        public Mensaje buscarArchivoOc(int id);
        public Mensaje eliminarArchivoOc(int id, string usuario);
        public Mensaje listarArchivoOc(string oc,string sociedad, bool ruta);
        public Mensaje registrarArchivoOc(string numero_oc, string ruta, string nombre_original, string nombre_registro, string tipo, string sociedad, string reg_usuario);
        public Mensaje notificarOCPendiente();
        public Mensaje obtenerOC(JObject jObject);
        public Mensaje obtenerOCHistorico(JObject jObject);

    }
}
