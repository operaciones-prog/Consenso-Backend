using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using Microsoft.Data.SqlClient;
using Microsoft.Graph;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;

namespace esupplier.Repositorys
{
    public class ConfiguracionRepository : IConfiguracionRepository
    {
        private IConfiguration _configuration;
        private readonly IHeaderService _headerService;
        public ConfiguracionRepository(IConfiguration configuration, IHeaderService headerService)
        {
            _configuration = configuration;
            _headerService = headerService;
        }

        public Mensaje listarConstantes(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            DataTable dt = new DataTable();
            
            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_listarConstantes", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", jObject["adm_cliente_id"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@tipo_constante", jObject["tipo_constante"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_constante", jObject["codigo_constante"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@idioma", _headerService.obtenerIdioma()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"constantes\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron constantes";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje mantenerConstante(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_mantenerConstante", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@reg_id", Convert.ToInt32(jObject["reg_id"].ToString())));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", jObject["adm_cliente_id"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@tipo_constante", jObject["tipo_constante"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_constante", jObject["codigo_constante"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@archivo", jObject["archivo"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@valor_constante", jObject["valor_constante"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@descripcion", jObject["descripcion"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", jObject["reg_estado"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", jObject["reg_usuario_creacion"].ToString()));

                    bdComando.ExecuteNonQuery();

                    bdSql.Close();
                }

                mensaje.tipo = "S";
                mensaje.mensaje = "Modificado correctamente";
                mensaje.data = "";
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "Ocurrio un error en la actualización";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje listarSociedad(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_listarSociedad", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", jObject["adm_cliente_id"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", jObject["reg_estado"].ToString()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"sociedad\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron Sociedades";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje mantenerSociedad(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            string reg_id = jObject["reg_id"].ToString();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_mantenerSociedad", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@reg_id", reg_id));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", jObject["adm_cliente_id"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", jObject["codigo_sociedad"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@denominacion_sociedad", jObject["denominacion_sociedad"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", jObject["reg_estado"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", jObject["reg_usuario_creacion"].ToString()));

                    bdComando.ExecuteNonQuery();

                    bdSql.Close();
                }

                mensaje.tipo = "S";
                if (reg_id.Equals("0"))
                {
                    mensaje.mensaje = "Creado correctamente";
                }
                else
                {
                    mensaje.mensaje = "Modificado correctamente";
                }

                mensaje.data = "";
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "Ocurrio un error en la actualización";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje registrarCondicionPago(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            int id = 0;
            if (jObject["id"] != null && int.TryParse(jObject["id"].ToString(), out int parsedId))
            {
                id = parsedId;
            }

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_insertarCondicionPago", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@p_id", id));
                    bdComando.Parameters.Add(new SqlParameter("@p_codigo", jObject["codigo"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@p_nombre_es", jObject["nombreEs"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@p_nombre_en", jObject["nombreEn"].ToString()));

                    bdComando.ExecuteNonQuery();

                    bdSql.Close();
                }

                mensaje.tipo = "S";
                if (id == 0)
                {
                    mensaje.mensaje = "Creado correctamente";
                }
                else
                {
                    mensaje.mensaje = "Modificado correctamente";
                }

                mensaje.data = "";
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "Ocurrio un error en la actualización";
                mensaje.data = "";
            }

            return mensaje;
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
                    SqlCommand bdComando = new SqlCommand("sp_listarCondicionPago", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"condiciones\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron Condiciones de pago";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje eliminarCondicionPago(int id)
        {
            Mensaje mensaje = new Mensaje();


            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_insertarCondicionPago", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@p_id", id));

                    bdComando.ExecuteNonQuery();

                    bdSql.Close();
                }

                mensaje.tipo = "S";
                mensaje.mensaje = "Eliminado correctamente";


                mensaje.data = "";

            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "Ocurrio un error al eliminar";
                mensaje.data = "";
            }

            return mensaje;

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
                    SqlCommand bdComando = new SqlCommand("sp_buscarCondicionPago", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@codigo", codigo));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"condiciones\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron Condiciones de pago";
                mensaje.data = "";
            }

            return mensaje;
        }

    }
}
