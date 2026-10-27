using esupplier.Services.IA;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace esupplier.ResControllers
{
    /// <summary>
    /// Webhooks de Kushki (cobros de las suscripciones de los planes de IA). No lleva sesión: la autenticidad
    /// se comprueba con la firma HMAC (id de firma de webhooks de Plataforma > Pago con tarjeta). Configurar en la consola de Kushki
    /// (Desarrolladores > Webhooks) la URL https://&lt;api&gt;/api/Kushki/webhook.
    /// Responde 200 si se procesó (o no requiere acción), 401 si la firma no es válida y 500 ante un error,
    /// para que Kushki reintente (hasta 7 veces en 3 horas).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class KushkiController : ControllerBase
    {
        /// <summary>Los eventos de Kushki son pequeños; se corta antes de leer cuerpos enormes.</summary>
        private const int TamanoMaximo = 256 * 1024;

        private readonly IPlanIAService _plan;
        private readonly ILogger<KushkiController> _logger;

        public KushkiController(IPlanIAService plan, ILogger<KushkiController> logger)
        {
            _plan = plan;
            _logger = logger;
        }

        [HttpPost("webhook")]
        public async Task<ActionResult> webhook()
        {
            if (Request.ContentLength > TamanoMaximo) return StatusCode(StatusCodes.Status413PayloadTooLarge);

            // La firma se calcula sobre el cuerpo exacto: se lee sin parsear
            using var lector = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string cuerpo = await lector.ReadToEndAsync();
            try
            {
                bool valido = await _plan.procesarWebhookAsync(cuerpo,
                    Request.Headers["X-Kushki-Id"].ToString(),
                    Request.Headers["X-Kushki-Signature"].ToString(),
                    Request.Headers["X-Kushki-SimpleSignature"].ToString());
                if (!valido)
                {
                    _logger.LogWarning("Webhook de Kushki con firma no válida desde {Ip}", HttpContext.Connection.RemoteIpAddress);
                    return Unauthorized();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el procesamiento de un webhook de Kushki");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
