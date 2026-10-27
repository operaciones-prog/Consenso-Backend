namespace esupplier.Models.IA
{
    /// <summary>Fila de tb_ia_plan. Precio mensual SIN IVA; saldo_mes_usd es el saldo de IA que da cada mes.</summary>
    public class PlanIA
    {
        public string codigo_plan { get; set; }
        public string nombre { get; set; }
        public string nombre_en { get; set; }
        public string descripcion { get; set; }
        public string descripcion_en { get; set; }
        public decimal precio_mensual_usd { get; set; }
        public decimal saldo_mes_usd { get; set; }
        public bool recomendado { get; set; }
        public bool activo { get; set; }
        public int orden { get; set; }
    }

    /// <summary>tb_ia_config_plan.</summary>
    public class ConfigPlanIA
    {
        public decimal iva_pct { get; set; }
        public int dias_gracia { get; set; }
    }

    /// <summary>
    /// Fila de tb_ia_suscripcion. Nunca contiene datos de la tarjeta: solo el id de la suscripción
    /// en Kushki, la marca y los últimos 4 dígitos.
    /// </summary>
    public class SuscripcionIA
    {
        public string adm_cliente_id { get; set; }
        public string codigo_sociedad { get; set; }
        public string codigo_plan { get; set; }
        public string codigo_plan_siguiente { get; set; }
        /// <summary>null = esperando el primer cobro.</summary>
        public DateTime? inicio_periodo { get; set; }
        public DateTime? fin_periodo { get; set; }
        public bool cancelar_al_final { get; set; }
        /// <summary>Cuándo cobrará Kushki (null = no cobrará).</summary>
        public DateTime? proximo_cobro { get; set; }
        /// <summary>null = Kushki ya no cobrará (cancelada o sin cobro posible).</summary>
        public string kushki_suscripcion_id { get; set; }
        public string tarjeta_marca { get; set; }
        public string tarjeta_ultimos4 { get; set; }
        public string email { get; set; }
        public string nombres { get; set; }
        public string apellidos { get; set; }
        public string tipo_documento { get; set; }
        public string numero_documento { get; set; }
        public string telefono { get; set; }
        public int intentos_fallidos { get; set; }
        public string ultimo_error { get; set; }
        /// <summary>El plan cuenta hoy (periodo, o días de gracia mientras Kushki reintenta).</summary>
        public bool vigente { get; set; }
    }

    /// <summary>Datos del pagador que Kushki exige para la suscripción.</summary>
    public class ContactoPlanIA
    {
        public string nombres { get; set; }
        public string apellidos { get; set; }
        public string email { get; set; }
        /// <summary>CI, RUC o PAS.</summary>
        public string tipo_documento { get; set; }
        public string numero_documento { get; set; }
        /// <summary>E.164 (+593...).</summary>
        public string telefono { get; set; }
    }

    /// <summary>
    /// Llaves de Kushki que configura el administrador de plataforma (tb_ia_config_kushki). La llave privada
    /// y el id de firma de webhooks están en AWS Secrets Manager (secreto_ref); aquí solo sus últimos 4.
    /// </summary>
    public class ConfigKushkiIA
    {
        public bool prueba { get; set; }
        public string llave_publica { get; set; }
        public string secreto_ref { get; set; }
        public string sufijo_privada { get; set; }
        public string sufijo_webhook { get; set; }
        /// <summary>verificada | error</summary>
        public string estado { get; set; }
        public DateTime? fecha_verificacion { get; set; }
        public DateTime? fecha_modificacion { get; set; }
        public string modificado_por { get; set; }
    }

    /// <summary>Estado de la suscripción que ve la empresa.</summary>
    public static class EstadoSuscripcionIA
    {
        /// <summary>Sin plan.</summary>
        public const string Ninguna = "ninguna";
        /// <summary>Tarjeta registrada, esperando el primer cobro de Kushki.</summary>
        public const string Pendiente = "pendiente";
        public const string Activa = "activa";
        /// <summary>Terminó el periodo y Kushki está reintentando el cobro: el plan sigue funcionando.</summary>
        public const string Gracia = "gracia";
        /// <summary>Cancelada por el cliente: sigue activa hasta fin_periodo.</summary>
        public const string Cancelada = "cancelada";
        /// <summary>Terminó sin renovación.</summary>
        public const string Vencida = "vencida";

        public static string De(SuscripcionIA s, DateTime hoy)
        {
            if (s == null) return Ninguna;
            if (s.inicio_periodo == null)
            {
                return s.kushki_suscripcion_id != null ? Pendiente : Ninguna;
            }
            if (hoy.Date <= s.fin_periodo.Value.Date)
            {
                return s.cancelar_al_final || s.kushki_suscripcion_id == null ? Cancelada : Activa;
            }
            return s.vigente ? Gracia : Vencida;
        }
    }
}
