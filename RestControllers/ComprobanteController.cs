using esupplier.Models.Response;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.RestControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ComprobanteController : ControllerBase
    {
        private readonly IComprobanteService _comprobanteService;
        private readonly ILogger<ComprobanteController> _logger;

        public ComprobanteController(IComprobanteService comprobanteService, ILogger<ComprobanteController> logger)
        {
            _comprobanteService = comprobanteService;
            _logger = logger;
        }

        [HttpPost("buscarComprobantes")]
        public async Task<ActionResult> buscarComprobantes()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = await _comprobanteService.buscarComprobantes(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en buscarComprobantes");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Comprobante.buscarComprobantes"));
            }
        }
    }
}
