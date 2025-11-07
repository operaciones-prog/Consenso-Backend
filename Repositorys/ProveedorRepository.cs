using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Transactions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace esupplier.Repositorys
{
    public class ProveedorRepository : IProveedorRepository
    {
        private IConfiguration _configuration;
        private IHeaderService _headerService;
        public ProveedorRepository(IConfiguration configuration, IHeaderService headerService)
        {
            _configuration = configuration;
            _headerService  = headerService;
        }

        public Mensaje buscarProveedor(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_buscarProveedor", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", jObject["adm_cliente_id"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@sociedad", jObject["sociedad"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", jObject["codigo_proveedor"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", jObject["identificacion_fiscal"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", jObject["denominacion_fiscal"].ToString()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"proveedores\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }

                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = ex.Message.ToString();
                mensaje.data = "";

            }

            return mensaje;
        }

        public Mensaje registrarProveedor(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            mensaje.tipo = "";

            string clave = "";
            string usuario = "";

            try
            {
                JArray jsonArrayD = new JArray();
                jsonArrayD = (JArray)jObject["Proveedor"];

                for (int i = 0; i < jsonArrayD.Count; i++)
                {
                    if (jsonArrayD[i]["Correo"].ToString().Equals(""))
                    {
                        mensaje.tipo = "E";
                        mensaje.mensaje = "Email del Proveedor no encontrado para la Sociedad " + jsonArrayD[i]["Sociedad"].ToString();
                        mensaje.data = "";

                        i = jsonArrayD.Count;
                    }
                    else
                    {
                        if (!(new EmailAddressAttribute().IsValid(jsonArrayD[i]["Correo"].ToString())))
                        {
                            mensaje.tipo = "E";
                            mensaje.mensaje = "Email del Proveedor con formato incorrecto para la Sociedad " + jsonArrayD[i]["Sociedad"].ToString();
                            mensaje.data = "";

                            i = jsonArrayD.Count;
                        }
                    }
                }

                if (mensaje.tipo.Equals(""))
                {
                    using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                    {
                        bdSql.Open();

                        for (int i = 0; i < jsonArrayD.Count; i++)
                        {
                            SqlCommand bdComando = new SqlCommand("sp_mantenerProveedor", bdSql);
                            bdComando.CommandType = CommandType.StoredProcedure;
                            bdComando.Parameters.Add(new SqlParameter("@action_code", jsonArrayD[i]["ActionCode"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                            bdComando.Parameters.Add(new SqlParameter("@sociedad", jsonArrayD[i]["Sociedad"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", jsonArrayD[i]["Proveedor"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", jsonArrayD[i]["RazonSocial"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", jsonArrayD[i]["IdentFiscal"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@tipo_nif", jsonArrayD[i]["TipoIdentFiscal"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@direccion", jsonArrayD[i]["Direccion"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@correo", jsonArrayD[i]["Correo"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@telefono", jsonArrayD[i]["Telefono"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@pais", jsonArrayD[i]["Pais"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@idioma", jsonArrayD[i]["Idioma"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@bloqueo_contabilizar", jsonArrayD[i]["BloqueoContabilizar"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@peticion_borrado", jsonArrayD[i]["PeticionBorrado"].ToString()));
                            bdComando.Parameters.Add(new SqlParameter("@reg_estado", Constantes.ESTADO_ACTIVO_CREATE));

                            bdComando.ExecuteNonQuery();

                            clave = ConsultaUtil.generarCodigo(10);

                            try
                            {
                                if (jsonArrayD[i]["ActionCode"].ToString().Equals("CREATE"))
                                {
                                    bdComando = new SqlCommand("sp_mantenerUsuario", bdSql);
                                    bdComando.CommandType = CommandType.StoredProcedure;
                                    bdComando.Parameters.Add(new SqlParameter("@reg_id", Convert.ToInt32(Constantes.ID_CERO)));
                                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                    bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", jsonArrayD[i]["Sociedad"].ToString()));
                                    bdComando.Parameters.Add(new SqlParameter("@codigo_referencia", jsonArrayD[i]["Proveedor"].ToString()));
                                    bdComando.Parameters.Add(new SqlParameter("@codigo_referencia_aux", Constantes.NO_REFERIDO));
                                    bdComando.Parameters.Add(new SqlParameter("@usuario", Constantes.NO_INICIA));
                                    bdComando.Parameters.Add(new SqlParameter("@clave", clave));
                                    bdComando.Parameters.Add(new SqlParameter("@idioma", jsonArrayD[i]["Idioma"].ToString()));
                                    if (jsonArrayD[i]["Pais"].ToString().Equals("EC - Ecuador"))
                                    {
                                        bdComando.Parameters.Add(new SqlParameter("@rol_id", Convert.ToInt32(Constantes.ROL_PROVEEDOR)));
                                    }
                                    else
                                    {
                                        bdComando.Parameters.Add(new SqlParameter("@rol_id", Convert.ToInt32(Constantes.ROL_PROVEEDOR_EXT)));
                                    }
                                    bdComando.Parameters.Add(new SqlParameter("@denominacion_usuario", Constantes.NO_INICIA));
                                    bdComando.Parameters.Add(new SqlParameter("@correo_referencia", Constantes.NO_INICIA));
                                    bdComando.Parameters.Add(new SqlParameter("@referencia_id", Constantes.ID_CERO));
                                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", Constantes.ESTADO_ACTIVO_CREATE));
                                    bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", Constantes.USUARIO_ID_ADM));

                                    bdComando.ExecuteNonQuery();

                                    bdComando = new SqlCommand("sp_generarUsuario", bdSql);
                                    bdComando.CommandType = CommandType.StoredProcedure;
                                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                    bdComando.Parameters.Add(new SqlParameter("@sociedad", jsonArrayD[i]["Sociedad"].ToString()));
                                    bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", jsonArrayD[i]["Proveedor"].ToString()));

                                    bdComando.ExecuteNonQuery();
                                }
                            }
                            catch (Exception ex) 
                            {
                                bdComando = new("sp_eliminarProveedor", bdSql);
                                bdComando.CommandType = CommandType.StoredProcedure;
                                bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                bdComando.Parameters.Add(new SqlParameter("@sociedad", jsonArrayD[i]["Sociedad"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", jsonArrayD[i]["Proveedor"].ToString()));

                                bdComando.ExecuteNonQuery();

                                mensaje.tipo = "E";
                                mensaje.mensaje = ex.Message.ToString();
                                mensaje.data = "";
                            }

                        }

                        bdSql.Close();
                    }

                    mensaje.tipo = "S";
                    mensaje.mensaje = "registrado correctamente";
                    mensaje.data = "";
                }

            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = ex.Message.ToString();
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje registrarCalificacion(List<Dictionary<string, object>> result)
        {
            Mensaje mensaje = new Mensaje();
            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("esupplier")))
            {
                connection.Open();
             //   SqlTransaction transaction = connection.BeginTransaction();

                try
                {
                    SqlCommand bdComandoE = new SqlCommand("sp_eliminarCalificacionProveedor", connection);
                    bdComandoE.CommandType = CommandType.StoredProcedure;
                    bdComandoE.ExecuteNonQuery();
                    foreach (var item in result)
                    {
                        SqlCommand bdComando = new SqlCommand("sp_insertarCalificacionProveedor", connection);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@origen", (string)item["origen"]));
                        bdComando.Parameters.Add(new SqlParameter("@nombre", (string)item["nombre"]));
                        bdComando.Parameters.Add(new SqlParameter("@codigo", (string)item["codigo"]));
                        bdComando.Parameters.Add(new SqlParameter("@annio", (string)item["annio"]));
                        bdComando.Parameters.Add(new SqlParameter("@mes", (string)item["mes"]));
                        bdComando.Parameters.Add(new SqlParameter("@set_calificacion", (string)item["set_calificacion"]));
                        bdComando.Parameters.Add(new SqlParameter("@calidad", (double)item["calidad"]));
                        bdComando.Parameters.Add(new SqlParameter("@contrato", (double)item["contrato"]));
                        bdComando.Parameters.Add(new SqlParameter("@credito", (double)item["credito"]));
                        bdComando.Parameters.Add(new SqlParameter("@on_time", (double)item["on_time"]));
                        bdComando.Parameters.Add(new SqlParameter("@in_full", (double)item["in_full"]));
                        bdComando.Parameters.Add(new SqlParameter("@calificacion", (double)item["calificacion"]));

                        // Ejecutar el procedimiento almacenado
                        bdComando.ExecuteNonQuery();
                    }

                    // Confirmar la transacción
                   // transaction.Commit();
                    mensaje.tipo = "S";
                    mensaje.mensaje = "registrado correctamente";
                    mensaje.data = "";
                }
                catch (Exception ex)
                {/*
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception ex2)
                    {
                        mensaje.tipo = "E";
                        mensaje.mensaje = ex2.Message.ToString();
                        mensaje.data = "";
                        throw new InvalidOperationException(ex2.Message);
                    }*/

                    mensaje.tipo = "E";
                    mensaje.mensaje = ex.Message.ToString();
                    mensaje.data = "";

                }
                finally
                {
                    connection.Close();
                }
            }
            return mensaje;
        }


        public Mensaje comboCalificacion(int tipo, string codigo)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_comboCalificacionProveedor", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@tipo", tipo));
                    bdComando.Parameters.Add(new SqlParameter("@codigo", codigo));
                    bdComando.Parameters.Add(new SqlParameter("@idioma", _headerService.obtenerIdioma()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }

                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron registros";
                mensaje.data = "";
            }

            return mensaje;
        }


        public Mensaje obtenerCalificacion(int tipo, string codigo, string annio, string mes, string setCalificacion, string origen)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_obtenerCalificacionProveedor", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@tipo", tipo));
                    bdComando.Parameters.Add(new SqlParameter("@codigo", codigo));
                    bdComando.Parameters.Add(new SqlParameter("@annio", annio));
                    bdComando.Parameters.Add(new SqlParameter("@mes", mes));
                    bdComando.Parameters.Add(new SqlParameter("@set_calificacion", setCalificacion));
                    bdComando.Parameters.Add(new SqlParameter("@origen", origen));
                    bdComando.Parameters.Add(new SqlParameter("@idioma", _headerService.obtenerIdioma()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }

                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron registros";
                mensaje.data = "";
            }

            return mensaje;
        }

    }
}
