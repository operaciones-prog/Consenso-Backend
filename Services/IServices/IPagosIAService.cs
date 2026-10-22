using esupplier.Models.Response;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IServices
{
    /// <summary>
    /// Recargas de saldo IA con comprobante: la empresa las registra y el administrador
    /// de plataforma las aprueba o rechaza (pidiendo de nuevo su contraseña).
    /// </summary>
    public interface IPagosIAService
    {
        /// <summary>Empresa. Body: monto_usd, banco, referencia, fecha_pago (yyyy-MM-dd), file {fileName, fileContent (base64)}.</summary>
        Task<Mensaje> registrarRecargaAsync(JObject jObject);

        /// <summary>Empresa: sus recargas.</summary>
        Mensaje listarRecargasEmpresa();

        /// <summary>Plataforma: recargas de todas las empresas. Body: estado (opcional).</summary>
        Mensaje listarRecargasPlataforma(JObject jObject);

        /// <summary>Plataforma. Body: reg_id.</summary>
        Task<Mensaje> descargarComprobanteAsync(JObject jObject);

        /// <summary>Plataforma. Body: reg_id, monto_aprobado, clave.</summary>
        Mensaje aprobarRecarga(JObject jObject);

        /// <summary>Plataforma. Body: reg_id, motivo, clave.</summary>
        Mensaje rechazarRecarga(JObject jObject);
    }
}
