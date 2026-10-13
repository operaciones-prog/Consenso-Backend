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
    public class HESController : ControllerBase
    {
        private readonly IHESService _HESService;
        private readonly ILogger<HESController> _logger;

        public HESController(IHESService hesService, ILogger<HESController> logger)
        {
            _HESService = hesService;
            _logger = logger;
        }

        [HttpPost("buscarHES")]
        public async Task<ActionResult> buscarHES()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = await _HESService.buscarHES(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en buscarHES");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "HES.buscarHES"));
            }
        }
    }
}
