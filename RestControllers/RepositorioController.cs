using esupplier.Models.Response;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Auth;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using esupplier.Models;
using esupplier.Services;
using Newtonsoft.Json;
using System.Text;

namespace esupplier.RestControllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class RepositorioController : ControllerBase
    {
        private IConfiguracionService _ConfiguracionService;
        private readonly IConfidentialClientApplication _confidentialClientApplication;
        private readonly ClientCredentialProvider _authProvider;
        private readonly GraphServiceClient _graphClient;
        private readonly IRepositorioService _repositorioService;

        private readonly IHeaderService _headerService;

        public RepositorioController( IConfiguracionService configuracionService, IRepositorioService repositorioService, IHeaderService headerService)
        {
            _ConfiguracionService = configuracionService;
            _repositorioService = repositorioService;
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
            }

            _confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithTenantId(tenantId)
                .Build();

            _authProvider = new ClientCredentialProvider(_confidentialClientApplication);
            _graphClient = new GraphServiceClient(_authProvider);
        }


        [HttpGet("listarArchivos")]
        public async Task<ActionResult> listarArchivos([FromQuery] string usuario)
        {
            List<OneDriveItem> lista= new List<OneDriveItem>();

            string rpta = "";
            Mensaje msj = _repositorioService.obtenerRutaRepositorio(usuario);
            JArray jsonArray = JArray.Parse(msj.data);

            int admin = 0;
            string carga = "";
            string ruta = "";
            string correo = "";

            foreach (JObject item in jsonArray)
            {
                admin = (int)item["admin"];
                carga = (string)item["carga"];
                if (carga.EndsWith(@"/"))
                {
                    carga = carga.TrimEnd('/');
                }
                ruta = (string)item["ruta"];
                if (ruta.EndsWith(@"/"))
                {
                    ruta = ruta.TrimEnd('/');
                }
                correo = (string)item["correo"];
            }
           
                try
            {
                if(admin==0)
                {
                    var portal = await listarArchivosDrive(ruta, correo, admin);
                    lista.AddRange(portal);
                }


                var sublist = await listarArchivosDrive(carga, correo, admin);
                lista.AddRange(sublist);
                var json = JsonConvert.SerializeObject(lista, Formatting.Indented);

                rpta = "{\"type\":\"S\",\"data\":" + json + "}";
            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }
            
            return Ok(rpta);
        }

        private async Task<List<OneDriveItem>> listarArchivosDrive( string ruta, string correo,int admin)
        {

            List<OneDriveItem> lista = new List<OneDriveItem>();

            try
            {
                var driveItems = await _graphClient.Users[correo].Drive.Root
                    .ItemWithPath(ruta)
                    .Children
                    .Request()
                    .GetAsync();



                foreach (var item in driveItems)
                {
                    if (item.File != null) // Filtrar solo archivos
                    {
                        var driveItem = new OneDriveItem();
                        driveItem.id = item.Id;
                        driveItem.nombre = item.Name;
                        driveItem.carpeta = item.ParentReference.Name;
                        driveItem.fec = String.Format("{0:yyyy/MM/dd}", item.CreatedDateTime);  
                        driveItem.ext = ConsultaUtil.GetFileExtension(item.Name);
                        lista.Add(driveItem);

                    }
                    else if(admin==1)
                    {
                        var sublist= await listarArchivosDrive( ruta+'/'+item.Name,  correo,  admin);
                        lista.AddRange(sublist);

                    }
                }
            }
            catch (Exception e)
            {
            }

            return lista;
        }


        [HttpGet("descargarArchivo/{usuario}/{id}")]
        public async Task<IActionResult> DescargarArchivo([FromRoute] string usuario, [FromRoute] string id)
        {
            Mensaje msj = _repositorioService.obtenerRutaRepositorio(usuario);
            JArray jsonArray = JArray.Parse(msj.data);

            string correo = "";

            foreach (JObject item in jsonArray)
            {

                correo = (string)item["correo"];
            }
            if (correo != null && correo != "")
            {
                try
                {

                    var file = await _graphClient.Users[correo].Drive.Items[id].Request().GetAsync();
                    var fileName = file.Name;

                    var stream = await _graphClient.Users[correo].Drive.Items[id].Content.Request().GetAsync();
                    return File(stream, "application/octet-stream", fileName);
             
                
                }
                catch (Microsoft.Graph.ServiceException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                       
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


        [HttpPost("subirOneDrive")]
        public async Task<ActionResult> SubirOneDrive()
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

                string usuario = ((string)jObject["usuario"]).Trim();

                Mensaje msj = _repositorioService.obtenerRutaRepositorio(usuario);
                JArray jsonArray = JArray.Parse(msj.data);

               
                string carga = "";
                string correo = "";

                foreach (JObject item in jsonArray)
                {
                    carga = (string)item["carga"];
                    correo = (string)item["correo"];
                }




                byte[] byteArray = Convert.FromBase64String(fileContentSplit[1]);



                MemoryStream stream = new MemoryStream(byteArray);

                var uploadSession = await _graphClient.Users[correo].Drive.Root
                    .ItemWithPath(carga + fileName)
                    .CreateUploadSession()
                    .Request()
                    .PostAsync();

                var maxChunkSize = 320 * 1024; // Tamaño máximo del fragmento: 320 KB
                var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxChunkSize);
                var uploadResult = await fileUploadTask.UploadAsync();

                string idioma = _headerService.obtenerIdioma();
   
                if (uploadResult.UploadSucceeded)
                {
                    if (idioma == "ES")
                        rpta = "{\"message\":\"Se subio el archivo correctamente\",\"type\":\"S\"}";
                    else
                        rpta = "{\"message\":\"The file was uploaded successfully\",\"type\":\"S\"}";
                }
                else
                {
                    if (idioma == "ES")
                        rpta = "{\"message\":\"error al subir el archivo\",\"type\":\"E\"}";
                    else
                        rpta = "{\"message\":\"error uploading file\",\"type\":\"E\"}";

                }

            }
            catch (Exception ex)
            {

                rpta = "{\"message\":\"" + ex.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

    }
}
