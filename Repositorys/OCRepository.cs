using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace esupplier.Repositorys
{
    public class OCRepository : IOCRepository
    {
        // Máximo de consultas simultáneas a SAP para el histórico de OC rechazadas
        private const int MaxConsultasSapConcurrentes = 5;

        private readonly IConfiguration _configuration;
        private readonly IEmailRepository _emailRepository;
        private readonly IHeaderService _headerService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OCRepository> _logger;

        public OCRepository(IConfiguration configuration, IEmailRepository emailRepository, IHeaderService headerService,
            IHttpClientFactory httpClientFactory, ILogger<OCRepository> logger)
        {
            _configuration = configuration;
            _emailRepository = emailRepository;
            _headerService = headerService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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
                operador = TraducirOperador(jObject["operador"].ToString());

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

                    if (operador.Equals("OC") || operador.Equals("HC"))
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en buscarOC");
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
                JArray jsonArrayC = jObject?["Cabecera"] as JArray;
                JArray jsonArrayD = jObject?["Detalle"] as JArray ?? new JArray();
                if (jsonArrayC == null || jsonArrayC.Count == 0)
                {
                    return Error("Falta el campo obligatorio 'Cabecera'.");
                }

                using (SqlConnection bdSql = new(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    action_code_ca = Constantes.HISTORY;
                    numero_oc = Texto(jsonArrayC[0], "NroOc");

                    SqlCommand bdComando = new();

                    try
                    {
                        JToken cab = jsonArrayC[0];
                        bdComando = new SqlCommand("sp_mantenerOC", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@action_code", action_code_ca));
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                        bdComando.Parameters.Add(new SqlParameter("@sociedad", Texto(cab, "Sociedad")));
                        bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                        bdComando.Parameters.Add(new SqlParameter("@tipo_oc", Texto(cab, "TipoOc")));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_emision", Texto(cab, "FechaEmision")));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_etd", Texto(cab, "FechaEtd")));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_produccion", Texto(cab, "FechaProduccion")));
                        bdComando.Parameters.Add(new SqlParameter("@fecha_recibido_bodega", Texto(cab, "FechaRecibidoBodega")));
                        bdComando.Parameters.Add(new SqlParameter("@estado_oc", Texto(cab, "Estado")));
                        bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", Texto(cab, "Proveedor")));
                        bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", Texto(cab, "Ruc")));
                        bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", Texto(cab, "RazonSocial")));
                        bdComando.Parameters.Add(new SqlParameter("@moneda", Texto(cab, "Moneda")));
                        bdComando.Parameters.Add(new SqlParameter("@forma_pago", Texto(cab, "FormaPago")));
                        bdComando.Parameters.Add(new SqlParameter("@condicion_pago", Texto(cab, "CondicionPago")));
                        bdComando.Parameters.Add(new SqlParameter("@numero_oferta", Texto(cab, "NroOfer")));
                        bdComando.Parameters.Add(new SqlParameter("@importe_total", Numero(cab, "ImmporteTotal")));
                        bdComando.Parameters.Add(new SqlParameter("@impuesto_general", Numero(cab, "Iva")));
                        bdComando.Parameters.Add(new SqlParameter("@importe_final", Numero(cab, "ImporteFinal")));
                        bdComando.Parameters.Add(new SqlParameter("@observacion", Constantes.NO_INICIA));

                        bdComando.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falló sp_mantenerOC (HISTORY) para la OC {NumeroOc}; se valida si ya existe", numero_oc);

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
                                    JToken det = jsonArrayD[i];
                                    posicion_oc = Texto(det, "Posicion");

                                    bdComando.Parameters["@adm_cliente_id"].Value = Constantes.ADM_CLIENTE;
                                    bdComando.Parameters["@numero_oc"].Value = numero_oc;
                                    bdComando.Parameters["@posicion_oc"].Value = posicion_oc;
                                    bdComando.Parameters["@cod_material"].Value = Texto(det, "Material");
                                    bdComando.Parameters["@des_material"].Value = Texto(det, "Descripcion");
                                    bdComando.Parameters["@um_material"].Value = Texto(det, "Unidadmedida");
                                    bdComando.Parameters["@cantidad"].Value = Numero(det, "Cantidad");
                                    bdComando.Parameters["@cantidad_reparto"].Value = Numero(det, "CantidadReparto");
                                    bdComando.Parameters["@cantidad_por_entregar"].Value = Numero(det, "CantidadPorEntregar");
                                    bdComando.Parameters["@cantidad_entregada"].Value = Numero(det, "CantidadEntregada");
                                    bdComando.Parameters["@precio"].Value = Numero(det, "Precio");
                                    bdComando.Parameters["@total"].Value = Numero(det, "Total");
                                    bdComando.Parameters["@simbolo_moneda"].Value = Texto(det, "SimboloMoneda");
                                    bdComando.Parameters["@numero_solped"].Value = Texto(det, "Solped");
                                    bdComando.Parameters["@posicion_solped"].Value = Texto(det, "PosicionSolped");
                                    bdComando.Parameters["@fecha_entrega_solped"].Value = Texto(det, "FechaEntregaSolped");
                                    bdComando.Parameters["@fecha_entrega"].Value = Texto(det, "FechaEntregaPosicion");
                                    bdComando.Parameters["@fecha_entrega_estadistica"].Value = Texto(det, "FechaEntregaEstadistica");
                                    bdComando.Parameters["@estado_posicion_oc"].Value = Texto(det, "Estado");

                                    bdComando.ExecuteNonQuery();
                                }

                                transaction.Commit();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al registrar la posición {Posicion} de la OC histórica {NumeroOc}", posicion_oc, numero_oc);
                                Revertir(transaction, numero_oc);

                                // La cabecera se registró fuera de la transacción: se elimina para no dejarla huérfana
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
            catch (CampoRequeridoException ex)
            {
                return Error(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en registrarOCHistorico para la OC {NumeroOc}", numero_oc);
                return Error("Error al registrar el histórico de la OC " + numero_oc + ".");
            }

            return mensaje;
        }

        public Mensaje registrarOC(JObject jObject)
        {
            string action_code_ca = "";
            string numero_oc = "";
            string posicion_oc = "";
            string correo_not = "";

            try
            {
                JArray jsonArrayC = jObject?["Cabecera"] as JArray;
                // Sin "Detalle" solo se procesa la cabecera
                JArray jsonArrayD = jObject?["Detalle"] as JArray ?? new JArray();
                if (jsonArrayC == null || jsonArrayC.Count == 0)
                {
                    return Error("Falta el campo obligatorio 'Cabecera'.");
                }

                // Se leen y validan los campos de cabecera antes de abrir la transacción
                JToken cab = jsonArrayC[0];
                action_code_ca = Texto(cab, "ActionCode");
                numero_oc = Texto(cab, "NroOc");
                string sociedad = Texto(cab, "Sociedad");
                string proveedor = Texto(cab, "Proveedor");
                bool conObservacion = action_code_ca.Equals("APPROVE") || action_code_ca.Equals("REFUSED");
                string observacion = conObservacion ? Texto(cab, "Observacion") : "";

                bool errorCabecera = false;
                string detalleErrorCabecera = "";

                using (SqlConnection bdSql = new(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    // Cabecera y detalle se confirman juntos: si algo falla se revierte todo
                    using (SqlTransaction transaccion = bdSql.BeginTransaction())
                    {
                        try
                        {
                            using SqlCommand bdComando = new SqlCommand("sp_mantenerOC", bdSql, transaccion);
                            bdComando.CommandType = CommandType.StoredProcedure;
                            bdComando.Parameters.Add(new SqlParameter("@action_code", action_code_ca));
                            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                            bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                            bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                            bdComando.Parameters.Add(new SqlParameter("@tipo_oc", Texto(cab, "TipoOc")));
                            bdComando.Parameters.Add(new SqlParameter("@fecha_emision", Texto(cab, "FechaEmision")));
                            bdComando.Parameters.Add(new SqlParameter("@fecha_etd", Texto(cab, "FechaEtd")));
                            bdComando.Parameters.Add(new SqlParameter("@fecha_produccion", Texto(cab, "FechaProduccion")));
                            bdComando.Parameters.Add(new SqlParameter("@fecha_recibido_bodega", Texto(cab, "FechaRecibidoBodega")));
                            bdComando.Parameters.Add(new SqlParameter("@estado_oc", Texto(cab, "Estado")));
                            bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", proveedor));
                            bdComando.Parameters.Add(new SqlParameter("@identificacion_fiscal", Texto(cab, "Ruc")));
                            bdComando.Parameters.Add(new SqlParameter("@denominacion_fiscal", Texto(cab, "RazonSocial")));
                            bdComando.Parameters.Add(new SqlParameter("@moneda", Texto(cab, "Moneda")));
                            bdComando.Parameters.Add(new SqlParameter("@forma_pago", Texto(cab, "FormaPago")));
                            bdComando.Parameters.Add(new SqlParameter("@condicion_pago", Texto(cab, "CondicionPago")));
                            bdComando.Parameters.Add(new SqlParameter("@numero_oferta", Texto(cab, "NroOfer")));
                            bdComando.Parameters.Add(new SqlParameter("@importe_total", Numero(cab, "ImmporteTotal")));
                            bdComando.Parameters.Add(new SqlParameter("@impuesto_general", Numero(cab, "Iva")));
                            bdComando.Parameters.Add(new SqlParameter("@importe_final", Numero(cab, "ImporteFinal")));
                            bdComando.Parameters.Add(new SqlParameter("@observacion", observacion));

                            bdComando.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Falló sp_mantenerOC ({ActionCode}) para la OC {NumeroOc}", action_code_ca, numero_oc);
                            Revertir(transaccion, numero_oc);
                            errorCabecera = true;
                            detalleErrorCabecera = DetalleErrorNegocio(ex);
                        }

                        if (!errorCabecera)
                        {
                            for (int i = 0; i < jsonArrayD.Count; i++)
                            {
                                JToken det = jsonArrayD[i];
                                posicion_oc = "";

                                try
                                {
                                    posicion_oc = Texto(det, "Posicion");

                                    using SqlCommand bdComando = new SqlCommand("sp_mantenerOCDet", bdSql, transaccion);
                                    bdComando.CommandType = CommandType.StoredProcedure;
                                    bdComando.Parameters.Add(new SqlParameter("@action_code", Texto(det, "ActionCode")));
                                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                                    bdComando.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                                    bdComando.Parameters.Add(new SqlParameter("@posicion_oc", posicion_oc));
                                    bdComando.Parameters.Add(new SqlParameter("@cod_material", Texto(det, "Material")));
                                    bdComando.Parameters.Add(new SqlParameter("@des_material", Texto(det, "Descripcion")));
                                    bdComando.Parameters.Add(new SqlParameter("@um_material", Texto(det, "Unidadmedida")));
                                    bdComando.Parameters.Add(new SqlParameter("@cantidad", Numero(det, "Cantidad")));
                                    bdComando.Parameters.Add(new SqlParameter("@cantidad_reparto", Numero(det, "CantidadReparto")));
                                    bdComando.Parameters.Add(new SqlParameter("@cantidad_por_entregar", Numero(det, "CantidadPorEntregar")));
                                    bdComando.Parameters.Add(new SqlParameter("@cantidad_entregada", Numero(det, "CantidadEntregada")));
                                    bdComando.Parameters.Add(new SqlParameter("@precio", Numero(det, "Precio")));
                                    bdComando.Parameters.Add(new SqlParameter("@total", Numero(det, "Total")));
                                    bdComando.Parameters.Add(new SqlParameter("@simbolo_moneda", Texto(det, "SimboloMoneda")));
                                    bdComando.Parameters.Add(new SqlParameter("@numero_solped", Texto(det, "Solped")));
                                    bdComando.Parameters.Add(new SqlParameter("@posicion_solped", Texto(det, "PosicionSolped")));
                                    bdComando.Parameters.Add(new SqlParameter("@fecha_entrega_solped", Texto(det, "FechaEntregaSolped")));
                                    bdComando.Parameters.Add(new SqlParameter("@fecha_entrega", Texto(det, "FechaEntregaPosicion")));
                                    bdComando.Parameters.Add(new SqlParameter("@fecha_entrega_estadistica", Texto(det, "FechaEntregaEstadistica")));
                                    bdComando.Parameters.Add(new SqlParameter("@estado_posicion_oc", Texto(det, "Estado")));

                                    bdComando.ExecuteNonQuery();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error en la posición {Posicion} de la OC {NumeroOc} ({ActionCode}); se revierte la transacción", posicion_oc, numero_oc, action_code_ca);
                                    Revertir(transaccion, numero_oc);

                                    string operacion = action_code_ca.Equals("CREATE") ? "creacion" : "actualizacion";
                                    return Error("Error en " + operacion + " de Pos. " + posicion_oc + " de la OC " + numero_oc + "." + DetalleErrorNegocio(ex));
                                }
                            }

                            transaccion.Commit();
                        }
                    }

                    if (errorCabecera)
                    {
                        if (!action_code_ca.Equals("CREATE"))
                        {
                            return Error("Error en actualizacion de Cab. de la OC " + numero_oc + "." + detalleErrorCabecera);
                        }

                        // En CREATE la OC puede existir por un envío previo de SAP
                        using SqlCommand bdValidar = new SqlCommand("sp_validarOCCreate", bdSql);
                        bdValidar.CommandType = CommandType.StoredProcedure;
                        bdValidar.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                        bdValidar.Parameters.Add(new SqlParameter("@numero_oc", numero_oc));
                        bdValidar.Parameters.Add(new SqlParameter("@mensaje", SqlDbType.VarChar, 18));
                        bdValidar.Parameters[2].Direction = ParameterDirection.Output;

                        bdValidar.ExecuteNonQuery();

                        string validadorOC = Convert.ToString(bdValidar.Parameters[2].Value, CultureInfo.InvariantCulture);
                        if (numero_oc.Equals(validadorOC))
                        {
                            return Exito("OC " + numero_oc + " registrado correctamente posterior a un envio.");
                        }
                        return Error("Error en creacion de Cab. de la OC " + numero_oc + "." + detalleErrorCabecera);
                    }

                    if (action_code_ca.Equals("REFUSED"))
                    {
                        try
                        {
                            using SqlCommand bdComando = new SqlCommand("sp_obtenerCorreoNot", bdSql);
                            bdComando.CommandType = CommandType.StoredProcedure;
                            bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                            bdComando.Parameters.Add(new SqlParameter("@action_code", Constantes.ACTION_REFUSED));
                            bdComando.Parameters.Add(new SqlParameter("@sociedad", sociedad));
                            bdComando.Parameters.Add(new SqlParameter("@proveedor", proveedor));
                            bdComando.Parameters.Add(new SqlParameter("@correo", SqlDbType.VarChar, -1));
                            bdComando.Parameters[4].Direction = ParameterDirection.Output;

                            bdComando.ExecuteNonQuery();

                            correo_not = Convert.ToString(bdComando.Parameters[4].Value, CultureInfo.InvariantCulture);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "No se pudo obtener el correo de notificación de rechazo de la OC {NumeroOc}", numero_oc);
                        }
                    }
                }

                // El cambio ya está confirmado: un fallo en el correo no debe devolver error a SAP
                if (action_code_ca.Equals("REFUSED"))
                {
                    try
                    {
                        string mensajeRechazo = ConsultaUtil.generarCorreoRechazo(numero_oc, proveedor, observacion);
                        string asunto = "Nro OC " + numero_oc + " RECHAZADO.";
                        _emailRepository.SendEmail(sociedad, correo_not, asunto, mensajeRechazo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "OC {NumeroOc} rechazada y registrada, pero falló el envío del correo", numero_oc);
                    }
                }

                return action_code_ca.Equals("CREATE")
                    ? Exito("OC " + numero_oc + " registrado correctamente.")
                    : Exito("OC " + numero_oc + " actualizado correctamente.");
            }
            catch (CampoRequeridoException ex)
            {
                return Error(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return Error(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en registrarOC para la OC {NumeroOc} ({ActionCode})", numero_oc, action_code_ca);
                return Error("Error al registrar la OC " + numero_oc + ".");
            }
        }

        public Mensaje leerOC(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            try
            {
                JArray jsonArray = jObject?["leidos"] as JArray;
                if (jsonArray == null)
                {
                    return Error("Falta el campo obligatorio 'leidos'.");
                }

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    for (int i = 0; i < jsonArray.Count; i++)
                    {
                        using SqlCommand bdComando = new SqlCommand("sp_leerOC", bdSql);
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Texto(jsonArray[i], "adm_cliente_id")));
                        bdComando.Parameters.Add(new SqlParameter("@reg_id", Convert.ToInt32(Texto(jsonArray[i], "reg_id"), CultureInfo.InvariantCulture)));
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
                _logger.LogError(ex, "Error en leerOC");
                mensaje.tipo = "E";
                mensaje.mensaje = "No se pudo leer el registro";
                mensaje.data = "";
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
                _logger.LogError(ex, "Error en obtenerTipoOC");
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

                    using SqlCommand bdComando = new SqlCommand("sp_insertarArchivoOC", bdSql);
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
                _logger.LogError(ex, "Error al registrar el archivo {Nombre} de la OC {NumeroOc}", nombre_registro, numero_oc);
                mensaje.tipo = "E";
                mensaje.mensaje = "No se pudo registrar el archivo de la OC.";
                mensaje.data = "";
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
                _logger.LogError(ex, "Error al buscar el archivo de OC {Id}", id);
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron registros";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje listarArchivoOc(string oc, string sociedad, bool ruta)
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
                    bdComando.Parameters.Add(new SqlParameter("@ruta", ruta ? 1 : 0));
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
                _logger.LogError(ex, "Error al listar archivos de la OC {NumeroOc} / sociedad {Sociedad}", oc, sociedad);
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron registros";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje eliminarArchivoOc(int id, string usuario)
        {
            Mensaje mensaje = new Mensaje();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    using SqlCommand bdComando = new SqlCommand("sp_eliminarPorIdArchivoOC", bdSql);
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
                _logger.LogError(ex, "Error al eliminar el archivo de OC {Id}", id);
                mensaje.tipo = "E";
                mensaje.mensaje = "No se pudo eliminar el archivo.";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje notificarOCPendiente()
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();

                    using (SqlCommand bdComando = new SqlCommand("sp_validarOCPendiente", bdSql))
                    {
                        bdComando.CommandType = CommandType.StoredProcedure;
                        bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                        using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                        {
                            adapter.Fill(dt);
                        }
                    }

                    JArray jsonArrayOC = JArray.Parse(JsonConvert.SerializeObject(dt, Formatting.None));
                    var notificadas = new List<string>();
                    int fallidas = 0;

                    if (jsonArrayOC.Count > 0)
                    {
                        // La configuración SMTP se carga una sola vez para todo el lote
                        ConfiguracionCorreo configuracionCorreo = _emailRepository.ObtenerConfiguracion();

                        foreach (JToken oc in jsonArrayOC)
                        {
                            string numeroOc = oc["numero_oc"]?.ToString() ?? "";
                            try
                            {
                                string idioma = oc["idioma"]?.ToString() ?? "";
                                string mensajeOCPendiente = ConsultaUtil.generarCorreoOCPendiente(numeroOc,
                                    oc["denominacion_fiscal"]?.ToString() ?? "",
                                    oc["fecha_registro"]?.ToString() ?? "",
                                    _configuration["Web:url"],
                                    idioma);

                                string asunto = (oc["denominacion_sociedad"]?.ToString() ?? "") + ": Nro OC " + numeroOc;
                                asunto += idioma.Equals("ES") ? " PENDIENTE." : " PENDING.";

                                _emailRepository.SendEmail(configuracionCorreo,
                                    oc["codigo_sociedad"]?.ToString() ?? "",
                                    oc["correo"]?.ToString() ?? "",
                                    asunto,
                                    mensajeOCPendiente);

                                notificadas.Add(numeroOc);
                            }
                            catch (Exception ex)
                            {
                                fallidas++;
                                _logger.LogError(ex, "No se pudo notificar la OC pendiente {NumeroOc}", numeroOc);
                            }
                        }
                    }

                    // Solo se actualiza el log si al menos un correo salió.
                    // Nota: sp_actualizarLogNotificaciones marca todas las pendientes, no solo las enviadas.
                    if (notificadas.Count > 0)
                    {
                        using SqlCommand bdLog = new SqlCommand("sp_actualizarLogNotificaciones", bdSql);
                        bdLog.CommandType = CommandType.StoredProcedure;
                        bdLog.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                        bdLog.ExecuteNonQuery();
                    }

                    if (fallidas > 0)
                    {
                        _logger.LogWarning("Notificación de OC pendientes: {Enviadas} enviadas ({Ocs}), {Fallidas} con error",
                            notificadas.Count, string.Join(",", notificadas), fallidas);
                    }

                    if (fallidas > 0 && notificadas.Count == 0)
                    {
                        mensaje.tipo = "E";
                        mensaje.mensaje = "No se pudo enviar ningún correo (" + fallidas + " con error).";
                    }
                    else if (fallidas > 0)
                    {
                        mensaje.tipo = "S";
                        mensaje.mensaje = "Correos enviados: " + notificadas.Count + ", con error: " + fallidas + ".";
                    }
                    else
                    {
                        mensaje.tipo = "S";
                        mensaje.mensaje = "Correos enviados con éxito";
                    }
                    mensaje.data = "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en notificarOCPendiente");
                mensaje.tipo = "E";
                mensaje.mensaje = "Error al notificar las OC pendientes.";
                mensaje.data = "";
            }

            return mensaje;
        }

        public async Task<Mensaje> obtenerOC(JObject jObject)
        {
            try
            {
                string operador = JsonUtil.Requerido(jObject, "operador");
                var bodyRequest = ConstruirPayload(jObject, operador);
                var responseBody = await LlamarSapAsync(_configuration["Http:ConsultarOC"], bodyRequest);

                var dataApi = JObject.Parse(responseBody);
                var ocCabApi = ObtenerItems(dataApi["ET_OC_CAB"]) ?? new JArray();
                var ocDetApi = operador == "O"
                    ? ObtenerItems(dataApi["ET_OC_DET"]) ?? new JArray()
                    : new JArray();

                var mensajeSP = obtenerOCCabecera(jObject);
                var ocCabSPDict = mensajeSP.tipo == "S"
                    ? ConstruirDiccionarioSP(JArray.Parse(mensajeSP.data))
                    : new Dictionary<string, JObject>();

                var cabecera = MapearCabeceras(ocCabApi, ocCabSPDict);
                var detalle = MapearDetalles(ocDetApi, ocCabApi);

                await RevisarRechazadasAsync(cabecera);

                await RegistrarPendientesNuevasAsync(cabecera, ocCabApi, JsonUtil.Opcional(jObject, "sociedad"));

                return new Mensaje
                {
                    tipo = "S",
                    mensaje = "",
                    data = "\"cabecera\":" + cabecera.ToString(Formatting.None) + ",\"detalle\":" + detalle.ToString(Formatting.None)
                };
            }
            catch (CampoRequeridoException ex)
            {
                return Error(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en obtenerOC");
                return Error("Error general al consultar las órdenes de compra.");
            }
        }

        /// <summary>
        /// Una OC rechazada vuelve a "Pendiente" si SAP tiene una versión posterior a la confirmación.
        /// Las consultas a SAP se hacen en paralelo con un máximo de <see cref="MaxConsultasSapConcurrentes"/>.
        /// </summary>
        private async Task RevisarRechazadasAsync(JArray cabecera)
        {
            var rechazadas = cabecera.OfType<JObject>()
                .Where(x => x["estado_confirmacion"]?.ToString() == "Rechazado" && !string.IsNullOrEmpty(x["numero_oc"]?.ToString()))
                .ToList();
            if (rechazadas.Count == 0) return;

            using var semaforo = new SemaphoreSlim(MaxConsultasSapConcurrentes);

            // Los datos de cada item se leen antes del primer await (en el hilo actual)
            var tareas = rechazadas.Select(async item =>
            {
                string numeroOC = item["numero_oc"].ToString();
                if (!TryObtenerFecha(item["fecha_confirmacion"], out DateTime fechaConfirmacion))
                    return false;

                await semaforo.WaitAsync();
                try
                {
                    var jObjectHist = new JObject
                    {
                        ["numero_oc"] = numeroOC,
                        ["estado_confirmacion"] = "Rechazado"
                    };

                    var respuestaHistorico = await obtenerOCHistorico(jObjectHist);
                    if (respuestaHistorico.tipo != "S" || string.IsNullOrEmpty(respuestaHistorico.data))
                        return false;

                    var cabeceraHist = JObject.Parse("{" + respuestaHistorico.data + "}")["cabecera"] as JArray;
                    if (cabeceraHist == null || cabeceraHist.Count == 0)
                        return false;

                    if (!TryObtenerFecha(cabeceraHist[0]["reg_fecha_creacion"], out DateTime fechaCreacion))
                        return false;

                    return fechaCreacion > fechaConfirmacion;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al revisar el histórico de la OC rechazada {NumeroOc}", numeroOC);
                    return false;
                }
                finally
                {
                    semaforo.Release();
                }
            }).ToList();

            bool[] volverAPendiente = await Task.WhenAll(tareas);

            for (int i = 0; i < rechazadas.Count; i++)
            {
                if (volverAPendiente[i])
                {
                    rechazadas[i]["estado_confirmacion"] = "Pendiente";
                }
            }
        }

        /// <summary>
        /// Registra en BD las OC que vienen de SAP y aún no existen (reg_id = 0).
        /// Es un efecto secundario de la consulta: si falla solo se registra en el log, sin afectar la respuesta.
        /// </summary>
        private async Task RegistrarPendientesNuevasAsync(JArray cabecera, JArray ocCabApi, string sociedadFiltro)
        {
            try
            {
                var cabeceraPendiente = cabecera
                    .Where(x => x["estado_confirmacion"]?.ToString() == "Pendiente" && x["reg_id"]?.ToString() == "0")
                    .ToList();
                if (cabeceraPendiente.Count == 0) return;

                // Sociedad por OC según SAP (BUKRS) si viene; si no, la del filtro de la consulta
                var sociedadPorOC = ocCabApi.OfType<JObject>()
                    .Where(x => !string.IsNullOrEmpty(x["EBELN"]?.ToString()) && !string.IsNullOrEmpty(x["BUKRS"]?.ToString()))
                    .GroupBy(x => x["EBELN"].ToString())
                    .ToDictionary(g => g.Key, g => g.First()["BUKRS"].ToString());

                // Se envían copias para no alterar la respuesta al frontend
                JArray jArrayOC = new JArray();
                foreach (JObject item in cabeceraPendiente.OfType<JObject>())
                {
                    JObject copia = (JObject)item.DeepClone();
                    string numeroOC = copia["numero_oc"]?.ToString() ?? "";
                    string sociedad = sociedadPorOC.TryGetValue(numeroOC, out string bukrs) ? bukrs : sociedadFiltro;
                    if (!string.IsNullOrEmpty(sociedad))
                    {
                        copia["sociedad"] = sociedad;
                    }
                    jArrayOC.Add(copia);
                }

                await registrarOCCabeceraAsync(jArrayOC, sociedadFiltro);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al preparar el registro de OC pendientes nuevas");
            }
        }

        private JObject ConstruirPayload(JObject jObject, string operador)
        {
            return new JObject
            {
                ["ET_OC_CAB"] = new JArray(),
                ["ET_OC_DET"] = new JArray(),
                ["I_TAXNUM"] = Constantes.NO_INICIA,
                ["I_LIFNR"] = JsonUtil.Opcional(jObject, "codigo_proveedor"),
                ["I_ESTADO"] = JsonUtil.Opcional(jObject, "estado_oc"),
                ["I_FEC_INI"] = JsonUtil.Opcional(jObject, "fecha_emision_d"),
                ["I_FEC_FIN"] = JsonUtil.Opcional(jObject, "fecha_emision_h"),
                ["I_EBELN"] = JsonUtil.Opcional(jObject, "numero_oc"),
                ["I_BUKRS"] = JsonUtil.Opcional(jObject, "sociedad"),
                ["I_BSART"] = JsonUtil.Opcional(jObject, "tipo_oc"),
                ["I_VER_DETALLE"] = operador == "O"
                    ? Constantes.INDICADOR_DETALLE
                    : Constantes.NO_INICIA
            };
        }

        /// <summary>POST a SAP CPI con el HttpClient nombrado (timeout y Authorization configurados en Startup).</summary>
        private async Task<string> LlamarSapAsync(string url, JObject cuerpo)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("URL de SAP no configurada.");
            }

            HttpClient cliente = _httpClientFactory.CreateClient(Constantes.HTTP_CLIENT_SAP);
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, url);
            solicitud.Content = new StringContent(cuerpo.ToString(Formatting.None), Encoding.UTF8);
            solicitud.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage respuesta = await cliente.SendAsync(solicitud);
            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("SAP respondió {StatusCode} para {Url}", (int)respuesta.StatusCode, url);
                respuesta.EnsureSuccessStatusCode();
            }
            return await respuesta.Content.ReadAsStringAsync();
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
            foreach (JObject itemApi in cabecerasApi.OfType<JObject>())
            {
                string numeroOC = itemApi["EBELN"]?.ToString() ?? "";
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
                    item["reg_estado"] = Convert.ToInt32(datos["reg_estado"], CultureInfo.InvariantCulture);
                    item["reg_id"] = Convert.ToInt32(datos["reg_id"], CultureInfo.InvariantCulture);
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

        public async Task<Mensaje> obtenerOCHistorico(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            string numeroOc = "";

            try
            {
                numeroOc = JsonUtil.Requerido(jObject, "numero_oc");

                JObject objectAUX = new JObject();
                objectAUX.Add("ET_OC_HIS_CAB", new JArray());
                objectAUX.Add("ET_OC_HIS_DET", new JArray());
                objectAUX.Add("I_EBELN", numeroOc);
                objectAUX.Add("I_VER_DETALLE", jObject["estado_confirmacion"]?.ToString() == "Rechazado" ? "9" : "1");

                string responseBody = await LlamarSapAsync(_configuration["Http:ConsultarOCHistorico"], objectAUX);
                JObject objJsonAux = JObject.Parse(responseBody);

                mensaje.tipo = "S";
                mensaje.mensaje = "";

                try
                {
                    JArray jArrayETOCCAB = ObtenerItems(objJsonAux["ET_OC_HIS_CAB"]);
                    if (jArrayETOCCAB == null)
                    {
                        throw new InvalidOperationException("SAP no devolvió ET_OC_HIS_CAB.item");
                    }

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

                    JArray jArrayOCDET = new JArray();
                    JArray jArrayETOCDET = ObtenerItems(objJsonAux["ET_OC_HIS_DET"]);

                    if (jArrayETOCDET != null)
                    {
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
                    _logger.LogWarning(ex, "Respuesta de histórico SAP sin datos válidos para la OC {NumeroOc}", numeroOc);
                    mensaje.tipo = "E";
                    mensaje.mensaje = "No se encontraron registros";
                    mensaje.data = "";
                }
            }
            catch (CampoRequeridoException ex)
            {
                return Error(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el histórico de la OC {NumeroOc} en SAP", numeroOc);
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encuntraron comprobantes";
                mensaje.data = "";
            }

            return mensaje;
        }

        public Mensaje obtenerOCCabecera(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                string operador = TraducirOperador(JsonUtil.Opcional(jObject, "operador"));

                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_buscarOC", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@operador", operador));
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                    bdComando.Parameters.Add(new SqlParameter("@sociedad", JsonUtil.Opcional(jObject, "sociedad")));
                    bdComando.Parameters.Add(new SqlParameter("@numero_oc", JsonUtil.Opcional(jObject, "numero_oc")));
                    bdComando.Parameters.Add(new SqlParameter("@fecha_emision_d", JsonUtil.Opcional(jObject, "fecha_emision_d")));
                    bdComando.Parameters.Add(new SqlParameter("@fecha_emision_h", JsonUtil.Opcional(jObject, "fecha_emision_h")));
                    bdComando.Parameters.Add(new SqlParameter("@estado_oc", JsonUtil.Opcional(jObject, "estado_oc")));
                    bdComando.Parameters.Add(new SqlParameter("@codigo_proveedor", JsonUtil.Opcional(jObject, "codigo_proveedor")));
                    bdComando.Parameters.Add(new SqlParameter("@tipo_oc", JsonUtil.Opcional(jObject, "tipo_oc")));
                    bdComando.Parameters.Add(new SqlParameter("@estado_confirmacion", JsonUtil.Opcional(jObject, "estado_confirmacion")));
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
                _logger.LogError(ex, "Error en obtenerOCCabecera");
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron registros";
                mensaje.data = "";
            }

            return mensaje;
        }

        /// <summary>Inserta las OC nuevas de SAP. Recibe un arreglo JSON de items de cabecera (ver RegistrarPendientesNuevasAsync).</summary>
        private async Task registrarOCCabeceraAsync(JArray jArrayOC, string sociedad)
        {
            try
            {
                await using SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier"));
                await bdSql.OpenAsync();
                await using SqlCommand bdComando = new SqlCommand("sp_mantenerOC_cabecera", bdSql);
                bdComando.CommandType = CommandType.StoredProcedure;
                bdComando.Parameters.Add("@p_json", SqlDbType.NVarChar, -1).Value = jArrayOC.ToString(Formatting.None);
                // Sociedad por defecto para los items que no la traen
                bdComando.Parameters.Add("@p_sociedad", SqlDbType.VarChar, 18).Value =
                    string.IsNullOrWhiteSpace(sociedad) ? DBNull.Value : (object)sociedad.Trim();
                await bdComando.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_mantenerOC_cabecera al registrar {Cantidad} OC pendientes", jArrayOC.Count);
            }
        }

        // ===== Utilitarios =====

        private static string TraducirOperador(string operador)
        {
            switch (operador)
            {
                case "B": return "B";
                case "O": return "OC";
                case "H": return "HC";
                default: return "";
            }
        }

        /// <summary>Lee un campo como texto: si no existe lanza CampoRequeridoException; un null explícito se toma como "".</summary>
        private static string Texto(JToken obj, string campo)
        {
            JToken token = obj?[campo];
            if (token == null) throw new CampoRequeridoException(campo);
            return token.Type == JTokenType.Null ? "" : token.ToString();
        }

        /// <summary>Lee un campo numérico con cultura invariante (punto decimal).</summary>
        private static decimal Numero(JToken obj, string campo)
        {
            JToken token = obj?[campo];
            if (token == null) throw new CampoRequeridoException(campo);
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return token.Value<decimal>();
            }
            if (!decimal.TryParse(token.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor))
            {
                throw new ArgumentException($"El campo '{campo}' no tiene un valor numérico válido.");
            }
            return valor;
        }

        /// <summary>Fecha de un token: soporta tokens Date (Newtonsoft) y textos ISO.</summary>
        private static bool TryObtenerFecha(JToken token, out DateTime fecha)
        {
            fecha = default;
            if (token == null || token.Type == JTokenType.Null) return false;
            if (token.Type == JTokenType.Date)
            {
                fecha = token.Value<DateTime>();
                return true;
            }
            return DateTime.TryParse(token.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
        }

        /// <summary>
        /// Devuelve contenedor["item"] como arreglo. SAP CPI envía un objeto cuando hay un solo registro
        /// y un texto vacío cuando no hay registros; en ese caso devuelve null.
        /// </summary>
        private static JArray ObtenerItems(JToken contenedor)
        {
            if (contenedor is not JObject obj) return null;
            JToken item = obj["item"];
            if (item is JArray arreglo) return arreglo;
            if (item is JObject unico) return new JArray(unico);
            return null;
        }

        /// <summary>
        /// Mensaje de los THROW de negocio de los SP (número >= 50000, p. ej. 50301/50302/50310/50311),
        /// que es seguro devolver a SAP. Para otros errores devuelve vacío.
        /// </summary>
        private static string DetalleErrorNegocio(Exception ex)
        {
            return ex is SqlException sqlEx && sqlEx.Number >= 50000 ? " " + sqlEx.Message : "";
        }

        private void Revertir(SqlTransaction transaccion, string numero_oc)
        {
            try
            {
                transaccion.Rollback();
            }
            catch (Exception ex)
            {
                // Puede fallar si el SP ya revirtió la transacción
                _logger.LogWarning(ex, "No se pudo revertir la transacción de la OC {NumeroOc}", numero_oc);
            }
        }

        private static Mensaje Exito(string texto)
        {
            return new Mensaje { tipo = "S", mensaje = texto, data = "" };
        }

        private static Mensaje Error(string texto)
        {
            return new Mensaje { tipo = "E", mensaje = texto, data = "" };
        }
    }
}
