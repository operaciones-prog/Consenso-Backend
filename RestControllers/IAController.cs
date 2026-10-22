using esupplier.Middleware.Autorizacion;
using esupplier.Services.IA;
using esupplier.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace esupplier.ResControllers
{
    /// <summary>
    /// Complementos IA de la empresa: catálogo, activación por sociedad, conexión con su propia
    /// API key y recargas de saldo para usar la clave de la plataforma.
    /// Cliente, sociedad y usuario salen de la identidad validada por [RequiereSesion].
    /// La API key nunca se devuelve: solo sus últimos 4 caracteres.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class IAController : ApiControllerBase
    {
        private readonly IIAService _iaService;
        private readonly IPagosIAService _pagosService;
        private readonly IAgentesIAService _agentes;
        private readonly IConsumoIAService _consumo;
        private readonly ILogger<IAController> _logger;

        public IAController(IIAService iaService, IPagosIAService pagosService, IAgentesIAService agentes, IConsumoIAService consumo,
            ILogger<IAController> logger)
        {
            _iaService = iaService;
            _pagosService = pagosService;
            _agentes = agentes;
            _consumo = consumo;
            _logger = logger;
        }

        protected override ILogger Logger => _logger;

        /// <summary>Catálogo con el estado de cada complemento en la sociedad del usuario.</summary>
        [RequiereSesion]
        [HttpPost("listarAddons")]
        public Task<ActionResult> listarAddons() =>
            Ejecutar("IA.listarAddons", _ => _iaService.listarAddons(), conObjeto: true);

        /// <summary>Body: {"codigo_addon":"...","activo":true|false}</summary>
        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("mantenerAddon")]
        public Task<ActionResult> mantenerAddon() =>
            Ejecutar("IA.mantenerAddon", json => _iaService.mantenerAddon(json), conObjeto: false);

        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("obtenerConexion")]
        public Task<ActionResult> obtenerConexion() =>
            Ejecutar("IA.obtenerConexion", _ => _iaService.obtenerConexion(), conObjeto: true);

        /// <summary>Body: {"proveedor":"anthropic","modelo":"...","api_key":"...","limite_mensual_usd":50}. Sin api_key solo cambia modelo y límite.</summary>
        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("guardarConexion")]
        public Task<ActionResult> guardarConexion() =>
            Ejecutar("IA.guardarConexion", json => _iaService.guardarConexionAsync(json), conObjeto: true);

        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("probarConexion")]
        public Task<ActionResult> probarConexion() =>
            Ejecutar("IA.probarConexion", _ => _iaService.probarConexionAsync(), conObjeto: true);

        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("eliminarConexion")]
        public Task<ActionResult> eliminarConexion() =>
            Ejecutar("IA.eliminarConexion", _ => _iaService.eliminarConexionAsync(), conObjeto: true);

        /// <summary>Body: monto_usd, banco, referencia, fecha_pago (yyyy-MM-dd), file {fileName, fileContent}.</summary>
        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("registrarRecarga")]
        public Task<ActionResult> registrarRecarga() =>
            Ejecutar("IA.registrarRecarga", json => _pagosService.registrarRecargaAsync(json), conObjeto: true);

        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("listarRecargas")]
        public Task<ActionResult> listarRecargas() =>
            Ejecutar("IA.listarRecargas", _ => _pagosService.listarRecargasEmpresa(), conObjeto: true);

        /// <summary>
        /// Ejecuta un agente. Body: {"codigo":"analista_desempeno","contexto":{...}} → data {"respuesta": Markdown}.
        /// El agente debe estar activo en la sociedad y permitido para el rol (lo valida el motor).
        /// </summary>
        [RequiereSesion]
        [HttpPost("agente")]
        public Task<ActionResult> agente() =>
            Ejecutar("IA.agente", json => _agentes.ejecutarAsync(json), conObjeto: true);

        /// <summary>Asistente del proveedor (chat). Body: {"mensajes":[{"rol":"user"|"assistant","texto":"..."}]}.</summary>
        [RequiereSesion]
        [HttpPost("asistente")]
        public Task<ActionResult> asistente() =>
            Ejecutar("IA.asistente", json => _agentes.conversarAsync(json), conObjeto: true);

        /// <summary>Consumo de IA de la empresa (tokens y costo). Body: {"desde":"yyyy-MM-dd","hasta":"yyyy-MM-dd"}.</summary>
        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("resumenConsumo")]
        public Task<ActionResult> resumenConsumo() =>
            Ejecutar("IA.resumenConsumo", json => _consumo.resumen(json, plataforma: false), conObjeto: true);
    }
}
