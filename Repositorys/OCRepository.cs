using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Net;
using System.Security.AccessControl;
using System.Text.Json.Nodes;

namespace esupplier.Repositorys
{
    public class OCRepository : IOCRepository
    {
        private IConfiguration _configuration;
        private IEmailRepository _emailRepository;
        private IHeaderService _headerService;

        public OCRepository(IConfiguration configuration, IEmailRepository emailRepository, IHeaderService headerService)
        {
            _configuration = configuration;
            _emailRepository = emailRepository;
            _headerService = headerService;
        }

        public Mensaje buscarOC(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            string operador = "";
            string adm_cliente_id = "";
            string sociedad = "";
            string numero_oc = "";
            string fecha_emision_d = "";
            string fecha_emision_h = "";
            string estado_oc = "";
            string codigo_proveedor = "";
            string tipo_oc = "";
            string estado_confirmacion = "";

            DataTable dt = new DataTable();

            try
            {
                if(jObject["operador"].ToString().Equals("B"))
                {
                    operador = "B";
                }
                else if (jObject["operador"].ToString().Equals("O"))
                {
                    operador = "OC";
                }
                else if (jObject["operador"].ToString().Equals("H"))
                {
                    operador = "HC";
                }

                adm_cliente_id = jObject["adm_cliente_id"].ToString();
                sociedad = jObject["sociedad"].ToString();
                numero_oc = jObject["numero_oc"].ToString();
                fecha_emision_d = jObject["fecha_emision_d"].ToString();
                fecha_emision_h = jObject["fecha_emision_h"].ToString();
                estado_oc = jObject["estado_oc"].ToString();
                codigo_proveedor = jObject["codigo_proveedor"].ToString();
                tipo_oc = jObject["tipo_oc"].ToString();
                estado_confirmacion = jObject["estado_confirmacion"].ToString();

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_buscarOC", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@operador", operador));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", adm_cliente_id));
                    bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                    bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                    bdComando.Parameters.Add(new SqlParameter("@fecha_emision_d", fecha_emision_d));
                    bdComando.Parameters.Add(new SqlParameter("@fecha_emision_h", fecha_emision_h));
                    bdComando.Parameters.Add(new SqlParameter("@estado_oc", estado_oc));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", codigo_proveedor));
                    bdComando.Parameters.Add(new SqlParameter("@tipo_oc", tipo_oc));
                    bdComando.Parameters.Add(new SqlParameter("@estado_confirmacion", estado_confirmacion));
                    bdComando.Parameters.Add(new SqlParameter("@idioma", _headerService.obtenerIdioma()));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"cabecera\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }

                    if(operador.Equals("OC") || operador.Equals("HC"))
                    {
                        if (operador.Equals("OC"))
                        {
                            operador = "OD";
                        }
                        else
                        {
                            operador = "HD";
                        }

                        bdComando = new SqlCommand("sp_buscarOC", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@operador", operador));
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", adm_cliente_id));
                        bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                        bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_emision_d", fecha_emision_d));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_emision_h", fecha_emision_h));
                        bdComando.Parameters.Add(new SqlParameter("@estado_oc", estado_oc));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", codigo_proveedor));
                        bdComando.Parameters.Add(new SqlParameter("@tipo_oc", tipo_oc));
                        bdComando.Parameters.Add(new SqlParameter("@estado_confirmacion", estado_confirmacion));
                        bdComando.Parameters.Add(new SqlParameter("@idioma", _headerService.obtenerIdioma()));
                        using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                        {
                            dt = new DataTable();
                            adapter.Fill(dt);
                            mensaje.tipo = "S";
                            mensaje.mensaje = "";
                            mensaje.data = mensaje.data + ",\"detalle\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
                        }
                    }
                    bdSql.Close();
                }
            }
            catch(Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron registros";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje registrarOCHistorico(JObject jObject)
        {
            Mensaje mensaje = new();
            mensaje.tipo = "";

            string validadorOC = "";
            string action_code_ca = "";
            string numero_oc = "";
            string posicion_oc = "";

            try
            {
                JArray jsonArrayC = new();
                jsonArrayC = (JArray)jObject["Cabecera"];

                JArray jsonArrayD = new();
                jsonArrayD = (JArray)jObject["Detalle"];

                using (SqlConnection bdSql = new(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    action_code_ca = Constantes.HISTORY;
                    numero_oc = jsonArrayC[0]["NroOc"].ToString();

                    SqlCommand bdComando = new();

                    try
                    {
                        bdComando = new SqlCommand("sp_mantenerOC", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@action_code", action_code_ca));
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                        bdComando.Parameters.Add(new SqlParameter("@sociedad", jsonArrayC[0]["Sociedad"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                        bdComando.Parameters.Add(new SqlParameter("@tipo_oc", jsonArrayC[0]["TipoOc"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_emision", jsonArrayC[0]["FechaEmision"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_etd", jsonArrayC[0]["FechaEtd"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_produccion", jsonArrayC[0]["FechaProduccion"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_recibido_bodega", jsonArrayC[0]["FechaRecibidoBodega"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@estado_oc", jsonArrayC[0]["Estado"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", jsonArrayC[0]["Proveedor"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", jsonArrayC[0]["Ruc"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", jsonArrayC[0]["RazonSocial"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@moneda", jsonArrayC[0]["Moneda"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@forma_pago", jsonArrayC[0]["FormaPago"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@condicion_pago", jsonArrayC[0]["CondicionPago"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@numero_oferta", jsonArrayC[0]["NroOfer"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@importe_total", Convert.ToDecimal(jsonArrayC[0]["ImmporteTotal"].ToString())));
                        bdComando.Parameters.Add(new SqlParameter("@impuesto_general", Convert.ToDecimal(jsonArrayC[0]["Iva"].ToString())));
                        bdComando.Parameters.Add(new SqlParameter("@importe_final", Convert.ToDecimal(jsonArrayC[0]["ImporteFinal"].ToString())));
                        bdComando.Parameters.Add(new SqlParameter("@observacion", Constantes.NO_INICIA));

                        bdComando.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        bdComando = new("sp_validarOCCreate", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                        bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                        bdComando.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.VarChar, 18));
                        bdComando.Parameters[2].Direction = ParameterDirection.Output;

                        bdComando.ExecuteNonQuery();

                        validadorOC = bdComando.Parameters[2].Value.ToString();

                        if (validadorOC.Equals(numero_oc))
                        {
                            mensaje.tipo = "S";
                            mensaje.mensaje = "OC " + numero_oc +" registrado correctamente posterior a un envio.";
                            mensaje.data = "";
                        }
                        else
                        {
                            mensaje.tipo = "E";
                            mensaje.mensaje = "Error en creacion de Cab. de la OC " + numero_oc + ".";
                            mensaje.data = "";
                        }
                        
                    }

                    if (mensaje.tipo.Equals(""))
                    {
                        using (SqlTransaction transaction = bdSql.BeginTransaction())
                        {
                            bdComando = new();
                            bdComando.Connection = bdSql;
                            bdComando.Transaction = transaction;
                            bdComando.CommandType = CommandType.Text;
                            bdComando.CommandText = "insert into tb_orden_compra_det " +
                                "(adm_cliente_id,numero_oc,posicion_oc,cod_material,des_material," +
                                "um_material,cantidad,cantidad_reparto,cantidad_por_entregar,cantidad_entregada," +
                                "precio,total,simbolo_moneda,numero_solped,posicion_solped,fecha_entrega_solped,fecha_entrega," +
                                "fecha_entrega_estadistica,estado_posicion_oc,reg_estado,reg_fecha_creacion," +
                                "reg_usuario_creacion)" +
                                "values (@adm_cliente_id,@numero_oc,@posicion_oc,@cod_material,@des_material," +
                                "@um_material,@cantidad,@cantidad_reparto,@cantidad_por_entregar,@cantidad_entregada," +
                                "@precio,@total,@simbolo_moneda,@numero_solped,@posicion_solped,@fecha_entrega_solped," +
                                "@fecha_entrega,@fecha_entrega_estadistica,@estado_posicion_oc,'1',getdate(),'2')";

                            bdComando.Parameters.Add("@adm_cliente_id", SqlDbType.VarChar, 12);
                            bdComando.Parameters.Add("@numero_oc", SqlDbType.VarChar, 18);
                            bdComando.Parameters.Add("@posicion_oc", SqlDbType.VarChar, 12);
                            bdComando.Parameters.Add("@cod_material", SqlDbType.VarChar, 18);
                            bdComando.Parameters.Add("@des_material", SqlDbType.VarChar, 250);
                            bdComando.Parameters.Add("@um_material", SqlDbType.VarChar, 12);
                            bdComando.Parameters.Add("@cantidad", SqlDbType.Decimal);
                            bdComando.Parameters.Add("@cantidad_reparto", SqlDbType.Decimal);
                            bdComando.Parameters.Add("@cantidad_por_entregar", SqlDbType.Decimal);
                            bdComando.Parameters.Add("@cantidad_entregada", SqlDbType.Decimal);
                            bdComando.Parameters.Add("@precio", SqlDbType.Decimal);
                            bdComando.Parameters.Add("@total", SqlDbType.Decimal);
                            bdComando.Parameters.Add("@simbolo_moneda", SqlDbType.VarChar, 1);
                            bdComando.Parameters.Add("@numero_solped", SqlDbType.VarChar, 18);
                            bdComando.Parameters.Add("@posicion_solped", SqlDbType.VarChar, 12);
                            bdComando.Parameters.Add("@fecha_entrega_solped", SqlDbType.VarChar, 10);
                            bdComando.Parameters.Add("@fecha_entrega", SqlDbType.VarChar, 10);
                            bdComando.Parameters.Add("@fecha_entrega_estadistica", SqlDbType.VarChar, 10);
                            bdComando.Parameters.Add("@estado_posicion_oc", SqlDbType.VarChar, 18);

                            try
                            {
                                for (int i = 0; i < jsonArrayD.Count; i++)
                                {
                                    posicion_oc = jsonArrayD[i]["Posicion"].ToString();

                                    bdComando.Parameters["@adm_cliente_id"].Value = Constantes.ADM_CLIENTE;
                                    bdComando.Parameters["@numero_oc"].Value = numero_oc;
                                    bdComando.Parameters["@posicion_oc"].Value = posicion_oc;
                                    bdComando.Parameters["@cod_material"].Value = jsonArrayD[i]["Material"].ToString();
                                    bdComando.Parameters["@des_material"].Value = jsonArrayD[i]["Descripcion"].ToString();
                                    bdComando.Parameters["@um_material"].Value = jsonArrayD[i]["Unidadmedida"].ToString();
                                    bdComando.Parameters["@cantidad"].Value = Convert.ToDecimal(jsonArrayD[i]["Cantidad"].ToString());
                                    bdComando.Parameters["@cantidad_reparto"].Value = Convert.ToDecimal(jsonArrayD[i]["CantidadReparto"].ToString());
                                    bdComando.Parameters["@cantidad_por_entregar"].Value = Convert.ToDecimal(jsonArrayD[i]["CantidadPorEntregar"].ToString());
                                    bdComando.Parameters["@cantidad_entregada"].Value = Convert.ToDecimal(jsonArrayD[i]["CantidadEntregada"].ToString());
                                    bdComando.Parameters["@precio"].Value = Convert.ToDecimal(jsonArrayD[i]["Precio"].ToString());
                                    bdComando.Parameters["@total"].Value = Convert.ToDecimal(jsonArrayD[i]["Total"].ToString());
                                    bdComando.Parameters["@simbolo_moneda"].Value = jsonArrayD[i]["SimboloMoneda"].ToString();
                                    bdComando.Parameters["@numero_solped"].Value = jsonArrayD[i]["Solped"].ToString();
                                    bdComando.Parameters["@posicion_solped"].Value = jsonArrayD[i]["PosicionSolped"].ToString();
                                    bdComando.Parameters["@fecha_entrega_solped"].Value = jsonArrayD[i]["FechaEntregaSolped"].ToString();
                                    bdComando.Parameters["@fecha_entrega"].Value = jsonArrayD[i]["FechaEntregaPosicion"].ToString();
                                    bdComando.Parameters["@fecha_entrega_estadistica"].Value = jsonArrayD[i]["FechaEntregaEstadistica"].ToString();
                                    bdComando.Parameters["@estado_posicion_oc"].Value = jsonArrayD[i]["Estado"].ToString();

                                    bdComando.ExecuteNonQuery();
                                }

                                transaction.Commit();
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();

                                bdComando = new("sp_eliminarOC", bdSql);
                                bdComando.CommandType = CommandType.StoredProcedure;
                                bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));

                                bdComando.ExecuteNonQuery();

                                mensaje.tipo = "E";
                                mensaje.mensaje = "Error en creacion de Pos. " + posicion_oc + " de la OC " + numero_oc + ".";
                                mensaje.data = "";
                            }
                        }
                    }

                    bdSql.Close();
                }

                if (mensaje.tipo.Equals(""))
                {
                    mensaje.tipo = "S";
                    mensaje.mensaje = "OC " + numero_oc + " registrado correctamente.";
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

        public Mensaje registrarOC(JObject jObject)
        {
            Mensaje mensaje = new();
            mensaje.tipo = "";

            string validadorOC = "";
            string action_code_ca = "";
            string numero_oc = "";
            string posicion_oc = "";
            string correo_not = "";

            try
            {
                JArray jsonArrayC = new();
                jsonArrayC = (JArray)jObject["Cabecera"];

                JArray jsonArrayD = new();
                jsonArrayD = (JArray)jObject["Detalle"];

                using (SqlConnection bdSql = new(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    action_code_ca = jsonArrayC[0]["ActionCode"].ToString();
                    numero_oc = jsonArrayC[0]["NroOc"].ToString();

                    SqlCommand bdComando = new();

                    try
                    {
                        bdComando = new SqlCommand("sp_mantenerOC", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@action_code", action_code_ca));
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                        bdComando.Parameters.Add(new SqlParameter("@sociedad", jsonArrayC[0]["Sociedad"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                        bdComando.Parameters.Add(new SqlParameter("@tipo_oc", jsonArrayC[0]["TipoOc"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_emision", jsonArrayC[0]["FechaEmision"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_etd", jsonArrayC[0]["FechaEtd"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_produccion", jsonArrayC[0]["FechaProduccion"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_recibido_bodega", jsonArrayC[0]["FechaRecibidoBodega"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@estado_oc", jsonArrayC[0]["Estado"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", jsonArrayC[0]["Proveedor"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", jsonArrayC[0]["Ruc"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", jsonArrayC[0]["RazonSocial"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@moneda", jsonArrayC[0]["Moneda"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@forma_pago", jsonArrayC[0]["FormaPago"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@condicion_pago", jsonArrayC[0]["CondicionPago"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@numero_oferta", jsonArrayC[0]["NroOfer"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@importe_total", Convert.ToDecimal(jsonArrayC[0]["ImmporteTotal"].ToString())));
                        bdComando.Parameters.Add(new SqlParameter("@impuesto_general", Convert.ToDecimal(jsonArrayC[0]["Iva"].ToString())));
                        bdComando.Parameters.Add(new SqlParameter("@importe_final", Convert.ToDecimal(jsonArrayC[0]["ImporteFinal"].ToString())));
                        if (action_code_ca.Equals("APPROVE") || action_code_ca.Equals("REFUSED"))
                        {
                            bdComando.Parameters.Add(new SqlParameter("@observacion", jsonArrayC[0]["Observacion"].ToString()));
                        }
                        else
                        {
                            bdComando.Parameters.Add(new SqlParameter("@observacion", ""));
                        }

                        bdComando.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        if (action_code_ca.Equals("CREATE"))
                        {
                            bdComando = new("sp_validarOCCreate", bdSql);
                            bdComando.CommandType = CommandType.StoredProcedure;
                            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                            bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                            bdComando.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.VarChar, 18));
                            bdComando.Parameters[2].Direction = ParameterDirection.Output;

                            bdComando.ExecuteNonQuery();

                            validadorOC = bdComando.Parameters[2].Value.ToString();

                            if (validadorOC.Equals(numero_oc))
                            {
                                mensaje.tipo = "S";
                                mensaje.mensaje = "OC " + numero_oc + " registrado correctamente posterior a un envio.";
                                mensaje.data = "";
                            }
                            else
                            {
                                mensaje.tipo = "E";
                                mensaje.mensaje = "Error en creacion de Cab. de la OC " + numero_oc + ".";
                                mensaje.data = "";
                            }
                        }
                        else
                        {
                            mensaje.tipo = "E";
                            mensaje.mensaje = "Error en actualizacion de Cab. de la OC " + numero_oc + ".";
                            mensaje.data = "";
                        }

                    }

                    if (mensaje.tipo.Equals(""))
                    {
                        for (int i = 0; i < jsonArrayD.Count; i++)
                        {
                            posicion_oc = jsonArrayD[i]["Posicion"].ToString();

                            try
                            {
                                bdComando = new("sp_mantenerOCDet", bdSql);
                                bdComando.CommandType = CommandType.StoredProcedure;
                                bdComando.Parameters.Add(new SqlParameter("@action_code", jsonArrayD[i]["ActionCode"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                                bdComando.Parameters.Add(new SqlParameter("@posicion_oc", posicion_oc));
                                bdComando.Parameters.Add(new SqlParameter("@cod_material", jsonArrayD[i]["Material"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@des_material", jsonArrayD[i]["Descripcion"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@um_material", jsonArrayD[i]["Unidadmedida"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@cantidad", Convert.ToDecimal(jsonArrayD[i]["Cantidad"].ToString())));
                                bdComando.Parameters.Add(new SqlParameter("@cantidad_reparto", Convert.ToDecimal(jsonArrayD[i]["CantidadReparto"].ToString())));
                                bdComando.Parameters.Add(new SqlParameter("@cantidad_por_entregar", Convert.ToDecimal(jsonArrayD[i]["CantidadPorEntregar"].ToString())));
                                bdComando.Parameters.Add(new SqlParameter("@cantidad_entregada", Convert.ToDecimal(jsonArrayD[i]["CantidadEntregada"].ToString())));
                                bdComando.Parameters.Add(new SqlParameter("@precio", Convert.ToDecimal(jsonArrayD[i]["Precio"].ToString())));
                                bdComando.Parameters.Add(new SqlParameter("@total", Convert.ToDecimal(jsonArrayD[i]["Total"].ToString())));
                                bdComando.Parameters.Add(new SqlParameter("@simbolo_moneda", jsonArrayD[i]["SimboloMoneda"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@numero_solped", jsonArrayD[i]["Solped"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@posicion_solped", jsonArrayD[i]["PosicionSolped"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@fecha_entrega_solped", jsonArrayD[i]["FechaEntregaSolped"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@fecha_entrega", jsonArrayD[i]["FechaEntregaPosicion"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@fecha_entrega_estadistica", jsonArrayD[i]["FechaEntregaEstadistica"].ToString()));
                                bdComando.Parameters.Add(new SqlParameter("@estado_posicion_oc", jsonArrayD[i]["Estado"].ToString()));

                                bdComando.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {

                                if (action_code_ca.Equals("CREATE"))
                                {
                                    i = jsonArrayD.Count();

                                    bdComando = new("sp_eliminarOC", bdSql);
                                    bdComando.CommandType = CommandType.StoredProcedure;
                                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                    bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));

                                    bdComando.ExecuteNonQuery();

                                    mensaje.tipo = "E";
                                    mensaje.mensaje = "Error en creacion de Pos. " + posicion_oc + " de la OC " + numero_oc + ".";
                                    mensaje.data = "";
                                }
                                else
                                {
                                    mensaje.tipo = "E";
                                    mensaje.mensaje = "Error en actualizacion de Pos. " + posicion_oc + " de la OC " + numero_oc + ".";
                                    mensaje.data = "";
                                }

                            }
                        }
                    }

                    if (action_code_ca.Equals("REFUSED"))
                    {
                        string proveedor = jsonArrayC[0]["Proveedor"].ToString();
                        string sociedad = jsonArrayC[0]["Sociedad"].ToString();

                        bdComando = new("sp_obtenerCorreoNot", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                        bdComando.Parameters.Add(new SqlParameter("@action_code", Constantes.ACTION_REFUSED));
                        bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                        bdComando.Parameters.Add(new SqlParameter("@proveedor", proveedor)); 
                        bdComando.Parameters.Add(new SqlParameter("@correo", SqlDbType.VarChar, -1));
                        bdComando.Parameters[4].Direction = ParameterDirection.Output;

                        bdComando.ExecuteNonQuery();

                        correo_not = bdComando.Parameters[4].Value.ToString();
                    }

                    bdSql.Close();
                }

                if (mensaje.tipo.Equals(""))
                {
                    if (action_code_ca.Equals("CREATE"))
                    {
                        mensaje.tipo = "S";
                        mensaje.mensaje = "OC " + numero_oc + " registrado correctamente.";
                        mensaje.data = "";
                    }
                    else
                    {
                        mensaje.tipo = "S";
                        mensaje.mensaje = "OC " + numero_oc + " actualizado correctamente.";
                        mensaje.data = "";
                    }

                    if (action_code_ca.Equals("REFUSED"))
                    {
                        string sociedad = jsonArrayC[0]["Sociedad"].ToString();
                        string proveedor = jsonArrayC[0]["Proveedor"].ToString();
                        string observacion = jsonArrayC[0]["Observacion"].ToString();
                        string mensajeRechazo = ConsultaUtil.generarCorreoRechazo(numero_oc, proveedor, observacion);
                        string asunto = "Nro OC " + numero_oc + " RECHAZADO.";
                        _emailRepository.SendEmail(sociedad, correo_not, asunto, mensajeRechazo);
                    }
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

        public Mensaje leerOC(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            try
            {
                JArray jsonArray = new JArray();
                jsonArray = (JArray)jObject["leidos"];

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    for (int i = 0; i < jsonArray.Count; i++)
                    {
                        SqlCommand bdComando = new SqlCommand("sp_leerOC", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", jsonArray[i]["adm_cliente_id"].ToString()));
                        bdComando.Parameters.Add(new SqlParameter("@reg_id", Convert.ToInt32(jsonArray[i]["reg_id"].ToString())));
                        bdComando.ExecuteNonQuery();
                    }

                    bdSql.Close();
                }

                mensaje.tipo = "S";
                mensaje.mensaje = "oc leidos correctamente";
                mensaje.data = "";
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se pudo leer el registro";
                mensaje.data = "";
                throw new InvalidOperationException(ex.Message);
            }

            return mensaje;
        }

        public Mensaje obtenerTipoOC()
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_obtenerTipoOC", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"tipoOC\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
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

        public Mensaje registrarArchivoOc(string numero_oc, string ruta, string nombre_original, string nombre_registro, string tipo, string sociedad, string reg_usuario)
        {
            Mensaje mensaje = new Mensaje();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_insertarArchivoOC", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                    bdComando.Parameters.Add(new SqlParameter("@ruta", ruta));
                    bdComando.Parameters.Add(new SqlParameter("@nombre_original", nombre_original));
                    bdComando.Parameters.Add(new SqlParameter("@nombre_registro", nombre_registro));
                    bdComando.Parameters.Add(new SqlParameter("@tipo", tipo));
                    bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                    bdComando.Parameters.Add(new SqlParameter("@reg_usuario_creacion", reg_usuario));

                    bdComando.ExecuteNonQuery();
                    
                    mensaje.tipo = "S";
                    mensaje.mensaje = "";
                    

                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = ex.Message.ToString();
                mensaje.data = "";
                throw new InvalidOperationException(ex.Message);
            }

            return mensaje;
        }

        public Mensaje buscarArchivoOc(int id)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_buscarPorIdArchivoOC", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@id", id));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"archivo\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
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

        public Mensaje listarArchivoOc(string oc, string sociedad,bool ruta)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_listarArchivoOC", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@numero_oc", oc));
                    bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                    bdComando.Parameters.Add(new SqlParameter("@ruta", ruta? 1:0));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = "\"archivo\":" + JsonConvert.SerializeObject(dt, Formatting.Indented);
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

        public Mensaje eliminarArchivoOc(int id,string usuario)
        {
            Mensaje mensaje = new Mensaje();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_eliminarPorIdArchivoOC", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@id", id));
                    bdComando.Parameters.Add(new SqlParameter("@usuario_mod", usuario));

                    bdComando.ExecuteNonQuery();

                    mensaje.tipo = "S";
                    mensaje.mensaje = "eliminado correctamente";


                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = ex.Message.ToString();
                mensaje.data = "";
                throw new InvalidOperationException(ex.Message);
            }

            return mensaje;
        }

        public Mensaje notificarOCPendiente()
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();
            JArray jsonArrayOC = new JArray();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {

                    bdSql.Open();

                    SqlCommand bdComando = new SqlCommand("sp_validarOCPendiente", bdSql);
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE)); 
                    bdComando.CommandType = CommandType.StoredProcedure;
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        string data = JsonConvert.SerializeObject(dt, Formatting.Indented);
                        jsonArrayOC = JArray.Parse(data);

                        for (int i = 0; i < jsonArrayOC.Count; i++)
                        {
                            string mensajeOCPendiente = ConsultaUtil.generarCorreoOCPendiente(jsonArrayOC[i]["numero_oc"].ToString(),
                                jsonArrayOC[i]["denominacion_fiscal"].ToString(),
                                jsonArrayOC[i]["fecha_registro"].ToString(),
                                _configuration["Web:url"],
                                jsonArrayOC[i]["idioma"].ToString());

                            string asunto = jsonArrayOC[i]["denominacion_sociedad"].ToString() + ": Nro OC " + jsonArrayOC[i]["numero_oc"].ToString();
                            if (jsonArrayOC[i]["idioma"].ToString().Equals("ES"))
                            {
                                asunto = asunto + " PENDIENTE.";
                            }
                            else
                            {
                                asunto = asunto + " PENDING.";
                            }

                            _emailRepository.SendEmail(jsonArrayOC[i]["codigo_sociedad"].ToString(),
                                jsonArrayOC[i]["correo"].ToString(),
                                asunto,
                                mensajeOCPendiente);
                        }
                    }

                    bdComando = new SqlCommand("sp_actualizarLogNotificaciones", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));

                    bdComando.ExecuteNonQuery();

                    mensaje.tipo = "S";
                    mensaje.mensaje = "Correos enviados con éxito";

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

        public Mensaje obtenerOC(JObject jObject)
        {
            try
            {
                var bodyRequest = ConstruirPayload(jObject);
                var responseBody = LlamarApiExtern(bodyRequest);

                var dataApi = JObject.Parse(responseBody);
                var ocCabApi = (JArray)dataApi["ET_OC_CAB"]["item"];
                var ocDetApi = jObject["operador"].ToString() == "O"
                    ? (JArray)dataApi["ET_OC_DET"]["item"]
                    : new JArray();

                var mensajeSP = obtenerOCCabecera(jObject);
                var ocCabSPDict = mensajeSP.tipo == "S"
                    ? ConstruirDiccionarioSP(JArray.Parse(mensajeSP.data))
                    : new Dictionary<string, JObject>();

                var cabecera = MapearCabeceras(ocCabApi, ocCabSPDict);
                var detalle = MapearDetalles(ocDetApi, ocCabApi);


                var cabeceraRechazada = cabecera.Where(x => x["estado_confirmacion"]?.ToString() == "Rechazado").ToList();

                if (cabeceraRechazada.Count > 0)
                {
                    foreach (var item in cabeceraRechazada.Where(i => !string.IsNullOrEmpty(i["numero_oc"]?.ToString())))
                    {
                        var numeroOC = item["numero_oc"]!.ToString();
                        var jObjectHist = new JObject
                        {
                            ["numero_oc"] = numeroOC,
                            ["estado_confirmacion"] = "Rechazado"
                        };

                        var respuestaHistorico = obtenerOCHistorico(jObjectHist);

                        if (respuestaHistorico.tipo != "S" || string.IsNullOrEmpty(respuestaHistorico.data))
                            continue;

                        var cabeceraHist = JObject.Parse("{" + respuestaHistorico.data + "}")["cabecera"] as JArray;
                        if (cabeceraHist == null || cabeceraHist.Count == 0)
                            continue;

                        var primerItemHist = cabeceraHist[0];

                        if (!DateTime.TryParse(primerItemHist["reg_fecha_creacion"]?.ToString(), out DateTime fechaCreacion))
                            continue;
                        if (!DateTime.TryParse(item["fecha_confirmacion"]?.ToString(), out DateTime fechaConfirmacion))
                            continue;
                        if (fechaCreacion > fechaConfirmacion)
                            item["estado_confirmacion"] = "Pendiente";
                    }
                }

                var cabeceraPendiente = cabecera.Where(x => x["estado_confirmacion"]?.ToString() == "Pendiente" && x["reg_id"]?.ToString() == "0").ToList();
                JArray jArrayOC = JArray.FromObject(cabeceraPendiente);
                if (jArrayOC.Count() > 0)
                {
                    registrarOCCabecera(jArrayOC);
                }

                return new Mensaje
                {
                    tipo = "S",
                    mensaje = "",
                    data = $"\"cabecera\":{cabecera},\"detalle\":{detalle}"
                };
            }
            catch (Exception ex)
            {
                return new Mensaje
                {
                    tipo = "E",
                    mensaje = "Error general: " + ex.Message,
                    data = ""
                };
            }
        }


        private JObject ConstruirPayload(JObject jObject)
        {
            return new JObject
            {
                ["ET_OC_CAB"] = new JArray(),
                ["ET_OC_DET"] = new JArray(),
                ["I_TAXNUM"] = Constantes.NO_INICIA,
                ["I_LIFNR"] = jObject["codigo_proveedor"].ToString(),
                ["I_ESTADO"] = jObject["estado_oc"].ToString(),
                ["I_FEC_INI"] = jObject["fecha_emision_d"].ToString(),
                ["I_FEC_FIN"] = jObject["fecha_emision_h"].ToString(),
                ["I_EBELN"] = jObject["numero_oc"].ToString(),
                ["I_BUKRS"] = jObject["sociedad"].ToString(),
                ["I_BSART"] = jObject["tipo_oc"].ToString(),
                ["I_VER_DETALLE"] = jObject["operador"].ToString() == "O"
                    ? Constantes.INDICADOR_DETALLE
                    : Constantes.NO_INICIA
            };
        }

        private string LlamarApiExtern(JObject jsonBody)
        {
            var url = _configuration["Http:ConsultarOC"];
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers.Add("Authorization", _configuration["Http:Autorizacion"]);

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(jsonBody.ToString());
            }

            using (var response = request.GetResponse())
            using (var strReader = response.GetResponseStream())
            using (var objReader = new StreamReader(strReader))
            {
                return objReader.ReadToEnd();
            }
        }

        private Dictionary<string, JObject> ConstruirDiccionarioSP(JArray dataSP)
        {
            return dataSP
                .OfType<JObject>()
                .GroupBy(x => x["numero_oc"].ToString())
                .ToDictionary(g => g.Key, g => g.First());
        }

        private JArray MapearCabeceras(JArray cabecerasApi, Dictionary<string, JObject> datosSP)
        {
            var resultado = new JArray();
            foreach (JObject itemApi in cabecerasApi)
            {
                string numeroOC = itemApi["EBELN"].ToString();
                var item = new JObject
                {
                    ["adm_cliente_id"] = Constantes.ADM_CLIENTE,
                    ["numero_oc"] = numeroOC,
                    ["tipo_oc"] = itemApi["BSART"],
                    ["fecha_emision"] = itemApi["FEC_EMISION"],
                    ["fecha_etd"] = itemApi["FEC_ETD"],
                    ["fecha_produccion"] = itemApi["FEC_PRODUCCION"],
                    ["fecha_recibido_bodega"] = itemApi["FEC_ETA"],
                    ["estado_oc"] = itemApi["ESTADO"],
                    ["codigo_proveedor"] = itemApi["LIFNR"],
                    ["identificacion_fiscal"] = itemApi["TAXNUM"],
                    ["denominacion_fiscal"] = itemApi["NAME1"],
                    ["moneda"] = itemApi["WAERS"],
                    ["forma_pago"] = itemApi["CONDICION_PAGO"],
                    ["numero_oferta"] = itemApi["NUMERO_OFERTA"],
                    ["importe_total"] = itemApi["IMPORTE_TOTAL"],
                    ["impuesto_general"] = itemApi["IMPUESTO_GENERAL"],
                    ["importe_final"] = itemApi["IMPORTE_FINAL"]
                };

                if (datosSP.TryGetValue(numeroOC, out var datos))
                {
                    item["reg_estado"] = Convert.ToInt32(datos["reg_estado"]);
                    item["reg_id"] = Convert.ToInt32(datos["reg_id"]);
                    item["estado_confirmacion"] = datos["estado_confirmacion"];
                    item["observacion_confirmacion"] = datos["observacion_confirmacion"];
                    item["archivo"] = datos["archivo"];
                    item["fecha_confirmacion"] = datos["fecha_confirmacion"];
                    item["reg_fecha_creacion"] = datos["reg_fecha_creacion"];
                }
                else
                {
                    item["reg_estado"] = 2;
                    item["reg_id"] = 0;
                    item["estado_confirmacion"] = "Pendiente";
                    item["observacion_confirmacion"] = "";
                    item["archivo"] = "";
                    item["fecha_confirmacion"] = "";
                    item["reg_fecha_creacion"] = itemApi["FEC_EMISION"];
                }

                resultado.Add(item);
            }

            return resultado;
        }

        private JArray MapearDetalles(JArray detallesApi, JArray cabeceraApi)
        {
            if (detallesApi == null || detallesApi.Count == 0) return new JArray();
            string moneda = cabeceraApi.First?["WAERS"]?.ToString() ?? "";
            return new JArray(
                detallesApi.Select(item => new JObject
                {
                    ["numero_oc"] = item["EBELN"],
                    ["posicion_oc"] = item["EBELP"],
                    ["cod_material"] = item["MATNR"],
                    ["des_material"] = item["TXZ01"],
                    ["um_material"] = item["MEINS"],
                    ["cantidad"] = item["MENGE"],
                    ["cantidad_reparto"] = item["MENGE"],
                    ["cantidad_por_entregar"] = item["POR_ENTREGAR"],
                    ["cantidad_entregada"] = item["WEMNG"],
                    ["precio"] = item["NETPR"],
                    ["total"] = item["NETWR"],
                    ["simbolo_moneda"] = moneda,
                    ["numero_solped"] = item["BANFN"],
                    ["posicion_solped"] = item["BNFPO"],
                    ["fecha_entrega_solped"] = item["EINDT"],
                    ["fecha_entrega"] = item["LFDAT"],
                    ["fecha_entrega_estadistica"] = item["SLFDT"],
                    ["estado_posicion_oc"] = item["ESTADO"]
                })
            );
        }


        public Mensaje obtenerOCHistorico(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            JObject objectAUX = new JObject();
            objectAUX.Add("ET_OC_HIS_CAB", new JArray());
            objectAUX.Add("ET_OC_HIS_DET", new JArray());
            objectAUX.Add("I_EBELN", jObject["numero_oc"].ToString());
            objectAUX.Add("I_VER_DETALLE", jObject["estado_confirmacion"]?.ToString() == "Rechazado" ? "9" : "1");


            var url = _configuration["Http:ConsultarOCHistorico"];
            var request = (HttpWebRequest)WebRequest.Create(url);
            string json = objectAUX.ToString();
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers.Add("Authorization", _configuration["Http:Autorizacion"]);

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream strReader = response.GetResponseStream())
                    {
                        if (strReader != null)
                        {
                            using (StreamReader objReader = new StreamReader(strReader))
                            {
                                string responseBody = objReader.ReadToEnd();
                                JObject objJsonAux = JObject.Parse(responseBody);

                                Console.WriteLine(responseBody);

                                mensaje.tipo = "S";
                                mensaje.mensaje = "";

                                try
                                {
                                    JArray jArrayETOCCAB = new JArray();
                                    jArrayETOCCAB = (JArray)objJsonAux["ET_OC_HIS_CAB"]["item"];

                                    JArray jArrayOCCAB = new JArray();

                                    for (int i = 0; i < jArrayETOCCAB.Count; i++)
                                    {
                                        JObject objectOCCAB = new JObject();
                                        objectOCCAB.Add("adm_cliente_id", Constantes.ADM_CLIENTE);
                                        objectOCCAB.Add("reg_version", jArrayETOCCAB[i]["VERSION"].ToString());
                                        objectOCCAB.Add("numero_oc", jArrayETOCCAB[i]["EBELN"].ToString());
                                        objectOCCAB.Add("tipo_oc", jArrayETOCCAB[i]["BSART"].ToString());
                                        objectOCCAB.Add("fecha_emision", jArrayETOCCAB[i]["FEC_EMISION"].ToString());
                                        objectOCCAB.Add("fecha_etd", jArrayETOCCAB[i]["FEC_ETD"].ToString());
                                        objectOCCAB.Add("fecha_produccion", jArrayETOCCAB[i]["FEC_PRODUCCION"].ToString());
                                        objectOCCAB.Add("fecha_recibido_bodega", jArrayETOCCAB[i]["FEC_ETA"].ToString());
                                        objectOCCAB.Add("estado_oc", jArrayETOCCAB[i]["ESTADO"].ToString());
                                        objectOCCAB.Add("codigo_proveedor", jArrayETOCCAB[i]["LIFNR"].ToString());
                                        objectOCCAB.Add("identificacion_fiscal", jArrayETOCCAB[i]["TAXNUM"].ToString());
                                        objectOCCAB.Add("denominacion_fiscal", jArrayETOCCAB[i]["NAME1"].ToString());
                                        objectOCCAB.Add("moneda", jArrayETOCCAB[i]["WAERS"].ToString());
                                        objectOCCAB.Add("forma_pago", jArrayETOCCAB[i]["CONDICION_PAGO"].ToString());
                                        objectOCCAB.Add("numero_oferta", jArrayETOCCAB[i]["NUMERO_OFERTA"].ToString());
                                        objectOCCAB.Add("importe_total", jArrayETOCCAB[i]["IMPORTE_TOTAL"].ToString());
                                        objectOCCAB.Add("impuesto_general", jArrayETOCCAB[i]["IMPUESTO_GENERAL"].ToString());
                                        objectOCCAB.Add("importe_final", jArrayETOCCAB[i]["IMPORTE_FINAL"].ToString());
                                        objectOCCAB.Add("reg_fecha_creacion", jArrayETOCCAB[i]["UDATE"].ToString() + "T" + jArrayETOCCAB[i]["UTIME"].ToString() + ".00");
                                        objectOCCAB.Add("reg_estado", 1); 
                                        objectOCCAB.Add("estado_confirmacion", "");
                                        objectOCCAB.Add("observacion_confirmacion", "");
                                        objectOCCAB.Add("fecha_confirmacion", "");
                                        jArrayOCCAB.Add(objectOCCAB);
                                    }

                                    mensaje.data = "\"cabecera\":" + jArrayOCCAB.ToString();

                                    JArray jArrayETOCDET = new JArray();
                                    JArray jArrayOCDET = new JArray();

                                    if (objJsonAux["ET_OC_HIS_DET"] != null &&
                                        objJsonAux["ET_OC_HIS_DET"].Type == JTokenType.Object &&
                                        objJsonAux["ET_OC_HIS_DET"]["item"] != null &&
                                        objJsonAux["ET_OC_HIS_DET"]["item"].Type == JTokenType.Array)
                                    {
                                        jArrayETOCDET = (JArray)objJsonAux["ET_OC_HIS_DET"]["item"];

                                        for (int i = 0; i < jArrayETOCDET.Count; i++)
                                        {
                                            JObject objectOCDET = new JObject();
                                            objectOCDET.Add("reg_version", jArrayETOCDET[i]["VERSION"].ToString());
                                            objectOCDET.Add("numero_oc", jArrayETOCDET[i]["EBELN"].ToString());
                                            objectOCDET.Add("posicion_oc", jArrayETOCDET[i]["EBELP"].ToString());
                                            objectOCDET.Add("cod_material", jArrayETOCDET[i]["MATNR"].ToString());
                                            objectOCDET.Add("des_material", jArrayETOCDET[i]["TXZ01"].ToString());
                                            objectOCDET.Add("um_material", jArrayETOCDET[i]["MEINS"].ToString());
                                            objectOCDET.Add("cantidad", jArrayETOCDET[i]["MENGE"].ToString());
                                            objectOCDET.Add("cantidad_reparto", jArrayETOCDET[i]["MENGE"].ToString());
                                            objectOCDET.Add("cantidad_por_entregar", jArrayETOCDET[i]["POR_ENTREGAR"].ToString());
                                            objectOCDET.Add("cantidad_entregada", jArrayETOCDET[i]["WEMNG"].ToString());
                                            objectOCDET.Add("precio", jArrayETOCDET[i]["NETPR"].ToString());
                                            objectOCDET.Add("total", jArrayETOCDET[i]["NETWR"].ToString());
                                            objectOCDET.Add("simbolo_moneda", jArrayETOCCAB[0]["WAERS"].ToString());
                                            objectOCDET.Add("numero_solped", jArrayETOCDET[i]["BANFN"].ToString());
                                            objectOCDET.Add("posicion_solped", jArrayETOCDET[i]["BNFPO"].ToString());
                                            objectOCDET.Add("fecha_entrega_solped", jArrayETOCDET[i]["EINDT"].ToString());
                                            objectOCDET.Add("fecha_entrega", jArrayETOCDET[i]["LFDAT"].ToString());
                                            objectOCDET.Add("fecha_entrega_estadistica", jArrayETOCDET[i]["SLFDT"].ToString());
                                            objectOCDET.Add("estado_posicion_oc", jArrayETOCDET[i]["ESTADO"].ToString());
                                            jArrayOCDET.Add(objectOCDET);
                                        }
                                    }

                                    mensaje.data = mensaje.data + ",\"detalle\":" + jArrayOCDET.ToString();

                                }
                                catch (Exception ex)
                                {
                                    mensaje.tipo = "E";
                                    mensaje.mensaje = "No se encontraron registros";
                                    mensaje.data = "";
                                }

                            }
                        }
                        else
                        {
                            mensaje.tipo = "E";
                            mensaje.mensaje = "Sin respuesta - null";
                            mensaje.data = "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encuntraron comprobantes";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje obtenerOCCabecera(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            string operador = "";

            DataTable dt = new DataTable();

            try
            {
                if (jObject["operador"].ToString().Equals("B"))
                {
                    operador = "B";
                }
                else if (jObject["operador"].ToString().Equals("O"))
                {
                    operador = "OC";
                }
                else if (jObject["operador"].ToString().Equals("H"))
                {
                    operador = "HC";
                }

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_buscarOC", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@operador", operador));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                    bdComando.Parameters.Add(new SqlParameter("@sociedad", jObject["sociedad"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@numero_oc", jObject["numero_oc"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@fecha_emision_d", jObject["fecha_emision_d"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@fecha_emision_h", jObject["fecha_emision_h"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@estado_oc", jObject["estado_oc"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", jObject["codigo_proveedor"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@tipo_oc", jObject["tipo_oc"].ToString()));
                    bdComando.Parameters.Add(new SqlParameter("@estado_confirmacion", jObject["estado_confirmacion"].ToString()));
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

        public void registrarOCCabecera(JArray jArrayOC)
        {
            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_mantenerOC_cabecera", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@p_json", jArrayOC.ToString()));
                    bdComando.ExecuteNonQuery();
                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
            }
        }

    }


}
