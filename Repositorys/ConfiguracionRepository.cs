using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Globalization;

namespace esupplier.Repositorys
{
    public class ConfiguracionRepository : IConfiguracionRepository
    {
        private readonly IConfiguration _configuration;
        private readonly IHeaderService _headerService;
        private readonly ILogger<ConfiguracionRepository> _logger;

        public ConfiguracionRepository(IConfiguration configuration, IHeaderService headerService, ILogger<ConfiguracionRepository> logger)
        {
            _configuration = configuration;
            _headerService = headerService;
            _logger = logger;
        }

        public Mensaje listarConstantes(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                string admClienteId = JsonUtil.Requerido(jObject, "adm_cliente_id");
                string tipoConstante = JsonUtil.Opcional(jObject, "tipo_constante");
                string codigoConstante = JsonUtil.Opcional(jObject, "codigo_constante");

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    using SqlCommand bdComando = new SqlCommand("sp_listarConstantes", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
                    bdComando.Parameters.Add(new SqlParameter("@tipo_constante", tipoConstante));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_constante", codigoConstante));
                    bdComando.Parameters.Add(new SqlParameter("@idioma", _headerService.obtenerIdioma()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"constantes\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                }
            }
            catch (CampoRequeridoException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_listarConstantes");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }

        public Mensaje mantenerConstante(JObject jObject)
        {
            try
            {
                int regId = int.Parse(JsonUtil.Requerido(jObject, "reg_id"), NumberStyles.Integer, CultureInfo.InvariantCulture);

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    using SqlCommand bdComando = new SqlCommand("sp_mantenerConstante", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@reg_id", regId));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", JsonUtil.Requerido(jObject, "adm_cliente_id")));
                    bdComando.Parameters.Add(new SqlParameter("@tipo_constante", JsonUtil.Requerido(jObject, "tipo_constante")));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_constante", JsonUtil.Requerido(jObject, "codigo_constante")));
                    bdComando.Parameters.Add(new SqlParameter("@archivo", JsonUtil.Opcional(jObject, "archivo")));
                    bdComando.Parameters.Add(new SqlParameter("@valor_constante", JsonUtil.Opcional(jObject, "valor_constante")));
                    bdComando.Parameters.Add(new SqlParameter("@descripcion", JsonUtil.Opcional(jObject, "descripcion")));
                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", JsonUtil.Requerido(jObject, "reg_estado")));
                    bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", JsonUtil.Requerido(jObject, "reg_usuario_creacion")));

                    bdComando.ExecuteNonQuery();
                }

                return new Mensaje("S", "Modificado correctamente", "");
            }
            catch (CampoRequeridoException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            catch (FormatException)
            {
                return new Mensaje("E", "El campo 'reg_id' no es válido.", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_mantenerConstante");
                return new Mensaje("E", "Ocurrio un error en la actualización", "");
            }
        }

        public Mensaje listarSociedad(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                string admClienteId = JsonUtil.Requerido(jObject, "adm_cliente_id");
                string regEstado = JsonUtil.Opcional(jObject, "reg_estado");

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    using SqlCommand bdComando = new SqlCommand("sp_listarSociedad", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", regEstado));
                    using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"sociedad\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                }
            }
            catch (CampoRequeridoException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_listarSociedad");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }

        public Mensaje mantenerSociedad(JObject jObject)
        {
            try
            {
                string reg_id = JsonUtil.Requerido(jObject, "reg_id");

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    using SqlCommand bdComando = new SqlCommand("sp_mantenerSociedad", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@reg_id", reg_id));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", JsonUtil.Requerido(jObject, "adm_cliente_id")));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", JsonUtil.Requerido(jObject, "codigo_sociedad")));
                    bdComando.Parameters.Add(new SqlParameter("@denominacion_sociedad", JsonUtil.Opcional(jObject, "denominacion_sociedad")));
                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", JsonUtil.Requerido(jObject, "reg_estado")));
                    bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", JsonUtil.Requerido(jObject, "reg_usuario_creacion")));

                    bdComando.ExecuteNonQuery();
                }

                return new Mensaje("S", reg_id.Equals("0") ? "Creado correctamente" : "Modificado correctamente", "");
            }
            catch (CampoRequeridoException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_mantenerSociedad");
                return new Mensaje("E", "Ocurrio un error en la actualización", "");
            }
        }

        public Mensaje registrarCondicionPago(JObject jObject)
        {
            int id = 0;
            if (jObject?["id"] != null && int.TryParse(jObject["id"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedId))
            {
                id = parsedId;
            }

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    using SqlCommand bdComando = new SqlCommand("sp_insertarCondicionPago", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@p_id", id));
                    bdComando.Parameters.Add(new SqlParameter("@p_codigo", JsonUtil.Requerido(jObject, "codigo")));
                    bdComando.Parameters.Add(new SqlParameter("@p_nombre_es", JsonUtil.Requerido(jObject, "nombreEs")));
                    bdComando.Parameters.Add(new SqlParameter("@p_nombre_en", JsonUtil.Requerido(jObject, "nombreEn")));

                    bdComando.ExecuteNonQuery();
                }

                return new Mensaje("S", id == 0 ? "Creado correctamente" : "Modificado correctamente", "");
            }
            catch (CampoRequeridoException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_insertarCondicionPago (id {Id})", id);
                return new Mensaje("E", "Ocurrio un error en la actualización", "");
            }
        }

        public Mensaje listarCondicionPago()
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    using SqlCommand bdComando = new SqlCommand("sp_listarCondicionPago", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"condiciones\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_listarCondicionPago");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }

        public Mensaje eliminarCondicionPago(int id)
        {
            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    // Antes llamaba por error a sp_insertarCondicionPago solo con @p_id.
                    using SqlCommand bdComando = new SqlCommand("sp_eliminarCondicionPago", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add("@p_id", SqlDbType.Int).Value = id;

                    bdComando.ExecuteNonQuery();
                }

                return new Mensaje("S", "Eliminado correctamente", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_eliminarCondicionPago (id {Id})", id);
                return new Mensaje("E", "Ocurrio un error al eliminar", "");
            }
        }

        public Mensaje buscarCondicionPagoPorCodigo(string codigo)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    using SqlCommand bdComando = new SqlCommand("sp_buscarCondicionPago", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@codigo", (object)codigo ?? DBNull.Value));
                    using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"condiciones\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_buscarCondicionPago (codigo {Codigo})", codigo);
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }
    }
}
