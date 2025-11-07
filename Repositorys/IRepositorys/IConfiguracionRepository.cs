using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IConfiguracionRepository
    {
        public Mensaje listarConstantes(JObject jObject);
        public Mensaje mantenerConstante(JObject jObject);
        public Mensaje listarSociedad(JObject jObject);
        public Mensaje mantenerSociedad(JObject jObject);

        public Mensaje registrarCondicionPago(JObject jObject);

        public Mensaje listarCondicionPago();

        public Mensaje eliminarCondicionPago(int id);

        public Mensaje buscarCondicionPagoPorCodigo(string codigo);
    }
}
