namespace esupplier.Models.IA
{
    /// <summary>Fila de tb_ia_credencial. Nunca contiene la API key, solo dónde está guardada.</summary>
    public class CredencialIA
    {
        public string proveedor { get; set; }
        public string modelo { get; set; }
        public string secreto_ref { get; set; }
        public string sufijo { get; set; }
        public string estado { get; set; }
        public DateTime? fecha_verificacion { get; set; }
        public decimal? limite_mensual_usd { get; set; }
        public string modificado_por { get; set; }
        public DateTime? fecha_modificacion { get; set; }
        public decimal consumo_mes_usd { get; set; }
        public int consultas_mes { get; set; }
    }

    /// <summary>Usuario activo devuelto por sp_obtenerUsuarioIA.</summary>
    public class UsuarioIA
    {
        public string usuario { get; set; }
        public string rol_id { get; set; }
        public string codigo_referencia { get; set; }
    }

    public class SaldoIA
    {
        /// <summary>Total disponible: el del plan vigente + el recargado.</summary>
        public decimal saldo_usd { get; set; }
        /// <summary>Saldo del plan del periodo actual (0 si no hay plan vigente). Se usa primero.</summary>
        public decimal saldo_plan_usd { get; set; }
        /// <summary>Recargado con comprobante: no vence.</summary>
        public decimal saldo_recarga_usd { get; set; }
        public int recargas_pendientes { get; set; }
    }

    /// <summary>Recarga de saldo IA: comprobante subido por el cliente y su validación.</summary>
    public class RecargaIA
    {
        public int reg_id { get; set; }
        public string adm_cliente_id { get; set; }
        public string codigo_sociedad { get; set; }
        public decimal monto_usd { get; set; }
        public string banco { get; set; }
        public string referencia { get; set; }
        public DateTime fecha_pago { get; set; }
        public string archivo_ruta { get; set; }
        public string archivo_nombre { get; set; }
        public string estado { get; set; }
    }

    public static class EstadoRecargaIA
    {
        public const string Pendiente = "pendiente";
        public const string Aprobada = "aprobada";
        public const string Rechazada = "rechazada";
    }

    public static class EstadoCredencialIA
    {
        public const string Verificada = "verificada";
        public const string Error = "error";
    }
}
