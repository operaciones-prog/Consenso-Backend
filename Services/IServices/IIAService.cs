using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    /// <summary>
    /// Complementos IA. Cliente, sociedad y usuario salen siempre del token, nunca del body.
    /// </summary>
    public interface IIAService
    {
        Mensaje listarAddons();
        Mensaje mantenerAddon(JObject jObject);
        Mensaje obtenerConexion();
        Task<Mensaje> guardarConexionAsync(JObject jObject);
        Task<Mensaje> probarConexionAsync();
        Task<Mensaje> eliminarConexionAsync();
    }
}
