using System.Net;
using System.Text;
using esupplier.Models.Response;
using esupplier.Services;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Auth;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using ExcelDataReader;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using esupplier.Models;

namespace esupplier.RestControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProveedorController : ControllerBase
    {
        private IProveedorService _proveedorService;
        private IConfiguration _configuration;
        private IHeaderService _headerService;
        private IConfiguracionService _ConfiguracionService;
        private readonly IConfidentialClientApplication _confidentialClientApplication;
        private readonly ClientCredentialProvider _authProvider;
        private readonly GraphServiceClient _graphClient;
        private string _correo;


        public ProveedorController(IProveedorService proveedorService, IConfiguration configuration, IConfiguracionService configuracionService, IHeaderService headerService)
        {
            _proveedorService = proveedorService;
            _ConfiguracionService = configuracionService;
            _configuration = configuration;
            _headerService = headerService;
            JObject jObject = new JObject();
            jObject["adm_cliente_id"] = Constantes.ADM_CLIENTE;
            jObject["tipo_constante"] = "ONEDRIVE";
            jObject["codigo_constante"] = "";

            Mensaje msj = _ConfiguracionService.listarConstantes(jObject);

            JObject jObjectList = JObject.Parse("{" + msj.data + "}");
            JArray jsonArray = (JArray)jObjectList["constantes"];

            string clientId = "";
            string clientSecret = "";
            string tenantId = "";

            foreach (JObject item in jsonArray)
            {
                string codigo_constante = (string)item["codigo_constante"];
                if ("CLIENT_ID" == codigo_constante)
                {
                    clientId = (string)item["valor_constante"];
                }
                else if ("SECRECT" == codigo_constante)
                {
                    clientSecret = (string)item["valor_constante"];
                }
                else if ("TENANT_ID" == codigo_constante)
                {
                    tenantId = (string)item["valor_constante"];
                }
                else if ("MAIL" == codigo_constante)
                {
                    _correo = (string)item["valor_constante"];
                }
            }

            _confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithTenantId(tenantId)
                .Build();

            _authProvider = new ClientCredentialProvider(_confidentialClientApplication);
            _graphClient = new GraphServiceClient(_authProvider);
        }

        [HttpPost("buscarProveedor")]
        public ActionResult buscarProveedor()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _proveedorService.buscarProveedor(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\",\"data\":{" + msj.data + "}}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("registrarProveedor")]
        public ActionResult registrarProveedor()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _proveedorService.registrarProveedor(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\"}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("consultarCreditoProveedores")]
        public ActionResult consultarCreditoProveedores()
        {
            StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
            string content = reader.ReadToEndAsync().Result.ToString();
            JObject jObject = JObject.Parse(content);

            string rpta = "";

            var url = _configuration["Http:consultarCreditoProveedores"];
            var request = (HttpWebRequest)WebRequest.Create(url);
            string json = jObject.ToString();
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

                                Mensaje msj = _ConfiguracionService.buscarCondicionPagoPorCodigo(objJsonAux["E_ZTERM"].ToString());
                                JObject jObjectList = JObject.Parse("{" + msj.data + "}");
                                JArray jsonArray = (JArray)jObjectList["condiciones"];

                                string dato = "-";
                                string idioma = _headerService.obtenerIdioma();
                                foreach (JObject item in jsonArray)
                                {
                                    if(idioma == "ES")
                                    {
                                        dato = (string)item["nombreES"];
                                    }
                                    else
                                    {
                                        dato = (string)item["nombreEN"];
                                    }
                                }
                               
                                try
                                {
                                   
                                    rpta = "{\"type\":\"S\",\"data\":\"" + dato + "\"}";

                                }
                                catch (Exception ex)
                                {
                                    rpta = "{\"message\":\"No se encontraron registros\",\"type\":\"E\"}";
                                }

                            }
                        }
                        else
                        {
                            rpta = "{\"message\":\"Sin respuesta - null\",\"type\":\"E\"}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                rpta = "{\"message\":\"No se encuntraron comprobantes\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("consultarProyeccionMateriales")]
        public ActionResult consultarProyeccionMateriales()
        {


            StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
            string content = reader.ReadToEndAsync().Result.ToString();
            JObject jObject = JObject.Parse(content);
            string rpta = "";

            var url = _configuration["Http:consultarProyeccionMateriales"];
            var request = (HttpWebRequest)WebRequest.Create(url);
            string json = jObject.ToString();
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


                                try
                                {
                                    JArray array = (JArray)objJsonAux["ES_DATA"]["item"];
                                    rpta = "{\"type\":\"S\",\"data\":" + array + "}";

                                }
                                catch (Exception ex)
                                {
                                    rpta = "{\"message\":\"No se encontraron registros\",\"type\":\"E\"}";

                                }

                            }
                        }
                        else
                        {
                            rpta = "{\"message\":\"Sin respuesta - null\",\"type\":\"E\"}";

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                rpta = "{\"message\":\"No se encuntraron comprobantes\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }


        [HttpGet("comboCalificacionMes")]
        public ActionResult comboCalificacionMes([FromQuery] string? codigo)
        {
            string rpta = "";
            try
            {

                Mensaje msj = new Mensaje();
                msj = _proveedorService.comboCalificacion(1, codigo);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"type\":\"S\",\"data\":" + msj.data + "}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpGet("comboCalificacionAnnio")]
        public ActionResult comboCalificacionAnnio([FromQuery] string? codigo)
        {
            string rpta = "";
            try
            {

                Mensaje msj = new Mensaje();
                msj = _proveedorService.comboCalificacion(2, codigo);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"type\":\"S\",\"data\":" + msj.data + "}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }
       

            [HttpGet("comboCalificacioCalificacion")]
            public ActionResult comboCalificacioCalificacion([FromQuery] string? codigo)
            {
                string rpta = "";
                try
                {

                    Mensaje msj = new Mensaje();
                    msj = _proveedorService.comboCalificacion(3, codigo);

                    if (msj.tipo.Equals("S"))
                    {
                        rpta = "{\"type\":\"S\",\"data\":" + msj.data + "}";
                    }
                    else
                    {
                        rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                    }

                }
                catch (Exception e)
                {
                    rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
                }

                return Ok(rpta);
            }

            [HttpGet("comboCalificacioProveedor")]
            public ActionResult comboCalificacioProveedor()
            {
                string rpta = "";
                try
                {

                    Mensaje msj = new Mensaje();
                    msj = _proveedorService.comboCalificacion(0, "");

                    if (msj.tipo.Equals("S"))
                    {
                        rpta = "{\"type\":\"S\",\"data\":" + msj.data + "}";
                    }
                    else
                    {
                        rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                    }

                }
                catch (Exception e)
                {
                    rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
                }

                return Ok(rpta);
            }

        [HttpGet("comboCalificacioOrigen")]
        public ActionResult comboCalificacioOrigen([FromQuery] string? codigo)
        {
            string rpta = "";
            try
            {

                Mensaje msj = new Mensaje();
                msj = _proveedorService.comboCalificacion(4, codigo);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"type\":\"S\",\"data\":" + msj.data + "}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }



        [HttpPost("obtenerCalificacion")]
        public ActionResult obtenerCalificacion( )
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                string codigo = null;
                if (jObject.ContainsKey("codigo"))
                {
                    codigo = ((string)jObject["codigo"]).Trim();
                }

                string annio = null;
                if (jObject.ContainsKey("annio"))
                {
                    annio = ((string)jObject["annio"]).Trim();
                }
                string mes = null;
                if (jObject.TryGetValue("mes", out JToken mesesToken) && mesesToken is JArray)
                {
                    var meses = mesesToken.ToObject<List<string>>();
                    mes = string.Join(",", meses);
                }
                string calificacion = null;
                if (jObject.ContainsKey("calificacion"))
                {
                    calificacion = ((string)jObject["calificacion"]).Trim();
                }
                string origen = null;
                if (jObject.ContainsKey("origen"))
                {
                    origen = ((string)jObject["origen"]).Trim();
                }
                rpta = "{\"type\":\"S\",\"data\":{";

                Mensaje msj = _proveedorService.obtenerCalificacion(1, codigo, annio,mes,calificacion, origen);
                if (msj.tipo.Equals("S"))
                {
                    rpta += "\"general\":" + msj.data;
                }
                else
                {
                    rpta += "\"general\": []";
                }

                msj = _proveedorService.obtenerCalificacion(2, codigo, annio, mes, calificacion, origen);
                if (msj.tipo.Equals("S"))
                {
                    rpta += ",\"calificacion\":" + msj.data;
                }
                else
                {
                    rpta += ",\"calificacion\": []";
                }
                msj = _proveedorService.obtenerCalificacion(3, codigo, annio, mes, calificacion, origen);
                if (msj.tipo.Equals("S"))
                {
                    rpta += ",\"calidad\":" + msj.data;
                }
                else
                {
                    rpta += ",\"calidad\": []";
                }
                msj = _proveedorService.obtenerCalificacion(4, codigo, annio, mes, calificacion, origen);
                if (msj.tipo.Equals("S"))
                {
                    rpta += ",\"otif\":" + msj.data;
                }
                else
                {
                    rpta += ",\"otif\": []";
                }
                rpta += "}}";
            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);


            return NotFound();
        }

    }
}