using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    public interface IConfiguracionService
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
