using esupplier.Middleware.Autorizacion;
using esupplier.Models.Response;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.ResControllers
{
    /// <summary>
    /// Complementos IA: catálogo, activación por sociedad y conexión con el proveedor de IA.
    /// Exige sesión (JWT) aunque Auth:Enabled=false; la gestión es solo para administradores.
    /// La API key nunca se devuelve: solo sus últimos 4 caracteres.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class IAController : ControllerBase
    {
        private readonly IIAService _iaService;
        private readonly ILogger<IAController> _logger;

        public IAController(IIAService iaService, ILogger<IAController> logger)
        {
            _iaService = iaService;
            _logger = logger;
        }

        /// <summary>Catálogo con el estado de cada complemento en la sociedad del usuario.</summary>
        [RequiereSesion]
        [HttpPost("listarAddons")]
        public Task<ActionResult> listarAddons() =>
            Ejecutar("IA.listarAddons", _ => Task.FromResult(_iaService.listarAddons()), conObjeto: true);

        /// <summary>Body: {"codigo_addon":"...","activo":true|false}</summary>
        [RequiereSesion(soloAdministrador: true)]
        [HttpPost("mantenerAddon")]
        public Task<ActionResult> mantenerAddon() =>
            Ejecutar("IA.mantenerAddon", json => Task.FromResult(_iaService.mantenerAddon(json)), conObjeto: false);

        [RequiereSesion(soloAdministrador: true)]
        [HttpPost("obtenerConexion")]
        public Task<ActionResult> obtenerConexion() =>
            Ejecutar("IA.obtenerConexion", _ => Task.FromResult(_iaService.obtenerConexion()), conObjeto: true);

        /// <summary>Body: {"proveedor":"anthropic","modelo":"...","api_key":"...","limite_mensual_usd":50}. Sin api_key solo cambia modelo y límite.</summary>
        [RequiereSesion(soloAdministrador: true)]
        [HttpPost("guardarConexion")]
        public Task<ActionResult> guardarConexion() =>
            Ejecutar("IA.guardarConexion", json => _iaService.guardarConexionAsync(json), conObjeto: true);

        [RequiereSesion(soloAdministrador: true)]
        [HttpPost("probarConexion")]
        public Task<ActionResult> probarConexion() =>
            Ejecutar("IA.probarConexion", _ => _iaService.probarConexionAsync(), conObjeto: true);

        [RequiereSesion(soloAdministrador: true)]
        [HttpPost("eliminarConexion")]
        public Task<ActionResult> eliminarConexion() =>
            Ejecutar("IA.eliminarConexion", _ => _iaService.eliminarConexionAsync(), conObjeto: true);

        private async Task<ActionResult> Ejecutar(string contexto, Func<JObject, Task<Mensaje>> accion, bool conObjeto)
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = await accion(jObject);
                if (msj == null) return Ok(ApiRespuesta.Error(ApiRespuesta.MensajeErrorGenerico));
                if (msj.tipo != "S")
                {
                    return Ok(string.IsNullOrEmpty(msj.data)
                        ? ApiRespuesta.Error(msj.mensaje)
                        : ApiRespuesta.ErrorConObjeto(msj.mensaje, msj.data));
                }
                return Ok(conObjeto ? ApiRespuesta.ExitoConObjeto(msj.mensaje, msj.data) : ApiRespuesta.Exito(msj.mensaje));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en {Contexto}", contexto);
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, contexto));
            }
        }
    }
}
