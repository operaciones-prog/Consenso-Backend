using System.Data;
using System.Globalization;
using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.Auth;
using esupplier.Services.IServices;
using esupplier.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.Services.IA
{
    public interface IConsumoIAService
    {
        /// <summary>
        /// Resumen de consumo (tokens y costo). Body: {"desde":"yyyy-MM-dd","hasta":"yyyy-MM-dd"} (por defecto, el mes actual).
        /// Empresa: todo su consumo. Plataforma: lo pagado con saldo por todas las empresas.
        /// </summary>
        Mensaje resumen(JObject jObject, bool plataforma);

        /// <summary>
        /// Historial: una fila por consulta con lo consultado y su costo, paginado.
        /// Body: {"desde","hasta" (yyyy-MM-dd),"pagina":1,"tamanio":15,"buscar":"texto"}.
        /// El administrador de la empresa ve toda su sociedad; cualquier otro usuario, solo sus consultas.
        /// </summary>
        Mensaje historial(JObject jObject);
    }

    public class ConsumoIAService : IConsumoIAService
    {
        private const int MaxDias = 366;

        private readonly IIARepository _repositorio;
        private readonly IdentidadSolicitud _usuario;
        private readonly IHeaderService _header;

        public ConsumoIAService(IIARepository repositorio, IdentidadSolicitud usuario, IHeaderService header)
        {
            _repositorio = repositorio;
            _usuario = usuario;
            _header = header;
        }

        public Mensaje resumen(JObject jObject, bool plataforma)
        {
            DateTime hoy = FechaUtil.AhoraEcuador().Date;
            DateTime desde = Fecha(jObject, "desde") ?? new DateTime(hoy.Year, hoy.Month, 1);
            DateTime hasta = Fecha(jObject, "hasta") ?? hoy;
            if (hasta < desde) (desde, hasta) = (hasta, desde);
            if ((hasta - desde).TotalDays > MaxDias) return new Mensaje("E", "El periodo puede ser de hasta un año.", "");

            DataTable filas = plataforma
                ? _repositorio.listarUso(null, null, desde, hasta, soloSaldo: true)
                : _repositorio.listarUso(_usuario.Cliente, _usuario.Sociedad, desde, hasta, soloSaldo: false);
            bool ingles = _header.obtenerIdioma() == "EN";
            var datos = filas.AsEnumerable().Select(r => new Fila(r, ingles)).ToList();

            var resultado = new JObject
            {
                ["periodo"] = new JObject { ["desde"] = desde.ToString("yyyy-MM-dd"), ["hasta"] = hasta.ToString("yyyy-MM-dd") },
                ["totales"] = Totales(datos),
                ["por_complemento"] = Agrupar(datos, f => f.CodigoAddon, f => new JObject { ["codigo"] = f.CodigoAddon, ["nombre"] = f.NombreAddon }),
                ["por_modelo"] = Agrupar(datos, f => f.Modelo, f => new JObject { ["modelo"] = f.Modelo }),
                ["por_dia"] = new JArray(datos.GroupBy(f => f.Dia).OrderBy(g => g.Key).Select(g =>
                {
                    var o = Totales(g);
                    o.AddFirst(new JProperty("dia", g.Key.ToString("yyyy-MM-dd")));
                    return o;
                })),
                ["precios"] = JToken.FromObject(PreciosIA.Tabla())
            };
            if (plataforma)
            {
                resultado["por_empresa"] = Agrupar(datos, f => f.Cliente + "|" + f.Sociedad,
                    f => new JObject { ["codigo_sociedad"] = f.Sociedad, ["razon_social"] = f.RazonSocial });
            }
            else
            {
                resultado["por_usuario"] = Agrupar(datos, f => f.Usuario, f => new JObject { ["usuario"] = f.Usuario });
            }

            return new Mensaje("S", "", "\"consumo\":" + resultado.ToString(Formatting.None));
        }

        public Mensaje historial(JObject jObject)
        {
            DateTime hoy = FechaUtil.AhoraEcuador().Date;
            DateTime desde = Fecha(jObject, "desde") ?? new DateTime(hoy.Year, hoy.Month, 1);
            DateTime hasta = Fecha(jObject, "hasta") ?? hoy;
            if (hasta < desde) (desde, hasta) = (hasta, desde);
            if ((hasta - desde).TotalDays > MaxDias) return new Mensaje("E", "El periodo puede ser de hasta un año.", "");

            int pagina = Math.Max(1, Entero(jObject, "pagina", 1));
            int tamanio = Math.Clamp(Entero(jObject, "tamanio", 15), 1, 100);
            string buscar = JsonUtil.Opcional(jObject, "buscar")?.Trim() ?? "";
            if (buscar.Length > 100) buscar = buscar[..100];

            // Administrador interno (rol 1 o 2): toda la sociedad. El resto, solo lo suyo.
            bool administrador = _usuario.EsInterno && (_usuario.RolId == "1" || _usuario.RolId == "2");
            DataTable filas = _repositorio.listarHistorial(_usuario.Cliente, _usuario.Sociedad,
                administrador ? null : _usuario.Usuario, desde, hasta, buscar, pagina, tamanio);

            bool ingles = _header.obtenerIdioma() == "EN";
            var consultas = new JArray(filas.AsEnumerable().Select(r =>
            {
                string codigo = r["codigo_addon"] as string ?? "";
                return new JObject
                {
                    ["reg_id"] = Convert.ToInt64(r["reg_id"], CultureInfo.InvariantCulture),
                    ["fecha"] = Convert.ToDateTime(r["fecha"], CultureInfo.InvariantCulture).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                    ["usuario"] = r["usuario"] as string ?? "",
                    ["codigo"] = codigo,
                    ["complemento"] = (ingles ? r["nombre_addon_en"] : r["nombre_addon"]) as string ?? codigo,
                    ["pregunta"] = r["pregunta"] as string ?? "",
                    ["modelo"] = r["modelo"] as string ?? "",
                    ["tokens_entrada"] = Convert.ToInt64(r["tokens_entrada"], CultureInfo.InvariantCulture),
                    ["tokens_salida"] = Convert.ToInt64(r["tokens_salida"], CultureInfo.InvariantCulture),
                    ["costo_usd"] = Convert.ToDecimal(r["costo_usd"], CultureInfo.InvariantCulture),
                    ["con_saldo"] = r["con_saldo"] is bool b && b
                };
            }));

            // total y costo vienen repetidos en cada fila (calculados sobre todo el filtro). Una página
            // fuera de rango no trae filas: se informa 0 y el frontend vuelve a la primera.
            DataRow primera = filas.Rows.Count > 0 ? filas.Rows[0] : null;
            long total = primera == null ? 0 : Convert.ToInt64(primera["total"], CultureInfo.InvariantCulture);
            decimal costoTotal = primera == null || primera["costo_total_usd"] == DBNull.Value
                ? 0 : Math.Round(Convert.ToDecimal(primera["costo_total_usd"], CultureInfo.InvariantCulture), 6);

            return new Mensaje("S", "",
                "\"consultas\":" + consultas.ToString(Formatting.None)
                + ",\"total\":" + total.ToString(CultureInfo.InvariantCulture)
                + ",\"costo_total_usd\":" + costoTotal.ToString(CultureInfo.InvariantCulture));
        }

        private static int Entero(JObject j, string campo, int defecto) =>
            int.TryParse(JsonUtil.Opcional(j, campo), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : defecto;

        private static JObject Totales(IEnumerable<Fila> filas)
        {
            var lista = filas.ToList();
            return new JObject
            {
                ["consultas"] = lista.Sum(f => f.Consultas),
                ["tokens_entrada"] = lista.Sum(f => f.TokensEntrada),
                ["tokens_salida"] = lista.Sum(f => f.TokensSalida),
                ["costo_usd"] = Math.Round(lista.Sum(f => f.Costo), 6),
                ["costo_con_saldo"] = Math.Round(lista.Where(f => f.ConSaldo).Sum(f => f.Costo), 6),
                ["costo_clave_propia"] = Math.Round(lista.Where(f => !f.ConSaldo).Sum(f => f.Costo), 6)
            };
        }

        /// <summary>Agrupa y ordena de mayor a menor costo: identificación del grupo + totales.</summary>
        private static JArray Agrupar(List<Fila> filas, Func<Fila, string> clave, Func<Fila, JObject> identificacion) =>
            new JArray(filas.GroupBy(clave)
                .Select(g =>
                {
                    JObject o = identificacion(g.First());
                    o.Merge(Totales(g));
                    return o;
                })
                .OrderByDescending(o => (decimal)o["costo_usd"]));

        private static DateTime? Fecha(JObject j, string campo) =>
            DateTime.TryParseExact(JsonUtil.Opcional(j, campo), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime f)
                ? f : null;

        private sealed class Fila
        {
            public Fila(DataRow r, bool ingles)
            {
                Cliente = r["adm_cliente_id"] as string ?? "";
                Sociedad = r["codigo_sociedad"] as string ?? "";
                RazonSocial = r["razon_social"] as string ?? Sociedad;
                Usuario = r["usuario"] as string ?? "";
                CodigoAddon = r["codigo_addon"] as string ?? "";
                NombreAddon = (ingles ? r["nombre_addon_en"] : r["nombre_addon"]) as string ?? CodigoAddon;
                Modelo = r["modelo"] as string ?? "";
                ConSaldo = r["con_saldo"] is bool b && b;
                Dia = Convert.ToDateTime(r["dia"], CultureInfo.InvariantCulture);
                Consultas = Convert.ToInt32(r["consultas"], CultureInfo.InvariantCulture);
                TokensEntrada = Convert.ToInt64(r["tokens_entrada"], CultureInfo.InvariantCulture);
                TokensSalida = Convert.ToInt64(r["tokens_salida"], CultureInfo.InvariantCulture);
                Costo = Convert.ToDecimal(r["costo_usd"], CultureInfo.InvariantCulture);
            }

            public string Cliente { get; }
            public string Sociedad { get; }
            public string RazonSocial { get; }
            public string Usuario { get; }
            public string CodigoAddon { get; }
            public string NombreAddon { get; }
            public string Modelo { get; }
            public bool ConSaldo { get; }
            public DateTime Dia { get; }
            public int Consultas { get; }
            public long TokensEntrada { get; }
            public long TokensSalida { get; }
            public decimal Costo { get; }
        }
    }
}
