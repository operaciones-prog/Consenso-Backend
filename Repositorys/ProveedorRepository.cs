using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace esupplier.Repositorys
{
    public class ProveedorRepository : IProveedorRepository
    {
        private readonly IConfiguration _configuration;
        private readonly IHeaderService _headerService;
        private readonly ILogger<ProveedorRepository> _logger;

        public ProveedorRepository(IConfiguration configuration, IHeaderService headerService, ILogger<ProveedorRepository> logger)
        {
            _configuration = configuration;
            _headerService = headerService;
            _logger = logger;
        }

        public Mensaje buscarProveedor(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                string admClienteId = JsonUtil.Requerido(jObject, "adm_cliente_id");
                string sociedad = JsonUtil.Opcional(jObject, "sociedad");
                string codigoProveedor = JsonUtil.Opcional(jObject, "codigo_proveedor");
                string identificacionFiscal = JsonUtil.Opcional(jObject, "identificacion_fiscal");
                string denominacionFiscal = JsonUtil.Opcional(jObject, "denominacion_fiscal");

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    using SqlCommand bdComando = new SqlCommand("sp_buscarProveedor", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", admClienteId));
                    bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", codigoProveedor));
                    bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", identificacionFiscal));
                    bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", denominacionFiscal));
                    using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"proveedores\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                }
            }
            catch (CampoRequeridoException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_buscarProveedor");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }

        /// <summary>
        /// Alta/modificación de proveedores enviada por SAP. Cada proveedor se procesa en su propia transacción
        /// (proveedor + usuario + generación de usuario): si un paso falla se revierte solo ese proveedor.
        /// </summary>
        public Mensaje registrarProveedor(JObject jObject)
        {
            if (!(jObject?["Proveedor"] is JArray proveedores) || proveedores.Count == 0)
            {
                return new Mensaje("E", "Falta el campo obligatorio 'Proveedor'.", "");
            }

            // Validación previa de correos (se mantiene el comportamiento: se detiene en el primer error).
            foreach (JToken token in proveedores)
            {
                if (!(token is JObject item))
                {
                    return new Mensaje("E", "Formato de proveedor no válido.", "");
                }
                string correo = JsonUtil.Opcional(item, "Correo");
                string sociedad = JsonUtil.Opcional(item, "Sociedad");
                if (correo.Equals(""))
                {
                    return new Mensaje("E", "Email del Proveedor no encontrado para la Sociedad " + sociedad, "");
                }
                if (!new EmailAddressAttribute().IsValid(correo))
                {
                    return new Mensaje("E", "Email del Proveedor con formato incorrecto para la Sociedad " + sociedad, "");
                }
            }

            var fallidos = new List<string>();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    foreach (JToken token in proveedores)
                    {
                        JObject item = (JObject)token;
                        string sociedad = JsonUtil.Opcional(item, "Sociedad");
                        string codigoProveedor = JsonUtil.Opcional(item, "Proveedor");
                        string identificador = $"{sociedad}/{codigoProveedor}";

                        using SqlTransaction transaccion = bdSql.BeginTransaction();
                        try
                        {
                            string actionCode = JsonUtil.Requerido(item, "ActionCode");

                            using (SqlCommand bdComando = new SqlCommand("sp_mantenerProveedor", bdSql, transaccion))
                            {
                                bdComando.CommandType = CommandType.StoredProcedure;
                                bdComando.Parameters.Add(new SqlParameter("@action_code", actionCode));
                                bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                bdComando.Parameters.Add(new SqlParameter("@sociedad", JsonUtil.Requerido(item, "Sociedad")));
                                bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", JsonUtil.Requerido(item, "Proveedor")));
                                bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", JsonUtil.Opcional(item, "RazonSocial")));
                                bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", JsonUtil.Opcional(item, "IdentFiscal")));
                                bdComando.Parameters.Add(new SqlParameter("@tipo_nif", JsonUtil.Opcional(item, "TipoIdentFiscal")));
                                bdComando.Parameters.Add(new SqlParameter("@direccion", JsonUtil.Opcional(item, "Direccion")));
                                bdComando.Parameters.Add(new SqlParameter("@correo", JsonUtil.Opcional(item, "Correo")));
                                bdComando.Parameters.Add(new SqlParameter("@telefono", JsonUtil.Opcional(item, "Telefono")));
                                bdComando.Parameters.Add(new SqlParameter("@pais", JsonUtil.Opcional(item, "Pais")));
                                bdComando.Parameters.Add(new SqlParameter("@idioma", JsonUtil.Opcional(item, "Idioma")));
                                bdComando.Parameters.Add(new SqlParameter("@bloqueo_contabilizar", JsonUtil.Opcional(item, "BloqueoContabilizar")));
                                bdComando.Parameters.Add(new SqlParameter("@peticion_borrado", JsonUtil.Opcional(item, "PeticionBorrado")));
                                bdComando.Parameters.Add(new SqlParameter("@reg_estado", Constantes.ESTADO_ACTIVO_CREATE));
                                bdComando.ExecuteNonQuery();
                            }

                            if (actionCode.Equals("CREATE"))
                            {
                                // La clave inicial no se devuelve ni se envía: solo se almacena su hash.
                                string clave = PasswordHasher.GenerarClaveAleatoria();
                                int rolId = JsonUtil.Opcional(item, "Pais").Equals("EC - Ecuador")
                                    ? Constantes.ROL_PROVEEDOR
                                    : Constantes.ROL_PROVEEDOR_EXT;

                                using (SqlCommand bdComando = new SqlCommand("sp_mantenerUsuario", bdSql, transaccion))
                                {
                                    bdComando.CommandType = CommandType.StoredProcedure;
                                    bdComando.Parameters.Add(new SqlParameter("@reg_id", Constantes.ID_CERO));
                                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                    bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", sociedad));
                                    bdComando.Parameters.Add(new SqlParameter("@codigo_referencia", codigoProveedor));
                                    bdComando.Parameters.Add(new SqlParameter("@codigo_referencia_aux", Constantes.NO_REFERIDO));
                                    bdComando.Parameters.Add(new SqlParameter("@usuario", Constantes.NO_INICIA));
                                    bdComando.Parameters.Add("@clave", SqlDbType.VarChar, 256).Value = PasswordHasher.Hashear(clave);
                                    bdComando.Parameters.Add(new SqlParameter("@idioma", JsonUtil.Opcional(item, "Idioma")));
                                    bdComando.Parameters.Add(new SqlParameter("@rol_id", rolId));
                                    bdComando.Parameters.Add(new SqlParameter("@denominacion_usuario", Constantes.NO_INICIA));
                                    bdComando.Parameters.Add(new SqlParameter("@correo_referencia", Constantes.NO_INICIA));
                                    bdComando.Parameters.Add(new SqlParameter("@referencia_id", Constantes.ID_CERO));
                                    bdComando.Parameters.Add(new SqlParameter("@reg_estado", Constantes.ESTADO_ACTIVO_CREATE));
                                    bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", Constantes.USUARIO_ID_ADM));
                                    bdComando.ExecuteNonQuery();
                                }

                                using (SqlCommand bdComando = new SqlCommand("sp_generarUsuario", bdSql, transaccion))
                                {
                                    bdComando.CommandType = CommandType.StoredProcedure;
                                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                    bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                                    bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", codigoProveedor));
                                    bdComando.ExecuteNonQuery();
                                }
                            }

                            transaccion.Commit();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al registrar el proveedor {Proveedor}", identificador);
                            try
                            {
                                transaccion.Rollback();
                            }
                            catch (Exception exRollback)
                            {
                                // Puede fallar si el SP ya revirtió la transacción.
                                _logger.LogWarning(exRollback, "Rollback no aplicado para el proveedor {Proveedor}", identificador);
                            }
                            fallidos.Add(ex is CampoRequeridoException ? $"{identificador} ({ex.Message})" : identificador);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error de conexión al registrar proveedores");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            if (fallidos.Count > 0)
            {
                int registrados = proveedores.Count - fallidos.Count;
                return new Mensaje("E",
                    $"No se pudieron registrar {fallidos.Count} de {proveedores.Count} proveedores (registrados: {registrados}). Fallidos: {string.Join(", ", fallidos)}",
                    "");
            }

            return new Mensaje("S", "registrado correctamente", "");
        }

        /// <summary>
        /// Reemplaza la calificación de proveedores: elimina la carga anterior e inserta la nueva en una sola transacción.
        /// </summary>
        public Mensaje registrarCalificacion(List<Dictionary<string, object>> result)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    connection.Open();
                    using SqlTransaction transaction = connection.BeginTransaction();

                    try
                    {
                        using (SqlCommand bdComandoE = new SqlCommand("sp_eliminarCalificacionProveedor", connection, transaction))
                        {
                            bdComandoE.CommandType = CommandType.StoredProcedure;
                            bdComandoE.ExecuteNonQuery();
                        }

                        foreach (var item in result)
                        {
                            using SqlCommand bdComando = new SqlCommand("sp_insertarCalificacionProveedor", connection, transaction);
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
                            bdComando.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return new Mensaje("S", "registrado correctamente", "");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al registrar la calificación de proveedores; se revierte la carga");
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception exRollback)
                        {
                            _logger.LogError(exRollback, "Error al revertir la carga de calificación de proveedores");
                        }
                        return new Mensaje("E", "No se pudo registrar la calificación de proveedores; no se aplicaron cambios.", "");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error de conexión al registrar la calificación de proveedores");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }
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

                    using SqlCommand bdComando = new SqlCommand("sp_comboCalificacionProveedor", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@tipo", tipo));
                    bdComando.Parameters.Add(new SqlParameter("@codigo", codigo));
                    bdComando.Parameters.Add(new SqlParameter("@idioma", _headerService.obtenerIdioma()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_comboCalificacionProveedor (tipo {Tipo})", tipo);
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
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

                    using SqlCommand bdComando = new SqlCommand("sp_obtenerCalificacionProveedor", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@tipo", tipo));
                    bdComando.Parameters.Add(new SqlParameter("@codigo", codigo));
                    bdComando.Parameters.Add(new SqlParameter("@annio", annio));
                    bdComando.Parameters.Add(new SqlParameter("@mes", mes));
                    bdComando.Parameters.Add(new SqlParameter("@set_calificacion", setCalificacion));
                    bdComando.Parameters.Add(new SqlParameter("@origen", origen));
                    bdComando.Parameters.Add(new SqlParameter("@idioma", _headerService.obtenerIdioma()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_obtenerCalificacionProveedor (tipo {Tipo})", tipo);
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }
    }
}
