namespace esupplier.Models.Response
{
    public class OrdenCompraResponse
    {
        public string? adm_cliente_id { get; set; }
        public string? numero_oc { get; set; }
        public string? tipo_oc { get; set; }
        public string? fecha_emision { get; set; }
        public string? fecha_etd { get; set; }
        public string? fecha_produccion { get; set; }
        public string? fecha_recibido_bodega { get; set; }
        public string? estado_oc { get; set; }
        public string? codigo_proveedor { get; set; }
        public string? identificacion_fiscal { get; set; }
        public string? denominacion_fiscal { get; set; }
        public string? moneda { get; set; }
        public string? forma_pago { get; set; }
        public string? numero_oferta { get; set; }
        public string? importe_total { get; set; }
        public string? impuesto_general { get; set; }
        public string? importe_final { get; set; }
        public int? reg_estado { get; set; }
        public int? reg_id { get; set; }
        public string? estado_confirmacion { get; set; }
        public string? observacion_confirmacion { get; set; }
        public string? archivo { get; set; }
        public string? fecha_confirmacion { get; set; }

    }
}
