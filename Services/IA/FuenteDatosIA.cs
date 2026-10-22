using System.Globalization;
using esupplier.Models.Response;
using esupplier.Services.Auth;
using esupplier.Services.IServices;
using esupplier.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IA
{
    /// <summary>
    /// Datos de solo lectura para los agentes IA, siempre con los permisos de quien pregunta:
    /// si es un usuario de proveedor, el código de proveedor sale de su identidad y no de lo que
    /// pida el agente o el navegador (lo mismo que hace [ProveedorDelToken] en los controladores).
    /// Los resultados se reducen a los campos útiles para no gastar tokens.
    /// </summary>
    public class FuenteDatosIA
    {
        private readonly IOCService _oc;
        private readonly IHESService _hes;
        private readonly IComprobanteService _comprobantes;
        private readonly IProveedorService _proveedores;
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _configuration;
        private readonly IdentidadSolicitud _usuario;

        public FuenteDatosIA(IOCService oc, IHESService hes, IComprobanteService comprobantes, IProveedorService proveedores,
            IHttpClientFactory http, IConfiguration configuration, IdentidadSolicitud usuario)
        {
            _oc = oc;
            _hes = hes;
            _comprobantes = comprobantes;
            _proveedores = proveedores;
            _http = http;
            _configuration = configuration;
            _usuario = usuario;
        }

        public static DateTime Hoy => FechaUtil.AhoraEcuador().Date;
        private static string F(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>Proveedor efectivo: el del usuario si es proveedor; si es interno, el pedido (puede ser vacío = todos).</summary>
        public string Proveedor(string pedido) =>
            _usuario.EsProveedor ? _usuario.CodigoProveedor : (pedido ?? "").Trim();

        // ---------- Órdenes de compra ----------

        private static readonly string[] CamposCabecera =
        {
            "numero_oc", "tipo_oc", "estado_oc", "estado_confirmacion", "observacion_confirmacion", "fecha_emision",
            "fecha_etd", "fecha_recibido_bodega", "codigo_proveedor", "denominacion_fiscal", "moneda", "forma_pago",
            "importe_total", "impuesto_general", "importe_final", "fecha_confirmacion"
        };

        private static readonly string[] CamposDetalle =
        {
            "posicion_oc", "cod_material", "des_material", "um_material", "cantidad", "cantidad_entregada",
            "cantidad_por_entregar", "precio", "total", "simbolo_moneda", "fecha_entrega", "fecha_entrega_estadistica",
            "estado_posicion_oc"
        };

        /// <summary>Órdenes de compra (cabeceras) del rango. estado: "", "Abierto" o "Cerrado".</summary>
        public async Task<JArray> OrdenesAsync(DateTime desde, DateTime hasta, string estado = "", string numeroOc = "", string proveedor = "")
        {
            JObject datos = await ConsultarOC("B", desde, hasta, estado, numeroOc, Proveedor(proveedor));
            var cabeceras = (datos["cabecera"] as JArray ?? new JArray())
                .Where(c => !new[] { "Anulado", "Bloqueado", "Retenido" }.Contains((string)c["estado_oc"]));
            return Proyectar(new JArray(cabeceras), CamposCabecera, 80);
        }

        /// <summary>Cabecera y posiciones de una OC (null si no existe o no es del proveedor del usuario).</summary>
        public async Task<JObject> DetalleOrdenAsync(string numeroOc)
        {
            if (string.IsNullOrWhiteSpace(numeroOc)) throw new ArgumentException("Indique el número de OC.");
            JObject datos = await ConsultarOC("O", Hoy.AddYears(-10), Hoy, "", numeroOc.Trim(), Proveedor(""));
            var cabecera = (datos["cabecera"] as JArray)?.FirstOrDefault();
            if (cabecera == null) return null;
            var detalle = (datos["detalle"] as JArray ?? new JArray())
                .Where(d => !new[] { "Anulado", "Bloqueado" }.Contains((string)d["estado_posicion_oc"]))
                .OrderBy(d => (string)d["posicion_oc"]);
            return new JObject
            {
                ["cabecera"] = Proyectar(new JArray(cabecera), CamposCabecera, 1)[0],
                ["posiciones"] = Proyectar(new JArray(detalle), CamposDetalle, 200)
            };
        }

        /// <summary>Versiones de una OC. Para un proveedor, primero comprueba que la OC sea suya.</summary>
        public async Task<JObject> HistorialOrdenAsync(string numeroOc)
        {
            if (await DetalleOrdenAsync(numeroOc) == null) return null;
            var cuerpo = new JObject
            {
                ["operador"] = "H", ["adm_cliente_id"] = _usuario.Cliente, ["sociedad"] = _usuario.Sociedad,
                ["numero_oc"] = numeroOc.Trim(), ["estado_confirmacion"] = ""
            };
            JObject datos = Datos(await _oc.obtenerOCHistorico(cuerpo));
            string[] camposVersion = CamposCabecera.Append("reg_version").Append("reg_fecha_creacion").ToArray();
            string[] camposDetalleVersion = CamposDetalle.Append("reg_version").ToArray();
            return new JObject
            {
                ["versiones"] = Proyectar(datos["cabecera"] as JArray ?? new JArray(), camposVersion, 30),
                ["posiciones"] = Proyectar(datos["detalle"] as JArray ?? new JArray(), camposDetalleVersion, 400)
            };
        }

        private async Task<JObject> ConsultarOC(string operador, DateTime desde, DateTime hasta, string estado, string numeroOc, string proveedor)
        {
            var cuerpo = new JObject
            {
                ["operador"] = operador, ["adm_cliente_id"] = _usuario.Cliente, ["sociedad"] = _usuario.Sociedad,
                ["numero_oc"] = numeroOc ?? "", ["fecha_emision_d"] = F(desde), ["fecha_emision_h"] = F(hasta),
                ["estado_oc"] = estado ?? "", ["codigo_proveedor"] = proveedor ?? "", ["identificacion_fiscal"] = "",
                ["denominacion_fiscal"] = "", ["tipo_oc"] = "", ["estado_confirmacion"] = ""
            };
            return Datos(await _oc.obtenerOC(cuerpo));
        }

        // ---------- HES / MIGO ----------

        public async Task<JArray> HesAsync(DateTime desde, DateTime hasta, string numeroOc = "", string proveedor = "")
        {
            var cuerpo = new JObject
            {
                ["I_INDICADOR"] = "1", ["NRO_OC"] = numeroOc ?? "", ["NRO_HESS"] = "", ["ESTADO"] = "",
                ["FEC_EMI_DESDE"] = F(desde), ["FEC_EMI_HASTA"] = F(hasta), ["I_SOCIEDAD"] = _usuario.Sociedad,
                ["I_PROVEEDOR"] = Proveedor(proveedor), ["PAGESIZE"] = 50, ["PAGENUMBER"] = 1
            };
            JObject datos = Datos(await _hes.buscarHES(cuerpo));
            return Proyectar(datos["ET_MHCAB"] as JArray ?? new JArray(), new[]
            {
                "NRO_HESS", "NRO_OC", "ESTADO", "RAZON_SOCIAL_P", "TIPO_SERVICIO", "AREA_ATENCION", "FEC_EMI_HESS",
                "FEC_ACEPTACION", "FEC_INI_SERVICIO", "FEC_FIN_SERVICIO", "OBSERVACION", "TOTAL"
            }, 50);
        }

        // ---------- Estado de cuenta ----------

        /// <summary>pagados = documentos pagados; si no, documentos por pagar.</summary>
        public async Task<JArray> ComprobantesAsync(bool pagados, DateTime desde, DateTime hasta, string proveedor = "")
        {
            string codigo = Proveedor(proveedor);
            var cuerpo = new JObject
            {
                ["I_INDICADOR"] = pagados ? "1" : "2", ["I_NRO_BOLETA"] = "", ["I_SOCIEDAD"] = _usuario.Sociedad,
                ["I_PROVEEDOR"] = codigo == "" ? "1" : codigo, ["I_FECINI"] = F(desde), ["I_FECFIN"] = F(hasta)
            };
            JObject datos = Datos(await _comprobantes.buscarComprobantes(cuerpo));
            return Proyectar(datos["ET_DOCPAGO"] as JArray ?? new JArray(), new[]
            {
                "COD_PROVEEDOR", "NRO_FACTURA", "NRO_DOC", "CLASE_DOC", "FEC_DOC", "FEC_BASE", "COND_PAGO", "FEC_VENCIMIENTO",
                "FEC_PAGO", "FEC_COMPENSACION", "TIPO_PAGO", "VIA_PAGO", "IMPORTE", "MONEDA"
            }, 150);
        }

        // ---------- Calificación ----------

        /// <summary>general, calificacion, calidad y otif del proveedor (anio y meses opcionales).</summary>
        public JObject Calificacion(string proveedor, string anio = "", IEnumerable<string> meses = null)
        {
            string codigo = Proveedor(proveedor);
            if (codigo == "") throw new ArgumentException("Indique el proveedor.");
            string mes = meses == null ? null : string.Join(",", meses.Where(m => !string.IsNullOrWhiteSpace(m)));
            var datos = new JObject();
            foreach (var (tipo, nombre) in new[] { (1, "general"), (2, "calificacion"), (3, "calidad"), (4, "otif") })
            {
                Mensaje msj = _proveedores.obtenerCalificacion(tipo, codigo, string.IsNullOrWhiteSpace(anio) ? null : anio,
                    string.IsNullOrWhiteSpace(mes) ? null : mes, null, null);
                if (msj == null || msj.tipo != "S") throw new InvalidOperationException(msj?.mensaje ?? "No se pudo consultar la calificación.");
                datos[nombre] = string.IsNullOrWhiteSpace(msj.data) ? new JArray() : JToken.Parse(msj.data);
            }
            return datos;
        }

        // ---------- Proyección de materiales ----------

        public async Task<JArray> ProyeccionAsync(string proveedor)
        {
            string codigo = Proveedor(proveedor);
            if (codigo == "") throw new ArgumentException("Indique el proveedor.");
            var cuerpo = new JObject
            {
                ["I_BUKRS"] = _usuario.Sociedad, ["I_EKORG"] = _usuario.Sociedad, ["I_FECINI"] = F(Hoy),
                ["I_LIFNR"] = codigo.PadLeft(10, '0')
            };
            string respuesta = await ConsultaUtil.PostSapAsync(_http.CreateClient(Constantes.HTTP_CLIENT_SAP),
                _configuration["Http:consultarProyeccionMateriales"], cuerpo.ToString(Formatting.None));
            if (string.IsNullOrWhiteSpace(respuesta)) return new JArray();
            JToken items = JObject.Parse(respuesta)["ES_DATA"]?["item"];
            return Proyectar(items as JArray ?? (items is JObject o ? new JArray(o) : new JArray()),
                new[] { "MATNR", "TXZ01", "MEINS", "PRECIO", "WAERS", "MES01", "MES02", "MES03", "MES04", "MES05", "MES06" }, 150);
        }

        // ---------- utilidades ----------

        /// <summary>data de un Mensaje ("clave":[...] o JSON completo) como JObject; lanza si el servicio falló.</summary>
        private static JObject Datos(Mensaje msj)
        {
            if (msj == null || msj.tipo != "S") throw new InvalidOperationException(msj?.mensaje ?? "No se pudo consultar la información.");
            string data = (msj.data ?? "").Trim();
            if (data == "") return new JObject();
            return data.StartsWith("{") ? JObject.Parse(data) : JObject.Parse("{" + data + "}");
        }

        /// <summary>Solo los campos indicados (los vacíos y fechas 0000-00-00 se omiten), hasta <paramref name="maximo"/> filas.</summary>
        public static JArray Proyectar(JArray filas, string[] campos, int maximo)
        {
            var salida = new JArray();
            foreach (JObject fila in filas.OfType<JObject>().Take(maximo))
            {
                var item = new JObject();
                foreach (string campo in campos)
                {
                    JToken valor = fila[campo];
                    if (valor == null || valor.Type == JTokenType.Null) continue;
                    string texto = valor.ToString().Trim();
                    if (texto == "" || texto.StartsWith("0000-00-00")) continue;
                    item[campo] = valor;
                }
                salida.Add(item);
            }
            return salida;
        }
    }
}
