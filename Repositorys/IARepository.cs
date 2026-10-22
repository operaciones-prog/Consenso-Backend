using esupplier.Models.IA;
using esupplier.Repositorys.IRepositorys;
using Microsoft.Data.SqlClient;
using System.Data;

namespace esupplier.Repositorys
{
    public class IARepository : IIARepository
    {
        private readonly IConfiguration _configuration;

        public IARepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection Conexion() => new SqlConnection(_configuration.GetConnectionString("esupplier"));

        public UsuarioIA obtenerUsuarioIA(string admClienteId, string codigoSociedad, int regId, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_obtenerUsuarioIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            bdComando.Parameters.Add(new SqlParameter("@reg_id", regId));
            bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
            using SqlDataReader lector = bdComando.ExecuteReader();
            if (!lector.Read()) return null;
            return new UsuarioIA
            {
                usuario = lector["usuario"] as string,
                rol_id = lector["rol_id"] as string,
                codigo_referencia = Convert.ToString(lector["codigo_referencia"])
            };
        }

        public DataTable listarAddon(string admClienteId, string codigoSociedad, string idioma)
        {
            DataTable dt = new DataTable();
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_listarAddon", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            bdComando.Parameters.Add(new SqlParameter("@idioma", idioma));
            using SqlDataAdapter adapter = new SqlDataAdapter(bdComando);
            adapter.Fill(dt);
            return dt;
        }

        public void mantenerAddonActivacion(string admClienteId, string codigoSociedad, string codigoAddon, bool activo, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_mantenerAddonActivacion", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            bdComando.Parameters.Add(new SqlParameter("@codigo_addon", codigoAddon));
            bdComando.Parameters.Add(new SqlParameter("@activo", activo));
            bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
            bdComando.ExecuteNonQuery();
        }

        public CredencialIA obtenerCredencial(string admClienteId, string codigoSociedad)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_obtenerCredencialIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            using SqlDataReader lector = bdComando.ExecuteReader();
            if (!lector.Read()) return null;

            return new CredencialIA
            {
                proveedor = lector["proveedor"] as string,
                modelo = lector["modelo"] as string,
                secreto_ref = lector["secreto_ref"] as string,
                sufijo = lector["sufijo"] as string,
                estado = lector["estado"] as string,
                fecha_verificacion = lector["fecha_verificacion"] as DateTime?,
                limite_mensual_usd = lector["limite_mensual_usd"] as decimal?,
                modificado_por = lector["modificado_por"] as string,
                fecha_modificacion = lector["fecha_modificacion"] as DateTime?,
                consumo_mes_usd = Convert.ToDecimal(lector["consumo_mes_usd"]),
                consultas_mes = Convert.ToInt32(lector["consultas_mes"])
            };
        }

        public void mantenerCredencial(string admClienteId, string codigoSociedad, CredencialIA credencial, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_mantenerCredencialIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            bdComando.Parameters.Add(new SqlParameter("@proveedor", credencial.proveedor));
            bdComando.Parameters.Add(new SqlParameter("@modelo", credencial.modelo));
            bdComando.Parameters.Add(new SqlParameter("@secreto_ref", credencial.secreto_ref));
            bdComando.Parameters.Add(new SqlParameter("@sufijo", credencial.sufijo));
            bdComando.Parameters.Add(new SqlParameter("@estado", credencial.estado));
            bdComando.Parameters.Add(new SqlParameter("@limite_mensual_usd", SqlDbType.Decimal)
            {
                Precision = 10,
                Scale = 2,
                Value = (object)credencial.limite_mensual_usd ?? DBNull.Value
            });
            bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
            bdComando.ExecuteNonQuery();
        }

        public void actualizarEstadoCredencial(string admClienteId, string codigoSociedad, string estado)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_actualizarEstadoCredencialIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            bdComando.Parameters.Add(new SqlParameter("@estado", estado));
            bdComando.ExecuteNonQuery();
        }

        public string eliminarCredencial(string admClienteId, string codigoSociedad, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_eliminarCredencialIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
            return bdComando.ExecuteScalar() as string;
        }

        public SaldoIA obtenerSaldo(string admClienteId, string codigoSociedad)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_obtenerSaldoIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            using SqlDataReader lector = bdComando.ExecuteReader();
            lector.Read();
            return new SaldoIA
            {
                saldo_usd = Convert.ToDecimal(lector["saldo_usd"]),
                recargas_pendientes = Convert.ToInt32(lector["recargas_pendientes"])
            };
        }

        public int registrarRecarga(RecargaIA recarga, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_registrarRecargaIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", recarga.adm_cliente_id));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", recarga.codigo_sociedad));
            bdComando.Parameters.Add(new SqlParameter("@monto_usd", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = recarga.monto_usd });
            bdComando.Parameters.Add(new SqlParameter("@banco", recarga.banco));
            bdComando.Parameters.Add(new SqlParameter("@referencia", recarga.referencia));
            bdComando.Parameters.Add(new SqlParameter("@fecha_pago", SqlDbType.Date) { Value = recarga.fecha_pago.Date });
            bdComando.Parameters.Add(new SqlParameter("@archivo_ruta", recarga.archivo_ruta));
            bdComando.Parameters.Add(new SqlParameter("@archivo_nombre", recarga.archivo_nombre));
            bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
            return Convert.ToInt32(bdComando.ExecuteScalar());
        }

        public DataTable listarRecargas(string admClienteId, string codigoSociedad, string estado)
        {
            DataTable dt = new DataTable();
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_listarRecargaIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", (object)admClienteId ?? DBNull.Value));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", (object)codigoSociedad ?? DBNull.Value));
            bdComando.Parameters.Add(new SqlParameter("@estado", (object)estado ?? DBNull.Value));
            using SqlDataAdapter adapter = new SqlDataAdapter(bdComando);
            adapter.Fill(dt);
            return dt;
        }

        public RecargaIA obtenerRecarga(int regId)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_obtenerRecargaIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@reg_id", regId));
            using SqlDataReader lector = bdComando.ExecuteReader();
            if (!lector.Read()) return null;
            return new RecargaIA
            {
                reg_id = Convert.ToInt32(lector["reg_id"]),
                adm_cliente_id = lector["adm_cliente_id"] as string,
                codigo_sociedad = lector["codigo_sociedad"] as string,
                monto_usd = Convert.ToDecimal(lector["monto_usd"]),
                estado = lector["estado"] as string,
                archivo_ruta = lector["archivo_ruta"] as string,
                archivo_nombre = lector["archivo_nombre"] as string
            };
        }

        public void aprobarRecarga(int regId, decimal montoAprobado, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_aprobarRecargaIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@reg_id", regId));
            bdComando.Parameters.Add(new SqlParameter("@monto_aprobado", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = montoAprobado });
            bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
            bdComando.ExecuteNonQuery();
        }

        public void rechazarRecarga(int regId, string motivo, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_rechazarRecargaIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@reg_id", regId));
            bdComando.Parameters.Add(new SqlParameter("@motivo", motivo));
            bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
            bdComando.ExecuteNonQuery();
        }

        public List<string> obtenerClavesUsuario(string admClienteId, string codigoSociedad, string usuario)
        {
            var claves = new List<string>();
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_obtenerCredencialUsuario", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add("@adm_cliente_id", SqlDbType.VarChar, 12).Value = admClienteId;
            bdComando.Parameters.Add("@codigo_sociedad", SqlDbType.VarChar, 18).Value = codigoSociedad;
            bdComando.Parameters.Add("@usuario", SqlDbType.VarChar, 50).Value = usuario;
            using SqlDataReader lector = bdComando.ExecuteReader();
            while (lector.Read())
            {
                claves.Add(lector["clave"] == DBNull.Value ? "" : lector["clave"].ToString());
            }
            return claves;
        }

        public void registrarUso(string admClienteId, string codigoSociedad, string usuario, string codigoAddon, string modelo,
            int tokensEntrada, int tokensSalida, decimal costoUsd, bool conSaldo)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_registrarUsoIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigoSociedad));
            bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
            bdComando.Parameters.Add(new SqlParameter("@codigo_addon", codigoAddon));
            bdComando.Parameters.Add(new SqlParameter("@modelo", modelo));
            bdComando.Parameters.Add(new SqlParameter("@tokens_entrada", tokensEntrada));
            bdComando.Parameters.Add(new SqlParameter("@tokens_salida", tokensSalida));
            bdComando.Parameters.Add(new SqlParameter("@costo_usd", SqlDbType.Decimal) { Precision = 12, Scale = 6, Value = costoUsd });
            bdComando.Parameters.Add(new SqlParameter("@con_saldo", conSaldo));
            bdComando.ExecuteNonQuery();
        }

        public DataTable listarUso(string admClienteId, string codigoSociedad, DateTime desde, DateTime hasta, bool soloSaldo)
        {
            DataTable dt = new DataTable();
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_listarUsoIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", (object)admClienteId ?? DBNull.Value));
            bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", (object)codigoSociedad ?? DBNull.Value));
            bdComando.Parameters.Add(new SqlParameter("@desde", SqlDbType.Date) { Value = desde.Date });
            bdComando.Parameters.Add(new SqlParameter("@hasta", SqlDbType.Date) { Value = hasta.Date });
            bdComando.Parameters.Add(new SqlParameter("@solo_saldo", soloSaldo));
            using SqlDataAdapter adapter = new SqlDataAdapter(bdComando);
            adapter.Fill(dt);
            return dt;
        }
    }
}
