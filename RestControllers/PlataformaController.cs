using esupplier.Middleware.Autorizacion;
using esupplier.Services.IA;
using esupplier.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace esupplier.ResControllers
{
    /// <summary>
    /// Administración de plataforma (el proveedor del sistema, rol 6): validación de recargas
    /// de saldo IA de todas las empresas y la clave de IA de la plataforma.
    /// Aprobar o rechazar pide de nuevo la contraseña.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [RequiereSesion(Acceso.Plataforma)]
    public class PlataformaController : ApiControllerBase
    {
        private readonly IPagosIAService _pagosService;
        private readonly IIAService _iaService;
        private readonly IConsumoIAService _consumo;
        private readonly IKushkiPlataformaService _kushki;
        private readonly ILogger<PlataformaController> _logger;

        public PlataformaController(IPagosIAService pagosService, IIAService iaService, IConsumoIAService consumo,
            IKushkiPlataformaService kushki, ILogger<PlataformaController> logger)
        {
            _pagosService = pagosService;
            _iaService = iaService;
            _consumo = consumo;
            _kushki = kushki;
            _logger = logger;
        }

        // ---- Pago con tarjeta (llaves de Kushki de los planes de IA) ----

        [HttpPost("obtenerKushki")]
        public Task<ActionResult> obtenerKushki() =>
            Ejecutar("Plataforma.obtenerKushki", _ => _kushki.obtener(), conObjeto: true);

        /// <summary>Body: {"prueba":true,"llave_publica","llave_privada","secreto_webhook","clave"}</summary>
        [HttpPost("guardarKushki")]
        public Task<ActionResult> guardarKushki() =>
            Ejecutar("Plataforma.guardarKushki", json => _kushki.guardarAsync(json), conObjeto: true);

        [HttpPost("probarKushki")]
        public Task<ActionResult> probarKushki() =>
            Ejecutar("Plataforma.probarKushki", _ => _kushki.probarAsync(), conObjeto: true);

        /// <summary>Body: {"clave"}</summary>
        [HttpPost("eliminarKushki")]
        public Task<ActionResult> eliminarKushki() =>
            Ejecutar("Plataforma.eliminarKushki", json => _kushki.eliminarAsync(json), conObjeto: true);

        protected override ILogger Logger => _logger;

        /// <summary>Body: {"estado":"pendiente"|"aprobada"|"rechazada"|""}</summary>
        [HttpPost("listarRecargas")]
        public Task<ActionResult> listarRecargas() =>
            Ejecutar("Plataforma.listarRecargas", json => _pagosService.listarRecargasPlataforma(json), conObjeto: true);

        /// <summary>Body: {"reg_id":1}</summary>
        [HttpPost("descargarComprobante")]
        public Task<ActionResult> descargarComprobante() =>
            Ejecutar("Plataforma.descargarComprobante", json => _pagosService.descargarComprobanteAsync(json), conObjeto: true);

        /// <summary>Body: {"reg_id":1,"monto_aprobado":100,"clave":"..."}</summary>
        [HttpPost("aprobarRecarga")]
        public Task<ActionResult> aprobarRecarga() =>
            Ejecutar("Plataforma.aprobarRecarga", json => _pagosService.aprobarRecarga(json), conObjeto: false);

        /// <summary>Body: {"reg_id":1,"motivo":"...","clave":"..."}</summary>
        [HttpPost("rechazarRecarga")]
        public Task<ActionResult> rechazarRecarga() =>
            Ejecutar("Plataforma.rechazarRecarga", json => _pagosService.rechazarRecarga(json), conObjeto: false);

        [HttpPost("obtenerConexion")]
        public Task<ActionResult> obtenerConexion() =>
            Ejecutar("Plataforma.obtenerConexion", _ => _iaService.obtenerConexion(plataforma: true), conObjeto: true);

        [HttpPost("guardarConexion")]
        public Task<ActionResult> guardarConexion() =>
            Ejecutar("Plataforma.guardarConexion", json => _iaService.guardarConexionAsync(json, plataforma: true), conObjeto: true);

        [HttpPost("probarConexion")]
        public Task<ActionResult> probarConexion() =>
            Ejecutar("Plataforma.probarConexion", _ => _iaService.probarConexionAsync(plataforma: true), conObjeto: true);

        [HttpPost("eliminarConexion")]
        public Task<ActionResult> eliminarConexion() =>
            Ejecutar("Plataforma.eliminarConexion", _ => _iaService.eliminarConexionAsync(plataforma: true), conObjeto: true);

        /// <summary>Consumo pagado con saldo por todas las empresas. Body: {"desde":"yyyy-MM-dd","hasta":"yyyy-MM-dd"}.</summary>
        [HttpPost("resumenConsumo")]
        public Task<ActionResult> resumenConsumo() =>
            Ejecutar("Plataforma.resumenConsumo", json => _consumo.resumen(json, plataforma: true), conObjeto: true);
    }
}
