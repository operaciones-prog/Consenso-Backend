using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    public interface IConfiguracionService
    {
        /// <summary>Uso interno: devuelve las constantes con sus valores reales (incluye secretos).</summary>
        public Mensaje listarConstantes(JObject jObject);
        /// <summary>Uso del endpoint público: enmascara los valores sensibles (claves, secrets).</summary>
        public Mensaje listarConstantesPublico(JObject jObject);
        public Mensaje mantenerConstante(JObject jObject);
        public Mensaje listarSociedad(JObject jObject);
        public Mensaje mantenerSociedad(JObject jObject);

        public Mensaje registrarCondicionPago(JObject jObject);

        public Mensaje listarCondicionPago();

        public Mensaje eliminarCondicionPago(int id);

        public Mensaje buscarCondicionPagoPorCodigo(string codigo);
    }
}
