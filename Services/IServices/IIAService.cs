using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    /// <summary>
    /// Complementos IA. Cliente, sociedad y usuario salen siempre de la identidad validada, nunca del body.
    /// Con plataforma=true la conexión es la clave de IA de la plataforma (la usan las empresas con saldo).
    /// </summary>
    public interface IIAService
    {
        Mensaje listarAddons();
        Mensaje mantenerAddon(JObject jObject);
        Mensaje obtenerConexion(bool plataforma = false);
        Task<Mensaje> guardarConexionAsync(JObject jObject, bool plataforma = false);
        Task<Mensaje> probarConexionAsync(bool plataforma = false);
        Task<Mensaje> eliminarConexionAsync(bool plataforma = false);
    }
}
