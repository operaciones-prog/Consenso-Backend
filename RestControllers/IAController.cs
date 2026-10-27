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
        private readonly IPlanIAService _plan;
        private readonly ILogger<IAController> _logger;

        public IAController(IIAService iaService, IPagosIAService pagosService, IAgentesIAService agentes, IConsumoIAService consumo,
            IPlanIAService plan, ILogger<IAController> logger)
        {
            _iaService = iaService;
            _pagosService = pagosService;
            _agentes = agentes;
            _consumo = consumo;
            _plan = plan;
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

        /// <summary>Planes mensuales (con IVA), la suscripción de la sociedad, sus cobros y el saldo → data {"plan":{...}}.</summary>
        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("plan")]
        public Task<ActionResult> plan() =>
            Ejecutar("IA.plan", _ => _plan.plan(), conObjeto: true);

        /// <summary>
        /// Contrata o cambia de plan. Body: {"codigo_plan","token" (Kushki Hosted Fields, isSubscription),
        /// "contacto":{nombres, apellidos, email, tipo_documento (CI|RUC|PAS), numero_documento, telefono},
        /// "tarjeta":{marca, ultimos4}}. Con suscripción vigente solo hace falta codigo_plan.
        /// </summary>
        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("suscribirPlan")]
        public Task<ActionResult> suscribirPlan() =>
            Ejecutar("IA.suscribirPlan", json => _plan.suscribirAsync(json), conObjeto: true);

        /// <summary>Body: {"token","tarjeta":{marca, ultimos4}}.</summary>
        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("cambiarTarjetaPlan")]
        public Task<ActionResult> cambiarTarjetaPlan() =>
            Ejecutar("IA.cambiarTarjetaPlan", json => _plan.cambiarTarjetaAsync(json), conObjeto: true);

        /// <summary>Kushki deja de cobrar; el plan sigue hasta el fin del periodo pagado.</summary>
        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("cancelarPlan")]
        public Task<ActionResult> cancelarPlan() =>
            Ejecutar("IA.cancelarPlan", _ => _plan.cancelarAsync(), conObjeto: true);

        [RequiereSesion(Acceso.AdministradorEmpresa)]
        [HttpPost("anularCambioPlan")]
        public Task<ActionResult> anularCambioPlan() =>
            Ejecutar("IA.anularCambioPlan", _ => _plan.anularCambioPlanAsync(), conObjeto: true);

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

        /// <summary>
        /// Historial de consultas (qué se consultó y cuánto costó), paginado.
        /// Body: {"desde","hasta" (yyyy-MM-dd),"pagina":1,"tamanio":15,"buscar":"texto"}
        /// → data {"consultas":[...],"total":n,"costo_total_usd":x}.
        /// El administrador de la empresa ve toda la sociedad; los demás usuarios, solo sus consultas.
        /// </summary>
        [RequiereSesion]
        [HttpPost("historialConsumo")]
        public Task<ActionResult> historialConsumo() =>
            Ejecutar("IA.historialConsumo", json => _consumo.historial(json), conObjeto: true);
    }
}
