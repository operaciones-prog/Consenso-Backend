using esupplier.Models.Response;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.AspNetCore.Authorization;
using esupplier.Middleware.Autorizacion;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace esupplier.ResControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioService _UsuarioService;
        private readonly ILogger<UsuarioController> _logger;

        public UsuarioController(IUsuarioService usuarioService, ILogger<UsuarioController> logger)
        {
            _UsuarioService = usuarioService;
            _logger = logger;
        }

        [HttpPost("validarUsuario")]
        [AllowAnonymous]
        public async Task<ActionResult> validarUsuario()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _UsuarioService.validarUsuario(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (CampoRequeridoException ex)
            {
                return Ok(ApiRespuesta.Error(ex.Message));
            }
            catch (Exception ex)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, ex, "Usuario/validarUsuario"));
            }
        }

        [SoloInternos]
        [HttpPost("listarUsuario")]
        public async Task<ActionResult> listarUsuario()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _UsuarioService.listarUsuario(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (CampoRequeridoException ex)
            {
                return Ok(ApiRespuesta.Error(ex.Message));
            }
            catch (Exception ex)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, ex, "Usuario/listarUsuario"));
            }
        }

        [SoloInternos]
        [HttpPost("mantenerUsuario")]
        public async Task<ActionResult> mantenerUsuario()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _UsuarioService.mantenerUsuario(jObject);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (CampoRequeridoException ex)
            {
                return Ok(ApiRespuesta.Error(ex.Message));
            }
            catch (Exception ex)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, ex, "Usuario/mantenerUsuario"));
            }
        }
    }
}
