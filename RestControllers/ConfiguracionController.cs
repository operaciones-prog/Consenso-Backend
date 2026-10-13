using esupplier.Models.Response;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.ResControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfiguracionController : ControllerBase
    {
        private readonly IConfiguracionService _configuracionService;
        private readonly ILogger<ConfiguracionController> _logger;

        public ConfiguracionController(IConfiguracionService configuracionService, ILogger<ConfiguracionController> logger)
        {
            _configuracionService = configuracionService;
            _logger = logger;
        }

        [HttpPost("listarConstantes")]
        public async Task<ActionResult> listarConstantes()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                // Endpoint público: los valores sensibles se devuelven enmascarados.
                Mensaje msj = _configuracionService.listarConstantesPublico(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en listarConstantes");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Configuracion.listarConstantes"));
            }
        }

        [HttpPost("mantenerConstante")]
        public async Task<ActionResult> mantenerConstante()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _configuracionService.mantenerConstante(jObject);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en mantenerConstante");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Configuracion.mantenerConstante"));
            }
        }

        [HttpPost("listarSociedad")]
        public async Task<ActionResult> listarSociedad()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _configuracionService.listarSociedad(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en listarSociedad");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Configuracion.listarSociedad"));
            }
        }

        [HttpPost("mantenerSociedad")]
        public async Task<ActionResult> mantenerSociedad()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _configuracionService.mantenerSociedad(jObject);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en mantenerSociedad");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Configuracion.mantenerSociedad"));
            }
        }

        [HttpGet("listarCalificacionPro")]
        public ActionResult listarCalificacionPro()
        {
            try
            {
                JObject jObject = new JObject
                {
                    ["adm_cliente_id"] = Constantes.ADM_CLIENTE,
                    ["tipo_constante"] = "PRO_CALI",
                    ["codigo_constante"] = ""
                };
                Mensaje msj = _configuracionService.listarConstantesPublico(jObject);
                if (msj == null || msj.tipo != "S")
                {
                    return Ok(ApiRespuesta.DesdeMensaje(msj));
                }

                JToken constantes = JObject.Parse("{" + msj.data + "}")["constantes"] ?? new JArray();
                return Ok(ApiRespuesta.ExitoConJson(msj.mensaje, constantes.ToString(Formatting.None)));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Configuracion.listarCalificacionPro"));
            }
        }

        [HttpGet("listarCondicionPago")]
        public ActionResult listarCondicionPago()
        {
            try
            {
                Mensaje msj = _configuracionService.listarCondicionPago();
                if (msj == null || msj.tipo != "S")
                {
                    return Ok(ApiRespuesta.DesdeMensaje(msj));
                }

                JToken condiciones = JObject.Parse("{" + msj.data + "}")["condiciones"] ?? new JArray();
                return Ok(ApiRespuesta.ExitoConJson(msj.mensaje, condiciones.ToString(Formatting.None)));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Configuracion.listarCondicionPago"));
            }
        }

        [HttpPost("registrarCondicionPago")]
        public async Task<ActionResult> registrarCondicionPago()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _configuracionService.registrarCondicionPago(jObject);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en registrarCondicionPago");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Configuracion.registrarCondicionPago"));
            }
        }

        // Se mantiene el GET histórico usado por el frontend; DELETE es el verbo correcto.
        [HttpGet("eliminarCondicionPago/{id}")]
        [HttpDelete("eliminarCondicionPago/{id}")]
        public ActionResult eliminarCondicionPago([FromRoute] int id)
        {
            try
            {
                Mensaje msj = _configuracionService.eliminarCondicionPago(id);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Configuracion.eliminarCondicionPago"));
            }
        }
    }
}
