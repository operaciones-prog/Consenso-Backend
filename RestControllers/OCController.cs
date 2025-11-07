using System.Text;
using esupplier.Models.Response;
using esupplier.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Auth;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using Microsoft.Data.SqlClient;
using esupplier.Utils;
using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using esupplier.Models;
using static System.Net.WebRequestMethods;
using System.IO.Pipes;
using System.IO.Compression;

namespace esupplier.RestControllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class OCController : ControllerBase
    {
        private IOCService _OCService;
        private IConfiguracionService _ConfiguracionService;
        private readonly IConfidentialClientApplication _confidentialClientApplication;
        private readonly ClientCredentialProvider _authProvider;
        private readonly GraphServiceClient _graphClient;
        private readonly IConfiguration _configuration;
        private string _correo;
        private readonly IProveedorService _proveedorService;

        public OCController(IOCService ocService, IConfiguracionService configuracionService, IConfiguration configuration, IProveedorService proveedorService)
        {

            _OCService = ocService;
            _ConfiguracionService = configuracionService;
            _configuration = configuration;
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
            _proveedorService = proveedorService;

        }

        [HttpPost("buscarOC")]
        public ActionResult buscarOC()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _OCService.buscarOC(jObject);

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

        [HttpPost("registrarOCHistorico")]
        public ActionResult registrarOCHistorico()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _OCService.registrarOCHistorico(jObject);

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

        [HttpPost("registrarOC")]
        public ActionResult registrarOC()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _OCService.registrarOC(jObject);

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

        [HttpPost("leerOC")]
        public ActionResult leerOC()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _OCService.leerOC(jObject);

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

        [HttpGet("obtenerTipoOC")]
        public ActionResult obtenerTipoOC()
        {
            string rpta = "";
            try
            {
                Mensaje msj = new Mensaje();
                msj = _OCService.obtenerTipoOC();

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

        [HttpPost("subirOneDriveOc")]
        public async Task<ActionResult> SubirOneDriveOc()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                JObject file = (JObject)jObject["file"];


                string fileContent = (string)file["fileContent"];
                string[] fileContentSplit = fileContent.Split(',');

                string fileName = (string)file["fileName"];

                string proveedor = ((string)jObject["proveedor"]).Trim();

                DateTime now = DateTime.Now;
                string formattedDate = now.ToString("ddMMyyyyHHmmss");
                

                string indicador = ((string)jObject["indicador"]).Trim();

                string oc = ((string)jObject["oc"]).Trim();

                string sociedad = ((string)jObject["sociedad"]).Trim();

                string nombre_registro = oc + "_" + formattedDate + "_" + fileName;

                JObject jObjectConstantes = new JObject();
                jObjectConstantes["adm_cliente_id"] = Constantes.ADM_CLIENTE;
                jObjectConstantes["tipo_constante"] = "RUTA_" + sociedad;
                jObjectConstantes["codigo_constante"] = indicador;

                Mensaje constantes = _ConfiguracionService.listarConstantes(jObjectConstantes);
                JObject jObjectList = JObject.Parse("{" + constantes.data + "}");
                JArray jsonArray = (JArray)jObjectList["constantes"];
                string ruta = _configuration["OneDrive:temporal"]; ;

                JObject jObjectProv = new JObject();
                jObjectProv["adm_cliente_id"] = Constantes.ADM_CLIENTE;
                jObjectProv["sociedad"] = sociedad;
                jObjectProv["codigo_proveedor"] = proveedor;
                jObjectProv["identificacion_fiscal"] = "";
                jObjectProv["denominacion_fiscal"] = "";

                Mensaje constantesProv = _proveedorService.buscarProveedor(jObjectProv);
                JObject jObjectListProv = JObject.Parse("{" + constantesProv.data + "}");
                JArray jsonArrayProv = (JArray)jObjectListProv["proveedores"];
                string nombProv = "";

                foreach (JObject item in jsonArrayProv)
                {
                    nombProv = (string)item["denominacion_fiscal"];
                }


                foreach (JObject item in jsonArray)
                {
                    ruta = (string)item["valor_constante"];
                }
                ruta += proveedor + " " + nombProv + "/";
                if (indicador != null && indicador.ToUpper() == "EXT")
                {
                    ruta += oc + "/";
                }
                byte[] byteArray = Convert.FromBase64String(fileContentSplit[1]);
              


                MemoryStream stream = new MemoryStream(byteArray);

                var uploadSession = await _graphClient.Users[_correo].Drive.Root
                    .ItemWithPath(ruta + nombre_registro)
                    .CreateUploadSession()
                    .Request()
                    .PostAsync();

                var maxChunkSize = 320 * 1024; // Tamaño máximo del fragmento: 320 KB
                var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxChunkSize);
                var uploadResult = await fileUploadTask.UploadAsync();

                if (uploadResult.UploadSucceeded)
                {
                    Mensaje msj = new Mensaje();
                    msj = _OCService.registrarArchivoOc(oc, ruta, fileName, nombre_registro, indicador, sociedad, Constantes.ADM_CLIENTE);
                    if (msj.tipo.Equals("S"))
                    {
                        rpta = "{\"message\":\"Se subio el archivo correctamente\",\"type\":\"S\",\"data\":{" + msj.data + "}}";
                    }
                    else
                    {
                        rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                    }
                }
                else
                {
                    rpta = "{\"message\":\"error al subir el archivo\",\"type\":\"E\"}";
                }

            }
            catch (Exception ex)
            {

                rpta = "{\"message\":\"" + ex.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }


        [HttpGet("descargarArchivo/{id}")]
        public async Task<IActionResult> DescargarArchivo([FromRoute] int id)
        {

            Mensaje msj = _OCService.buscarArchivoOc(id);

            JObject jObjectList = JObject.Parse("{" + msj.data + "}");
            JArray jsonArray = (JArray)jObjectList["archivo"];
            String ruta = "";
            String nombre = "";
            foreach (JObject item in jsonArray)
            {
                ruta = (string)item["ruta"];
                nombre = (string)item["nombre"];
            }
            if (ruta != null && ruta != "" && nombre != null && nombre != "")
            {
                try
                {
                    var driveItems = await _graphClient.Users[_correo].Drive.Root
                    .ItemWithPath(ruta)
                    .Request()
                    .GetAsync();

                    if (driveItems != null)
                    {
                        var fileId = driveItems.Id;
                        var downloadUrl = driveItems.AdditionalData["@microsoft.graph.downloadUrl"].ToString();

                        // Descargar el archivo
                        using (var httpClient = new HttpClient())
                        {
                            var response = await httpClient.GetAsync(downloadUrl);
                            if (response.IsSuccessStatusCode)
                            {
                                // Convertir el contenido del archivo a un arreglo de bytes
                                var fileBytes = await response.Content.ReadAsByteArrayAsync();

                                // Devolver el archivo como una respuesta HTTP
                                return File(fileBytes, "application/octet-stream", nombre);
                            }
                        }
                    }
                    else
                    {
                        _OCService.eliminarArchivoOc(id, Constantes.ADM_CLIENTE);
                    }
                }
                catch (Microsoft.Graph.ServiceException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _OCService.eliminarArchivoOc(id, Constantes.ADM_CLIENTE);
                        // Manejar el caso de que el archivo no se encuentre
                        Console.WriteLine("El archivo no se encontró en OneDrive.");
                    }
                    else
                    {
                        // Manejar otros errores de Graph API
                        Console.WriteLine($"Error al obtener el archivo: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    // Manejar otros tipos de excepciones
                    Console.WriteLine($"Error general: {ex.Message}");
                }
            }

            // Si no se puede descargar el archivo, devolver un error
            return NotFound();
        }


        [HttpGet("descargarMasiva/{sociedad}/{oc}")]
        public async Task<IActionResult> DescargarMasiva([FromRoute] string sociedad, [FromRoute] string oc)
        {

            //var claims = HttpContext.User.Claims;
            Mensaje msj = _OCService.listarArchivoOc(oc, sociedad, true);
            JObject jObjectList = JObject.Parse("{" + msj.data + "}");
            JArray jsonArray = (JArray)jObjectList["archivo"];
            var files = new List<FilesEntry>();
            foreach (JObject item in jsonArray)
            {
                string ruta = (string)item["ruta"];
                string nombre = (string)item["nombre"];
                int id = (int)item["reg_id"];
                try
                {
                    var driveItems = await _graphClient.Users[_correo].Drive.Root
                      .ItemWithPath(ruta)
                      .Request()
                      .GetAsync();

                    if (driveItems != null)
                    {
                        files.Add(new FilesEntry(driveItems.Id, nombre));

                    }
                }
                catch (Microsoft.Graph.ServiceException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _OCService.eliminarArchivoOc(id, Constantes.ADM_CLIENTE);
                        // Manejar el caso de que el archivo no se encuentre
                        Console.WriteLine("El archivo no se encontró en OneDrive.");
                    }
                    else
                    {
                        // Manejar otros errores de Graph API
                        Console.WriteLine($"Error al obtener el archivo: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    // Manejar otros tipos de excepciones
                    Console.WriteLine($"Error general: {ex.Message}");
                }
            }


            for (int i=0;i< files.Count;i++)
            {
                var fileStream = await _graphClient.Users[_correo].Drive.Items[files[i].FileId].Content.Request().GetAsync();
                using (var memoryStream = new MemoryStream())
                {
                    await fileStream.CopyToAsync(memoryStream);
                    files[i].FileBytes = memoryStream.ToArray();
                }
            }

            byte[] byteZip = null;
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    for (int i = 0; i < files.Count; i++)
                    {
                        var zipEntry = archive.CreateEntry((i+1)+"_"+files[i].FileName);
                        using (var entryStream = zipEntry.Open())
                        {
                            entryStream.Write(files[i].FileBytes, 0, files[i].FileBytes.Length);
                        }
                    }
                }
                byteZip= memoryStream.ToArray();
            }

            // Si no se puede descargar el archivo, devolver un error
            return File(byteZip, "application/octet-stream", oc+"_files.zip");
        }



        [HttpGet("listarArchivo/{sociedad}/{oc}")]
        // [Authorize]
        public ActionResult listarArchivo([FromRoute] string sociedad, [FromRoute] string oc)
        {
            string rpta = "";
            try
            {
                //var claims = HttpContext.User.Claims;
                Mensaje msj = new Mensaje();
                msj = _OCService.listarArchivoOc(oc, sociedad,false);



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



        [HttpGet("elimnarArchivo/{id}")]
        public async Task<ActionResult> EliminarArchivo([FromRoute] int id)
        {
            string rpta = "";
            try
            {
                Mensaje msj = _OCService.buscarArchivoOc(id);

                JObject jObjectList = JObject.Parse("{" + msj.data + "}");
                JArray jsonArray = (JArray)jObjectList["archivo"];
                String ruta = "";
                String nombre = "";
                foreach (JObject item in jsonArray)
                {
                    ruta = (string)item["ruta"];
                    nombre = (string)item["nombre"];
                }

                // Buscar el archivo por su nombre+

                var driveItems = await _graphClient.Users[_correo].Drive.Root
                   .ItemWithPath(ruta)
                   .Request()
                   .GetAsync();

                if (driveItems != null)
                {
                    var fileId = driveItems.Id;
                    await _graphClient.Users[_correo].Drive.Items[fileId].Request().DeleteAsync();

                    Mensaje eliminarM = _OCService.eliminarArchivoOc(id, Constantes.ADM_CLIENTE);

                    if (eliminarM.tipo.Equals("S"))
                    {
                        rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\"}";
                    }
                    else
                    {
                        rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                    }
                }
                else
                {
                    rpta = "{\"message\":\"archivo no encontrado en el drive\",\"type\":\"E\"}";
                }
            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpGet("notificarOCPendiente")]
        public ActionResult notificarOCPendiente()
        {
            string rpta = "";
            try
            {
                Mensaje msj = new Mensaje();
                msj = _OCService.notificarOCPendiente();

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

        [HttpPost("obtenerOC")]
        public ActionResult obtenerOC()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _OCService.obtenerOC(jObject);

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

        [HttpPost("obtenerOCHistorico")]
        public ActionResult obtenerOCHistorico()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _OCService.obtenerOCHistorico(jObject);

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
    }
}
