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
    }
}
