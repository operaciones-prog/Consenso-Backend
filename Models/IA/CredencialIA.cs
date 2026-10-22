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

    public static class EstadoCredencialIA
    {
        public const string Verificada = "verificada";
        public const string Error = "error";
    }
}
