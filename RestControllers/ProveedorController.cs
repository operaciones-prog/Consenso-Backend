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
    public class ProveedorController : ControllerBase
    {
        private readonly IProveedorService _proveedorService;
        private readonly IConfiguration _configuration;
        private readonly IHeaderService _headerService;
        private readonly IConfiguracionService _configuracionService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProveedorController> _logger;

        public ProveedorController(
            IProveedorService proveedorService,
            IConfiguration configuration,
            IConfiguracionService configuracionService,
            IHeaderService headerService,
            IHttpClientFactory httpClientFactory,
            ILogger<ProveedorController> logger)
        {
            _proveedorService = proveedorService;
            _configuracionService = configuracionService;
            _configuration = configuration;
            _headerService = headerService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpPost("buscarProveedor")]
        public async Task<ActionResult> buscarProveedor()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _proveedorService.buscarProveedor(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en buscarProveedor");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Proveedor.buscarProveedor"));
            }
        }

        [HttpPost("registrarProveedor")]
        public async Task<ActionResult> registrarProveedor()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _proveedorService.registrarProveedor(jObject);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en registrarProveedor");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Proveedor.registrarProveedor"));
            }
        }

        [HttpPost("consultarCreditoProveedores")]
        public async Task<ActionResult> consultarCreditoProveedores()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);

                HttpClient cliente = _httpClientFactory.CreateClient(Constantes.HTTP_CLIENT_SAP);
                string responseBody = await ConsultaUtil.PostSapAsync(cliente, _configuration["Http:consultarCreditoProveedores"], jObject.ToString());
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return Ok(ApiRespuesta.Error("Sin respuesta - null"));
                }

                JObject objJsonAux = JObject.Parse(responseBody);
                string condicion = (string)objJsonAux["E_ZTERM"];
                if (condicion == null)
                {
                    return Ok(ApiRespuesta.Error("No se encontraron registros"));
                }

                Mensaje msj = _configuracionService.buscarCondicionPagoPorCodigo(condicion);
                if (msj == null || msj.tipo != "S")
                {
                    return Ok(ApiRespuesta.DesdeMensaje(msj));
                }

                JArray condiciones = JObject.Parse("{" + msj.data + "}")["condiciones"] as JArray ?? new JArray();
                string dato = "-";
                string idioma = _headerService.obtenerIdioma();
                foreach (JToken item in condiciones)
                {
                    dato = idioma == "ES" ? (string)item["nombreES"] : (string)item["nombreEN"];
                }

                return Ok(ApiRespuesta.ExitoSoloData(new JValue(dato)));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "JSON inválido en consultarCreditoProveedores");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición o la respuesta de SAP no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Proveedor.consultarCreditoProveedores"));
            }
        }

        [HttpPost("consultarProyeccionMateriales")]
        public async Task<ActionResult> consultarProyeccionMateriales()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);

                HttpClient cliente = _httpClientFactory.CreateClient(Constantes.HTTP_CLIENT_SAP);
                string responseBody = await ConsultaUtil.PostSapAsync(cliente, _configuration["Http:consultarProyeccionMateriales"], jObject.ToString());
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return Ok(ApiRespuesta.Error("Sin respuesta - null"));
                }

                JObject objJsonAux = JObject.Parse(responseBody);
                if (!(objJsonAux["ES_DATA"]?["item"] is JArray array))
                {
                    return Ok(ApiRespuesta.Error("No se encontraron registros"));
                }

                return Ok(ApiRespuesta.ExitoSoloData(array));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "JSON inválido en consultarProyeccionMateriales");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición o la respuesta de SAP no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Proveedor.consultarProyeccionMateriales"));
            }
        }

        [HttpGet("comboCalificacionMes")]
        public ActionResult comboCalificacionMes([FromQuery] string? codigo)
        {
            return Combo(1, codigo, "Proveedor.comboCalificacionMes");
        }

        [HttpGet("comboCalificacionAnnio")]
        public ActionResult comboCalificacionAnnio([FromQuery] string? codigo)
        {
            return Combo(2, codigo, "Proveedor.comboCalificacionAnnio");
        }

        [HttpGet("comboCalificacioCalificacion")]
        public ActionResult comboCalificacioCalificacion([FromQuery] string? codigo)
        {
            return Combo(3, codigo, "Proveedor.comboCalificacioCalificacion");
        }

        [HttpGet("comboCalificacioProveedor")]
        public ActionResult comboCalificacioProveedor()
        {
            return Combo(0, "", "Proveedor.comboCalificacioProveedor");
        }

        [HttpGet("comboCalificacioOrigen")]
        public ActionResult comboCalificacioOrigen([FromQuery] string? codigo)
        {
            return Combo(4, codigo, "Proveedor.comboCalificacioOrigen");
        }

        private ActionResult Combo(int tipo, string codigo, string contexto)
        {
            try
            {
                Mensaje msj = _proveedorService.comboCalificacion(tipo, codigo);
                return Ok(msj != null && msj.tipo == "S" ? ApiRespuesta.ExitoSoloData(msj.data) : ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, contexto));
            }
        }

        [HttpPost("obtenerCalificacion")]
        public async Task<ActionResult> obtenerCalificacion()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);

                string codigo = JsonUtil.Opcional(jObject, "codigo", null)?.Trim();
                string annio = JsonUtil.Opcional(jObject, "annio", null)?.Trim();
                string mes = null;
                if (jObject.TryGetValue("mes", out JToken mesesToken) && mesesToken is JArray)
                {
                    var meses = mesesToken.ToObject<List<string>>();
                    mes = string.Join(",", meses);
                }
                string calificacion = JsonUtil.Opcional(jObject, "calificacion", null)?.Trim();
                string origen = JsonUtil.Opcional(jObject, "origen", null)?.Trim();

                var data = new JObject();
                var secciones = new[] { (1, "general"), (2, "calificacion"), (3, "calidad"), (4, "otif") };
                foreach (var (tipo, nombre) in secciones)
                {
                    Mensaje msj = _proveedorService.obtenerCalificacion(tipo, codigo, annio, mes, calificacion, origen);
                    if (msj == null || msj.tipo != "S")
                    {
                        // El repositorio solo devuelve "E" ante errores de infraestructura (un resultado vacío es "S" con []).
                        return Ok(ApiRespuesta.DesdeMensaje(msj));
                    }
                    data[nombre] = string.IsNullOrWhiteSpace(msj.data) ? new JArray() : JToken.Parse(msj.data);
                }

                return Ok(ApiRespuesta.ExitoSoloData(data));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en obtenerCalificacion");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Proveedor.obtenerCalificacion"));
            }
        }
    }
}
