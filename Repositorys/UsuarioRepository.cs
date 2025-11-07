using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;

namespace esupplier.Repositorys
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private IConfiguration _configuration;

        public UsuarioRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Mensaje validarUsuario(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            string adm_cliente_id = "";
            string codigo_sociedad = "";
            string usuario = "";
            string clave = "";
            string codigo_referencia = "";
            int id_rol = 0;

            DataTable dt = new DataTable();

            try
            {
                adm_cliente_id = jObject["adm_cliente_id"].ToString();
                codigo_sociedad = jObject["codigo_sociedad"].ToString();
                usuario = jObject["usuario"].ToString();
                clave = jObject["clave"].ToString();

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_validarUsuario", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", adm_cliente_id));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigo_sociedad));
                    bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
                    bdComando.Parameters.Add(new SqlParameter("@clave", clave));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_referencia", SqlDbType.VarChar, 18));
                    bdComando.Parameters[4].Direction = ParameterDirection.Output;
                    bdComando.Parameters.Add(new SqlParameter("@rol_id", SqlDbType.Int));
                    bdComando.Parameters[5].Direction = ParameterDirection.Output;
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        codigo_referencia = bdComando.Parameters[4].Value.ToString();
                        id_rol = Int32.Parse(bdComando.Parameters[5].Value.ToString());

                        if (codigo_referencia.Equals(""))
                        {
                            mensaje.tipo = "E";
                            mensaje.mensaje = "Usuario o Clave incorrrecto";
                        }
                        else
                        {
                            mensaje.tipo = "S";
                            mensaje.mensaje = "";
                            mensaje.data = "\"usuario\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                        }
                        
                    }

                    if (id_rol > 0 && mensaje.tipo.Equals("S"))
                    {
                        bdComando = new SqlCommand("sp_obtenerPermisos", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@reg_id_rol", id_rol));
                        using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                        {
                            dt = new DataTable();
                            adapter.Fill(dt);
                            mensaje.tipo = "S";
                            mensaje.mensaje = "";
                            mensaje.data = mensaje.data + ",\"permisos\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                        }
                    }

                    if (!codigo_referencia.Equals("0") && mensaje.tipo.Equals("S"))
                    {
                        bdComando = new SqlCommand("sp_obtenerProveedor", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", adm_cliente_id));
                        bdComando.Parameters.Add(new SqlParameter("@sociedad", codigo_sociedad));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", codigo_referencia));
                        using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                        {
                            dt = new DataTable();
                            adapter.Fill(dt);
                            mensaje.tipo = "S";
                            mensaje.mensaje = "";
                            mensaje.data = mensaje.data + ",\"proveedor\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                        }
                    }
                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "Usuario o Clave incorrrecto";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje listarUsuario(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            DataTable dt = new DataTable();

            try 
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_listarUsuario", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@indicador", jObject["indicador"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", jObject["adm_cliente_id"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", jObject["codigo_sociedad"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_referencia", jObject["codigo_referencia"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", jObject["denominacion_fiscal"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", jObject["identificacion_fiscal"].ToString()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"usuarios\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se Encontraron Registros";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje mantenerUsuario(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_mantenerUsuario", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@reg_id", Convert.ToInt32(jObject["reg_id"].ToString())));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", jObject["adm_cliente_id"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", jObject["codigo_sociedad"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_referencia", jObject["codigo_referencia"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_referencia_aux", jObject["codigo_referencia_aux"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@usuario", jObject["usuario"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@clave", jObject["clave"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@rol_id", Convert.ToInt32(jObject["rol_id"].ToString())));
                    bdComando.Parameters.Add(new SqlParameter("@denominacion_usuario", jObject["denominacion_usuario"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@correo_referencia", jObject["correo_referencia"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@referencia_id", Convert.ToInt32(jObject["referencia_id"].ToString())));
                    bdComando.Parameters.Add(new SqlParameter("@idioma", jObject["idioma"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", jObject["reg_estado"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", jObject["reg_usuario_creacion"].ToString()));

                    bdComando.ExecuteNonQuery();

                    bdSql.Close();
                }

                mensaje.tipo = "S";
                if (jObject["reg_id"].ToString().Equals("0"))
                {
                    mensaje.mensaje = "Registrado correctamente";
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
                mensaje.mensaje = "Usuario ya existe";
                mensaje.data = "";
            }

            return mensaje;
        }

    }
}
