using esupplier.Models.IA;
using esupplier.Repositorys.IRepositorys;
using Microsoft.Data.SqlClient;
using System.Data;

namespace esupplier.Repositorys
{
    public class PlanIARepository : IPlanIARepository
    {
        private readonly IConfiguration _configuration;

        public PlanIARepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection Conexion() => new SqlConnection(_configuration.GetConnectionString("esupplier"));

        public ConfigPlanIA obtenerConfig()
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_obtenerConfigPlanIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            using SqlDataReader lector = bdComando.ExecuteReader();
            if (!lector.Read()) throw new InvalidOperationException("Falta la configuración de planes de IA (tb_ia_config_plan).");
            return new ConfigPlanIA
            {
                iva_pct = Convert.ToDecimal(lector["iva_pct"]),
                dias_gracia = Convert.ToInt32(lector["dias_gracia"])
            };
        }

        public List<PlanIA> listarPlanes(bool soloActivos)
        {
            var planes = new List<PlanIA>();
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_listarPlanIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@solo_activos", soloActivos));
            using SqlDataReader lector = bdComando.ExecuteReader();
            while (lector.Read())
            {
                planes.Add(new PlanIA
                {
                    codigo_plan = lector["codigo_plan"] as string,
                    nombre = lector["nombre"] as string,
                    nombre_en = lector["nombre_en"] as string,
                    descripcion = lector["descripcion"] as string,
                    descripcion_en = lector["descripcion_en"] as string,
                    precio_mensual_usd = Convert.ToDecimal(lector["precio_mensual_usd"]),
                    saldo_mes_usd = Convert.ToDecimal(lector["saldo_mes_usd"]),
                    recomendado = Convert.ToBoolean(lector["recomendado"]),
                    activo = Convert.ToBoolean(lector["activo"]),
                    orden = Convert.ToInt32(lector["orden"])
                });
            }
            return planes;
        }

        public SuscripcionIA obtenerSuscripcion(string admClienteId, string codigoSociedad) =>
            LeerSuscripcion(admClienteId, codigoSociedad, null);

        public SuscripcionIA obtenerSuscripcionKushki(string kushkiSuscripcionId) =>
            LeerSuscripcion(null, null, kushkiSuscripcionId);

        private SuscripcionIA LeerSuscripcion(string admClienteId, string codigoSociedad, string kushkiSuscripcionId)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_obtenerSuscripcionIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add("@adm_cliente_id", SqlDbType.VarChar, 12).Value = (object)admClienteId ?? DBNull.Value;
            bdComando.Parameters.Add("@codigo_sociedad", SqlDbType.VarChar, 18).Value = (object)codigoSociedad ?? DBNull.Value;
            bdComando.Parameters.Add("@kushki_suscripcion_id", SqlDbType.VarChar, 40).Value = (object)kushkiSuscripcionId ?? DBNull.Value;
            using SqlDataReader lector = bdComando.ExecuteReader();
            if (!lector.Read()) return null;
            return new SuscripcionIA
            {
                adm_cliente_id = lector["adm_cliente_id"] as string,
                codigo_sociedad = lector["codigo_sociedad"] as string,
                codigo_plan = lector["codigo_plan"] as string,
                codigo_plan_siguiente = lector["codigo_plan_siguiente"] as string,
                inicio_periodo = lector["inicio_periodo"] as DateTime?,
                fin_periodo = lector["fin_periodo"] as DateTime?,
                cancelar_al_final = Convert.ToBoolean(lector["cancelar_al_final"]),
                proximo_cobro = lector["proximo_cobro"] as DateTime?,
                kushki_suscripcion_id = lector["kushki_suscripcion_id"] as string,
                tarjeta_marca = lector["tarjeta_marca"] as string,
                tarjeta_ultimos4 = lector["tarjeta_ultimos4"] as string,
                email = lector["email"] as string,
                nombres = lector["nombres"] as string,
                apellidos = lector["apellidos"] as string,
                tipo_documento = lector["tipo_documento"] as string,
                numero_documento = lector["numero_documento"] as string,
                telefono = lector["telefono"] as string,
                intentos_fallidos = Convert.ToInt32(lector["intentos_fallidos"]),
                ultimo_error = lector["ultimo_error"] as string,
                vigente = Convert.ToBoolean(lector["vigente"])
            };
        }

        public void registrarSuscripcion(SuscripcionIA s, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_registrarSuscripcionIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add("@adm_cliente_id", SqlDbType.VarChar, 12).Value = s.adm_cliente_id;
            bdComando.Parameters.Add("@codigo_sociedad", SqlDbType.VarChar, 18).Value = s.codigo_sociedad;
            bdComando.Parameters.Add("@codigo_plan", SqlDbType.VarChar, 20).Value = s.codigo_plan;
            bdComando.Parameters.Add("@kushki_suscripcion_id", SqlDbType.VarChar, 40).Value = s.kushki_suscripcion_id;
            bdComando.Parameters.Add(new SqlParameter("@proximo_cobro", SqlDbType.Date) { Value = s.proximo_cobro.Value.Date });
            bdComando.Parameters.Add("@tarjeta_marca", SqlDbType.VarChar, 30).Value = (object)s.tarjeta_marca ?? DBNull.Value;
            bdComando.Parameters.Add("@tarjeta_ultimos4", SqlDbType.VarChar, 4).Value = (object)s.tarjeta_ultimos4 ?? DBNull.Value;
            bdComando.Parameters.Add("@email", SqlDbType.VarChar, 150).Value = s.email;
            bdComando.Parameters.Add("@nombres", SqlDbType.NVarChar, 60).Value = s.nombres;
            bdComando.Parameters.Add("@apellidos", SqlDbType.NVarChar, 60).Value = s.apellidos;
            bdComando.Parameters.Add("@tipo_documento", SqlDbType.VarChar, 3).Value = s.tipo_documento;
            bdComando.Parameters.Add("@numero_documento", SqlDbType.VarChar, 20).Value = s.numero_documento;
            bdComando.Parameters.Add("@telefono", SqlDbType.VarChar, 20).Value = s.telefono;
            bdComando.Parameters.Add("@usuario", SqlDbType.VarChar, 50).Value = usuario;
            bdComando.ExecuteNonQuery();
        }

        public void actualizarSuscripcion(string admClienteId, string codigoSociedad, string accion, string codigoPlan,
            string tarjetaMarca, string tarjetaUltimos4, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_actualizarSuscripcionIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add("@adm_cliente_id", SqlDbType.VarChar, 12).Value = admClienteId;
            bdComando.Parameters.Add("@codigo_sociedad", SqlDbType.VarChar, 18).Value = codigoSociedad;
            bdComando.Parameters.Add("@accion", SqlDbType.VarChar, 15).Value = accion;
            bdComando.Parameters.Add("@codigo_plan", SqlDbType.VarChar, 20).Value = (object)codigoPlan ?? DBNull.Value;
            bdComando.Parameters.Add("@tarjeta_marca", SqlDbType.VarChar, 30).Value = (object)tarjetaMarca ?? DBNull.Value;
            bdComando.Parameters.Add("@tarjeta_ultimos4", SqlDbType.VarChar, 4).Value = (object)tarjetaUltimos4 ?? DBNull.Value;
            bdComando.Parameters.Add("@usuario", SqlDbType.VarChar, 50).Value = usuario;
            bdComando.ExecuteNonQuery();
        }

        public bool acreditarCobro(string kushkiSuscripcionId, string ticket, string eventoId, DateTime fechaCobro,
            decimal subtotal, decimal iva, decimal total, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_acreditarCobroPlanIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add("@kushki_suscripcion_id", SqlDbType.VarChar, 40).Value = kushkiSuscripcionId;
            bdComando.Parameters.Add("@kushki_ticket", SqlDbType.VarChar, 40).Value = (object)ticket ?? DBNull.Value;
            bdComando.Parameters.Add("@kushki_evento_id", SqlDbType.VarChar, 80).Value = (object)eventoId ?? DBNull.Value;
            bdComando.Parameters.Add(new SqlParameter("@fecha_cobro", SqlDbType.Date) { Value = fechaCobro.Date });
            bdComando.Parameters.Add(Monto("@subtotal_usd", subtotal));
            bdComando.Parameters.Add(Monto("@iva_usd", iva));
            bdComando.Parameters.Add(Monto("@total_usd", total));
            bdComando.Parameters.Add("@usuario", SqlDbType.VarChar, 50).Value = usuario;
            return Convert.ToBoolean(bdComando.ExecuteScalar());
        }

        public void registrarCobroFallido(string kushkiSuscripcionId, string eventoId, decimal subtotal, decimal iva, decimal total,
            string mensaje, bool ultimo, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_registrarCobroFallidoPlanIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add("@kushki_suscripcion_id", SqlDbType.VarChar, 40).Value = kushkiSuscripcionId;
            bdComando.Parameters.Add("@kushki_evento_id", SqlDbType.VarChar, 80).Value = (object)eventoId ?? DBNull.Value;
            bdComando.Parameters.Add(Monto("@subtotal_usd", subtotal));
            bdComando.Parameters.Add(Monto("@iva_usd", iva));
            bdComando.Parameters.Add(Monto("@total_usd", total));
            bdComando.Parameters.Add("@mensaje", SqlDbType.NVarChar, 300).Value = (object)mensaje ?? DBNull.Value;
            bdComando.Parameters.Add(new SqlParameter("@ultimo", ultimo));
            bdComando.Parameters.Add("@usuario", SqlDbType.VarChar, 50).Value = usuario;
            bdComando.ExecuteNonQuery();
        }

        public DataTable listarPagos(string admClienteId, string codigoSociedad)
        {
            DataTable dt = new DataTable();
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_listarPagoPlanIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add("@adm_cliente_id", SqlDbType.VarChar, 12).Value = (object)admClienteId ?? DBNull.Value;
            bdComando.Parameters.Add("@codigo_sociedad", SqlDbType.VarChar, 18).Value = (object)codigoSociedad ?? DBNull.Value;
            using SqlDataAdapter adapter = new SqlDataAdapter(bdComando);
            adapter.Fill(dt);
            return dt;
        }

        public (ConfigKushkiIA config, int suscripcionesVivas) obtenerConfigKushki()
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_obtenerConfigKushkiIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            using SqlDataReader lector = bdComando.ExecuteReader();
            ConfigKushkiIA config = null;
            if (lector.Read())
            {
                config = new ConfigKushkiIA
                {
                    prueba = Convert.ToBoolean(lector["prueba"]),
                    llave_publica = lector["llave_publica"] as string,
                    secreto_ref = lector["secreto_ref"] as string,
                    sufijo_privada = lector["sufijo_privada"] as string,
                    sufijo_webhook = lector["sufijo_webhook"] as string,
                    estado = lector["estado"] as string,
                    fecha_verificacion = lector["fecha_verificacion"] as DateTime?,
                    fecha_modificacion = lector["reg_fecha_modificacion"] as DateTime?,
                    modificado_por = lector["reg_usuario_modificacion"] as string
                };
            }
            lector.NextResult();
            int vivas = lector.Read() ? Convert.ToInt32(lector["suscripciones_vivas"]) : 0;
            return (config, vivas);
        }

        public void guardarConfigKushki(ConfigKushkiIA c, string usuario)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_guardarConfigKushkiIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add(new SqlParameter("@prueba", c.prueba));
            bdComando.Parameters.Add("@llave_publica", SqlDbType.VarChar, 100).Value = c.llave_publica;
            bdComando.Parameters.Add("@secreto_ref", SqlDbType.VarChar, 300).Value = c.secreto_ref;
            bdComando.Parameters.Add("@sufijo_privada", SqlDbType.VarChar, 4).Value = c.sufijo_privada;
            bdComando.Parameters.Add("@sufijo_webhook", SqlDbType.VarChar, 4).Value = (object)c.sufijo_webhook ?? DBNull.Value;
            bdComando.Parameters.Add("@usuario", SqlDbType.VarChar, 50).Value = usuario;
            bdComando.ExecuteNonQuery();
        }

        public void actualizarEstadoConfigKushki(string estado)
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_actualizarEstadoConfigKushkiIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            bdComando.Parameters.Add("@estado", SqlDbType.VarChar, 15).Value = estado;
            bdComando.ExecuteNonQuery();
        }

        public string eliminarConfigKushki()
        {
            using SqlConnection bdSql = Conexion();
            bdSql.Open();
            using SqlCommand bdComando = new SqlCommand("sp_eliminarConfigKushkiIA", bdSql);
            bdComando.CommandType = CommandType.StoredProcedure;
            return bdComando.ExecuteScalar() as string;
        }

        private static SqlParameter Monto(string nombre, decimal valor) =>
            new SqlParameter(nombre, SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = Math.Round(valor, 2) };
    }
}
