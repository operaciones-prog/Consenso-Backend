using esupplier.Models.IA;
using System.Data;

namespace esupplier.Repositorys.IRepositorys
{
    /// <summary>Planes mensuales de IA pagados con tarjeta (Kushki): planes, suscripción y cobros.</summary>
    public interface IPlanIARepository
    {
        ConfigPlanIA obtenerConfig();
        List<PlanIA> listarPlanes(bool soloActivos);

        /// <summary>La suscripción de la sociedad, o null.</summary>
        SuscripcionIA obtenerSuscripcion(string admClienteId, string codigoSociedad);
        /// <summary>La suscripción con ese id de Kushki (webhook), o null.</summary>
        SuscripcionIA obtenerSuscripcionKushki(string kushkiSuscripcionId);

        /// <summary>Tarjeta registrada en Kushki. Lanza 50901 si la sociedad ya tiene una suscripción viva.</summary>
        void registrarSuscripcion(SuscripcionIA suscripcion, string usuario);

        /// <summary>accion: programarPlan | cambiarPlan | cancelar | baja | tarjeta (ver sp_actualizarSuscripcionIA).</summary>
        void actualizarSuscripcion(string admClienteId, string codigoSociedad, string accion, string codigoPlan,
            string tarjetaMarca, string tarjetaUltimos4, string usuario);

        /// <summary>Cobro aprobado: abre el periodo y repone el saldo del plan. false si ya se había procesado.</summary>
        bool acreditarCobro(string kushkiSuscripcionId, string ticket, string eventoId, DateTime fechaCobro,
            decimal subtotal, decimal iva, decimal total, string usuario);

        /// <summary>Cobro rechazado. ultimo = Kushki agotó los reintentos y dio de baja la suscripción.</summary>
        void registrarCobroFallido(string kushkiSuscripcionId, string eventoId, decimal subtotal, decimal iva, decimal total,
            string mensaje, bool ultimo, string usuario);

        /// <summary>admClienteId null = todas las empresas.</summary>
        DataTable listarPagos(string admClienteId, string codigoSociedad);

        /// <summary>Llaves de Kushki de la plataforma (config null si no hay) y suscripciones vivas en Kushki.</summary>
        (ConfigKushkiIA config, int suscripcionesVivas) obtenerConfigKushki();
        /// <summary>Crea o reemplaza la configuración y la deja verificada.</summary>
        void guardarConfigKushki(ConfigKushkiIA config, string usuario);
        /// <summary>estado: verificada | error.</summary>
        void actualizarEstadoConfigKushki(string estado);
        /// <summary>Devuelve la referencia del secreto que había (null si no había configuración).</summary>
        string eliminarConfigKushki();
    }
}
