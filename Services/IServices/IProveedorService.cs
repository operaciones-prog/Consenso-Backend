using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    public interface IProveedorService
    {
        public Mensaje buscarProveedor(JObject jObject);
        public Mensaje registrarProveedor(JObject jObject);

        public Mensaje registrarCalificacion(List<Dictionary<string, object>> result);

        public Mensaje comboCalificacion(int tipo, string codigo);
        public Mensaje obtenerCalificacion(int tipo, string codigo, string annio, string mes, string setCalificacion, string origen);
    }
}
