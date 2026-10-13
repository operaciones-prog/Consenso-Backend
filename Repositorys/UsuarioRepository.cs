using System.Data;
using System.Globalization;
using esupplier.Models;
using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys
{
    public class UsuarioRepository : IUsuarioRepository
    {
        // El frontend puede comparar este texto: se mantiene tal cual.
        private const string MensajeCredencialesInvalidas = "Usuario o Clave incorrrecto";

        private readonly IConfiguration _configuration;
        private readonly ILogger<UsuarioRepository> _logger;

        public UsuarioRepository(IConfiguration configuration, ILogger<UsuarioRepository> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public ResultadoLogin validarUsuario(JObject jObject)
        {
            ResultadoLogin resultado = new ResultadoLogin();
            Mensaje mensaje = resultado.Mensaje;

            try
            {
                string adm_cliente_id = JsonUtil.Requerido(jObject, "adm_cliente_id");
                string codigo_sociedad = JsonUtil.Requerido(jObject, "codigo_sociedad");
                string usuario = JsonUtil.Requerido(jObject, "usuario");
                string clave = JsonUtil.Requerido(jObject, "clave");

                resultado.AdmClienteId = adm_cliente_id;
                resultado.CodigoSociedad = codigo_sociedad;
                resultado.Usuario = usuario;

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    // 1. Credenciales almacenadas (hash o texto plano heredado) de los usuarios activos.
                    var candidatos = new List<(int regId, string claveAlmacenada)>();
                    using (SqlCommand cmdCred = new SqlCommand("sp_obtenerCredencialUsuario", bdSql))
                    {
                        cmdCred.CommandType = CommandType.StoredProcedure;
                        cmdCred.Parameters.Add("@adm_cliente_id", SqlDbType.VarChar, 12).Value = adm_cliente_id;
                        cmdCred.Parameters.Add("@codigo_sociedad", SqlDbType.VarChar, 18).Value = codigo_sociedad;
                        cmdCred.Parameters.Add("@usuario", SqlDbType.VarChar, 50).Value = usuario;
                        using (SqlDataReader lector = cmdCred.ExecuteReader())
                        {
                            while (lector.Read())
                            {
                                int regId = Convert.ToInt32(lector["reg_id"], CultureInfo.InvariantCulture);
                                string almacenada = lector["clave"] == DBNull.Value ? "" : lector["clave"].ToString();
                                candidatos.Add((regId, almacenada));
                            }
                        }
                    }

                    // 2. Verificación de la clave.
                    int regIdValido = 0;
                    string claveAlmacenada = null;
                    bool requiereRehash = false;
                    foreach (var candidato in candidatos)
                    {
                        if (PasswordHasher.Verificar(clave, candidato.claveAlmacenada, out bool rehash))
                        {
                            regIdValido = candidato.regId;
                            claveAlmacenada = candidato.claveAlmacenada;
                            requiereRehash = rehash;
                            break;
                        }
                    }

                    if (claveAlmacenada == null)
                    {
                        mensaje.tipo = "E";
                        mensaje.mensaje = MensajeCredencialesInvalidas;
                        mensaje.data = "";
                        return resultado;
                    }

                    resultado.RegId = regIdValido;

                    // 3. Migración transparente: clave heredada (o hash débil) -> hash actual.
                    if (requiereRehash)
                    {
                        string nuevoHash = PasswordHasher.Hashear(clave);
                        try
                        {
                            using (SqlCommand cmdRehash = new SqlCommand("sp_actualizarClaveUsuario", bdSql))
                            {
                                cmdRehash.CommandType = CommandType.StoredProcedure;
                                cmdRehash.Parameters.Add("@reg_id", SqlDbType.Int).Value = regIdValido;
                                cmdRehash.Parameters.Add("@clave", SqlDbType.VarChar, 256).Value = nuevoHash;
                                cmdRehash.ExecuteNonQuery();
                            }
                            claveAlmacenada = nuevoHash;
                        }
                        catch (Exception exRehash)
                        {
                            _logger.LogWarning(exRehash, "No se pudo re-hashear la clave del usuario reg_id {RegId}; el login continúa.", regIdValido);
                        }
                    }

                    // 4. sp_validarUsuario recibe el valor almacenado para que su predicado clave = @clave coincida.
                    string codigo_referencia;
                    int id_rol;
                    DataTable dt = new DataTable();
                    using (SqlCommand bdComando = new SqlCommand("sp_validarUsuario", bdSql))
                    {
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", adm_cliente_id));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigo_sociedad));
                        bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
                        bdComando.Parameters.Add("@clave", SqlDbType.VarChar, 256).Value = claveAlmacenada;
                        SqlParameter pCodigoReferencia = bdComando.Parameters.Add("@codigo_referencia", SqlDbType.VarChar, 18);
                        pCodigoReferencia.Direction = ParameterDirection.Output;
                        SqlParameter pRolId = bdComando.Parameters.Add("@rol_id", SqlDbType.Int);
                        pRolId.Direction = ParameterDirection.Output;

                        using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                        {
                            adapter.Fill(dt);
                        }

                        codigo_referencia = pCodigoReferencia.Value == DBNull.Value ? "" : pCodigoReferencia.Value.ToString();
                        id_rol = pRolId.Value == DBNull.Value ? 0 : Convert.ToInt32(pRolId.Value, CultureInfo.InvariantCulture);
                    }

                    if (codigo_referencia.Equals(""))
                    {
                        mensaje.tipo = "E";
                        mensaje.mensaje = MensajeCredencialesInvalidas;
                        mensaje.data = "";
                        return resultado;
                    }

                    QuitarColumnaClave(dt);
                    resultado.RolId = id_rol;
                    resultado.CodigoReferencia = codigo_referencia;

                    mensaje.tipo = "S";
                    mensaje.mensaje = "";
                    mensaje.data = "\"usuario\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);

                    if (id_rol > 0)
                    {
                        using (SqlCommand bdComando = new SqlCommand("sp_obtenerPermisos", bdSql))
                        {
                            bdComando.CommandType = CommandType.StoredProcedure;
                            bdComando.Parameters.Add(new SqlParameter("@reg_id_rol", id_rol));
                            using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                            {
                                dt = new DataTable();
                                adapter.Fill(dt);
                                mensaje.data = mensaje.data + ",\"permisos\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                            }
                        }
                    }

                    if (!codigo_referencia.Equals("0"))
                    {
                        using (SqlCommand bdComando = new SqlCommand("sp_obtenerProveedor", bdSql))
                        {
                            bdComando.CommandType = CommandType.StoredProcedure;
                            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", adm_cliente_id));
                            bdComando.Parameters.Add(new SqlParameter("@sociedad", codigo_sociedad));
                            bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", codigo_referencia));
                            using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                            {
                                dt = new DataTable();
                                adapter.Fill(dt);
                                mensaje.data = mensaje.data + ",\"proveedor\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                            }
                        }
                    }
                }
            }
            catch (CampoRequeridoException ex)
            {
                resultado.Mensaje = new Mensaje("E", ex.Message, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar el usuario");
                resultado.Mensaje = new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return resultado;
        }

        public Mensaje listarUsuario(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                string indicador = JsonUtil.Requerido(jObject, "indicador");
                string adm_cliente_id = JsonUtil.Requerido(jObject, "adm_cliente_id");
                string codigo_sociedad = JsonUtil.Requerido(jObject, "codigo_sociedad");
                string codigo_referencia = JsonUtil.Requerido(jObject, "codigo_referencia");
                string denominacion_fiscal = JsonUtil.Requerido(jObject, "denominacion_fiscal");
                string identificacion_fiscal = JsonUtil.Requerido(jObject, "identificacion_fiscal");

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    using (SqlCommand bdComando = new SqlCommand("sp_listarUsuario", bdSql))
                    {
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@indicador", indicador));
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", adm_cliente_id));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", codigo_sociedad));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_referencia", codigo_referencia));
                        bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", denominacion_fiscal));
                        bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", identificacion_fiscal));
                        using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }

                // Defensa en profundidad: nunca devolver la clave aunque el SP la incluya.
                QuitarColumnaClave(dt);

                mensaje.tipo = "S";
                mensaje.mensaje = "";
                mensaje.data = "\"usuarios\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
            }
            catch (CampoRequeridoException ex)
            {
                mensaje = new Mensaje("E", ex.Message, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar usuarios");
                mensaje = new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }

        public Mensaje mantenerUsuario(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            try
            {
                int reg_id = int.Parse(JsonUtil.Requerido(jObject, "reg_id"), NumberStyles.Integer, CultureInfo.InvariantCulture);
                int rol_id = int.Parse(JsonUtil.Requerido(jObject, "rol_id"), NumberStyles.Integer, CultureInfo.InvariantCulture);
                int referencia_id = int.Parse(JsonUtil.Requerido(jObject, "referencia_id"), NumberStyles.Integer, CultureInfo.InvariantCulture);
                string clave = JsonUtil.Opcional(jObject, "clave");

                // Alta: clave obligatoria. Modificación sin clave: el SP conserva la clave actual.
                if (reg_id == 0 && string.IsNullOrEmpty(clave))
                {
                    return new Mensaje("E", "La clave es obligatoria", "");
                }
                object claveParametro = string.IsNullOrEmpty(clave) ? DBNull.Value : PasswordHasher.Hashear(clave);

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    using (SqlCommand bdComando = new SqlCommand("sp_mantenerUsuario", bdSql))
                    {
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@reg_id", reg_id));
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", JsonUtil.Requerido(jObject, "adm_cliente_id")));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_sociedad", JsonUtil.Requerido(jObject, "codigo_sociedad")));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_referencia", JsonUtil.Requerido(jObject, "codigo_referencia")));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_referencia_aux", JsonUtil.Requerido(jObject, "codigo_referencia_aux")));
                        bdComando.Parameters.Add(new SqlParameter("@usuario", JsonUtil.Requerido(jObject, "usuario")));
                        bdComando.Parameters.Add("@clave", SqlDbType.VarChar, 256).Value = claveParametro;
                        bdComando.Parameters.Add(new SqlParameter("@rol_id", rol_id));
                        bdComando.Parameters.Add(new SqlParameter("@denominacion_usuario", JsonUtil.Requerido(jObject, "denominacion_usuario")));
                        bdComando.Parameters.Add(new SqlParameter("@correo_referencia", JsonUtil.Requerido(jObject, "correo_referencia")));
                        bdComando.Parameters.Add(new SqlParameter("@referencia_id", referencia_id));
                        bdComando.Parameters.Add(new SqlParameter("@idioma", JsonUtil.Requerido(jObject, "idioma")));
                        bdComando.Parameters.Add(new SqlParameter("@reg_estado", JsonUtil.Requerido(jObject, "reg_estado")));
                        bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", JsonUtil.Requerido(jObject, "reg_usuario_creacion")));

                        bdComando.ExecuteNonQuery();
                    }
                }

                mensaje.tipo = "S";
                mensaje.mensaje = reg_id == 0 ? "Registrado correctamente" : "Modificado correctamente";
                mensaje.data = "";
            }
            catch (CampoRequeridoException ex)
            {
                mensaje = new Mensaje("E", ex.Message, "");
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Valor numérico inválido al mantener usuario");
                mensaje = new Mensaje("E", "Los campos reg_id, rol_id y referencia_id deben ser numéricos", "");
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                // Violación de clave única / índice único.
                mensaje = new Mensaje("E", "Usuario ya existe", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al mantener usuario");
                mensaje = new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }

        private static void QuitarColumnaClave(DataTable dt)
        {
            if (dt != null && dt.Columns.Contains("clave"))
            {
                dt.Columns.Remove("clave");
            }
        }
    }
}
