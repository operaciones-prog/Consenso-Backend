using System.Globalization;
using System.IO.Compression;
using esupplier.Models;
using esupplier.Models.Response;
using esupplier.Services;
using esupplier.Services.IServices;
using esupplier.Utils;
using esupplier.Middleware.Autorizacion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Newtonsoft.Json.Linq;

namespace esupplier.RestControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OCController : ControllerBase
    {
        // Lambda/API Gateway no puede devolver respuestas mayores (~6 MB con base64)
        private const long TamanoMaximoDescargaMasivaBytes = 4 * 1024 * 1024;

        private readonly IOCService _OCService;
        private readonly IConfiguracionService _ConfiguracionService;
        private readonly IConfiguration _configuration;
        private readonly IProveedorService _proveedorService;
        private readonly IGraphClientProvider _graphProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OCController> _logger;

        public OCController(IOCService ocService, IConfiguracionService configuracionService, IConfiguration configuration,
            IProveedorService proveedorService, IGraphClientProvider graphProvider, IHttpClientFactory httpClientFactory,
            ILogger<OCController> logger)
        {
            _OCService = ocService;
            _ConfiguracionService = configuracionService;
            _configuration = configuration;
            _proveedorService = proveedorService;
            _graphProvider = graphProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // Unidad de OneDrive del buzón configurado (las credenciales se cargan al primer uso)
        private IDriveRequestBuilder Drive => _graphProvider.Cliente.Users[_graphProvider.Correo].Drive;

        [ProveedorDelToken("codigo_proveedor")]
        [HttpPost("buscarOC")]
        public async Task<ActionResult> buscarOC()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _OCService.buscarOC(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/buscarOC"));
            }
        }

        [ProveedorDelToken("$.Cabecera[*].Proveedor")]
        [HttpPost("registrarOCHistorico")]
        public async Task<ActionResult> registrarOCHistorico()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _OCService.registrarOCHistorico(jObject);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/registrarOCHistorico"));
            }
        }

        [ProveedorDelToken("$.Cabecera[*].Proveedor")]
        [HttpPost("registrarOC")]
        public async Task<ActionResult> registrarOC()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _OCService.registrarOC(jObject);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/registrarOC"));
            }
        }

        [HttpPost("leerOC")]
        public async Task<ActionResult> leerOC()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = _OCService.leerOC(jObject);
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/leerOC"));
            }
        }

        [HttpGet("obtenerTipoOC")]
        public ActionResult obtenerTipoOC()
        {
            try
            {
                Mensaje msj = _OCService.obtenerTipoOC();
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/obtenerTipoOC"));
            }
        }

        [HttpPost("subirOneDriveOc")]
        public async Task<ActionResult> SubirOneDriveOc()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);

                if (jObject["file"] is not JObject file)
                {
                    throw new CampoRequeridoException("file");
                }

                string fileName = ArchivoUtil.NombreSeguro(JsonUtil.Requerido(file, "fileName"));
                byte[] byteArray = ArchivoUtil.DecodificarBase64(JsonUtil.Requerido(file, "fileContent"));

                string proveedor = ArchivoUtil.SegmentoSeguro(JsonUtil.Requerido(jObject, "proveedor").Trim());
                string indicador = JsonUtil.Requerido(jObject, "indicador").Trim();
                string oc = ArchivoUtil.SegmentoSeguro(JsonUtil.Requerido(jObject, "oc").Trim());
                string sociedad = ArchivoUtil.SegmentoSeguro(JsonUtil.Requerido(jObject, "sociedad").Trim());

                string formattedDate = FechaUtil.AhoraEcuador().ToString("ddMMyyyyHHmmss", CultureInfo.InvariantCulture);
                string nombre_registro = oc + "_" + formattedDate + "_" + fileName;

                // Carpeta base por sociedad e indicador (constante RUTA_{sociedad})
                JObject jObjectConstantes = new JObject();
                jObjectConstantes["adm_cliente_id"] = Constantes.ADM_CLIENTE;
                jObjectConstantes["tipo_constante"] = "RUTA_" + sociedad;
                jObjectConstantes["codigo_constante"] = indicador;

                string ruta = _configuration["OneDrive:temporal"];
                foreach (JObject item in ListarDesdeMensaje(_ConfiguracionService.listarConstantes(jObjectConstantes), "constantes"))
                {
                    ruta = (string)item["valor_constante"];
                }

                JObject jObjectProv = new JObject();
                jObjectProv["adm_cliente_id"] = Constantes.ADM_CLIENTE;
                jObjectProv["sociedad"] = sociedad;
                jObjectProv["codigo_proveedor"] = proveedor;
                jObjectProv["identificacion_fiscal"] = "";
                jObjectProv["denominacion_fiscal"] = "";

                string nombProv = "";
                foreach (JObject item in ListarDesdeMensaje(_proveedorService.buscarProveedor(jObjectProv), "proveedores"))
                {
                    nombProv = (string)item["denominacion_fiscal"];
                }

                ruta += proveedor + " " + nombProv + "/";
                if (indicador.ToUpperInvariant() == "EXT")
                {
                    ruta += oc + "/";
                }

                using MemoryStream stream = new MemoryStream(byteArray);

                var uploadSession = await Drive.Root
                    .ItemWithPath(ruta + nombre_registro)
                    .CreateUploadSession()
                    .Request()
                    .PostAsync();

                var maxChunkSize = 320 * 1024; // Tamaño máximo del fragmento: 320 KB
                var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxChunkSize);
                var uploadResult = await fileUploadTask.UploadAsync();

                if (!uploadResult.UploadSucceeded)
                {
                    _logger.LogWarning("No se pudo subir a OneDrive el archivo {Archivo} de la OC {Oc}", nombre_registro, oc);
                    return Ok(ApiRespuesta.Error("error al subir el archivo"));
                }

                Mensaje msj = _OCService.registrarArchivoOc(oc, ruta, fileName, nombre_registro, indicador, sociedad, Constantes.ADM_CLIENTE);
                if (msj.tipo.Equals("S"))
                {
                    return Ok(ApiRespuesta.ExitoConObjeto("Se subio el archivo correctamente", msj.data));
                }

                // No quedó registrado en BD: se elimina de OneDrive para no dejar huérfanos
                await EliminarDeOneDriveSinError(uploadResult.ItemResponse?.Id, ruta + nombre_registro);
                return Ok(ApiRespuesta.Error(msj.mensaje));
            }
            catch (CampoRequeridoException ex)
            {
                return Ok(ApiRespuesta.Error(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return Ok(ApiRespuesta.Error(ex.Message));
            }
            catch (FormatException)
            {
                return Ok(ApiRespuesta.Error("El contenido del archivo no es un base64 válido."));
            }
            catch (Exception ex)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, ex, "OC/subirOneDriveOc"));
            }
        }

        [HttpGet("descargarArchivo/{id}")]
        public async Task<IActionResult> DescargarArchivo([FromRoute] int id)
        {
            try
            {
                Mensaje msj = _OCService.buscarArchivoOc(id);
                string ruta = "";
                string nombre = "";
                foreach (JObject item in ListarDesdeMensaje(msj, "archivo"))
                {
                    ruta = (string)item["ruta"];
                    nombre = (string)item["nombre"];
                }

                if (string.IsNullOrEmpty(ruta) || string.IsNullOrEmpty(nombre))
                {
                    return NotFound();
                }

                try
                {
                    var driveItem = await Drive.Root.ItemWithPath(ruta).Request().GetAsync();

                    if (driveItem == null)
                    {
                        _OCService.eliminarArchivoOc(id, Constantes.ADM_CLIENTE);
                        return NotFound();
                    }

                    var downloadUrl = driveItem.AdditionalData["@microsoft.graph.downloadUrl"].ToString();

                    HttpClient httpClient = _httpClientFactory.CreateClient();
                    using var response = await httpClient.GetAsync(downloadUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var fileBytes = await response.Content.ReadAsByteArrayAsync();
                        return File(fileBytes, "application/octet-stream", nombre);
                    }

                    _logger.LogWarning("Descarga de OneDrive respondió {StatusCode} para el archivo {Id}", (int)response.StatusCode, id);
                }
                catch (ServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // El archivo ya no existe en OneDrive: se depura el registro
                    _logger.LogWarning("El archivo {Id} ({Ruta}) no existe en OneDrive; se elimina el registro", id, ruta);
                    _OCService.eliminarArchivoOc(id, Constantes.ADM_CLIENTE);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar el archivo de OC {Id}", id);
            }

            // Si no se puede descargar el archivo, devolver un error
            return NotFound();
        }

        [HttpGet("descargarMasiva/{sociedad}/{oc}")]
        public async Task<IActionResult> DescargarMasiva([FromRoute] string sociedad, [FromRoute] string oc)
        {
            try
            {
                Mensaje msj = _OCService.listarArchivoOc(oc, sociedad, true);
                if (msj == null || msj.tipo != "S")
                {
                    return NotFound();
                }

                var files = new List<FilesEntry>();
                long tamanoTotal = 0;

                foreach (JObject item in ListarDesdeMensaje(msj, "archivo"))
                {
                    string ruta = (string)item["ruta"];
                    string nombre = (string)item["nombre"];
                    int id = (int)item["reg_id"];
                    try
                    {
                        var driveItem = await Drive.Root.ItemWithPath(ruta).Request().GetAsync();
                        if (driveItem != null)
                        {
                            files.Add(new FilesEntry(driveItem.Id, nombre));
                            tamanoTotal += driveItem.Size ?? 0;
                        }
                    }
                    catch (ServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("El archivo {Id} ({Ruta}) no existe en OneDrive; se elimina el registro", id, ruta);
                        _OCService.eliminarArchivoOc(id, Constantes.ADM_CLIENTE);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al obtener el archivo {Id} ({Ruta}) de OneDrive", id, ruta);
                    }
                }

                if (tamanoTotal > TamanoMaximoDescargaMasivaBytes)
                {
                    return ExcedeTamanoMasiva(oc, tamanoTotal);
                }

                tamanoTotal = 0;
                for (int i = 0; i < files.Count; i++)
                {
                    using var fileStream = await Drive.Items[files[i].FileId].Content.Request().GetAsync();
                    using var memoryStream = new MemoryStream();
                    await fileStream.CopyToAsync(memoryStream);
                    files[i].FileBytes = memoryStream.ToArray();

                    // Control adicional por si OneDrive no informó el tamaño
                    tamanoTotal += files[i].FileBytes.LongLength;
                    if (tamanoTotal > TamanoMaximoDescargaMasivaBytes)
                    {
                        return ExcedeTamanoMasiva(oc, tamanoTotal);
                    }
                }

                byte[] byteZip;
                using (var memoryStream = new MemoryStream())
                {
                    using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                    {
                        for (int i = 0; i < files.Count; i++)
                        {
                            // Solo el nombre, sin rutas, para que el zip no extraiga fuera de su carpeta
                            string nombreEntrada = Path.GetFileName((files[i].FileName ?? "archivo").Replace('\\', '/'));
                            var zipEntry = archive.CreateEntry((i + 1) + "_" + nombreEntrada);
                            using (var entryStream = zipEntry.Open())
                            {
                                entryStream.Write(files[i].FileBytes, 0, files[i].FileBytes.Length);
                            }
                        }
                    }
                    byteZip = memoryStream.ToArray();
                }

                return File(byteZip, "application/octet-stream", oc + "_files.zip");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiRespuesta.ErrorInesperado(_logger, ex, "OC/descargarMasiva"));
            }
        }

        [HttpGet("listarArchivo/{sociedad}/{oc}")]
        public ActionResult listarArchivo([FromRoute] string sociedad, [FromRoute] string oc)
        {
            try
            {
                Mensaje msj = _OCService.listarArchivoOc(oc, sociedad, false);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/listarArchivo"));
            }
        }

        // "elimnarArchivo" (con la errata) lo usa el frontend actual; "eliminarArchivo" con DELETE es el alias correcto
        [HttpGet("elimnarArchivo/{id}")]
        [HttpDelete("eliminarArchivo/{id}")]
        public async Task<ActionResult> EliminarArchivo([FromRoute] int id)
        {
            try
            {
                Mensaje msj = _OCService.buscarArchivoOc(id);
                string ruta = "";
                foreach (JObject item in ListarDesdeMensaje(msj, "archivo"))
                {
                    ruta = (string)item["ruta"];
                }

                if (string.IsNullOrEmpty(ruta))
                {
                    return Ok(ApiRespuesta.Error("archivo no encontrado"));
                }

                // Primero BD: si luego falla OneDrive solo queda un archivo sin referencia (se registra en log)
                Mensaje eliminarM = _OCService.eliminarArchivoOc(id, Constantes.ADM_CLIENTE);
                if (!eliminarM.tipo.Equals("S"))
                {
                    return Ok(ApiRespuesta.Error(eliminarM.mensaje));
                }

                await EliminarDeOneDriveSinError(null, ruta);

                return Ok(ApiRespuesta.Exito(eliminarM.mensaje));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/eliminarArchivo"));
            }
        }

        [SoloInternos]
        [HttpGet("notificarOCPendiente")]
        [HttpPost("notificarOCPendiente")]
        public ActionResult notificarOCPendiente()
        {
            try
            {
                Mensaje msj = _OCService.notificarOCPendiente();
                return Ok(ApiRespuesta.DesdeMensaje(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/notificarOCPendiente"));
            }
        }

        [ProveedorDelToken("codigo_proveedor")]
        [HttpPost("obtenerOC")]
        public async Task<ActionResult> obtenerOC()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = await _OCService.obtenerOC(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/obtenerOC"));
            }
        }

        [HttpPost("obtenerOCHistorico")]
        public async Task<ActionResult> obtenerOCHistorico()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = await _OCService.obtenerOCHistorico(jObject);
                return Ok(ApiRespuesta.DesdeMensajeConObjeto(msj));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "OC/obtenerOCHistorico"));
            }
        }

        // ===== Utilitarios =====

        /// <summary>Devuelve el arreglo <paramref name="propiedad"/> de msj.data, o vacío si el mensaje es de error o no trae datos.</summary>
        private static IEnumerable<JObject> ListarDesdeMensaje(Mensaje msj, string propiedad)
        {
            if (msj == null || msj.tipo != "S" || string.IsNullOrWhiteSpace(msj.data))
            {
                return Enumerable.Empty<JObject>();
            }
            JArray arreglo = JObject.Parse("{" + msj.data + "}")[propiedad] as JArray;
            return arreglo?.OfType<JObject>() ?? Enumerable.Empty<JObject>();
        }

        /// <summary>Elimina un archivo de OneDrive por id o por ruta; los errores solo se registran en el log.</summary>
        private async Task EliminarDeOneDriveSinError(string itemId, string ruta)
        {
            try
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    var driveItem = await Drive.Root.ItemWithPath(ruta).Request().GetAsync();
                    itemId = driveItem?.Id;
                }
                if (!string.IsNullOrEmpty(itemId))
                {
                    await Drive.Items[itemId].Request().DeleteAsync();
                }
            }
            catch (ServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("El archivo {Ruta} ya no existía en OneDrive", ruta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo eliminar de OneDrive el archivo {Ruta}; queda sin referencia en BD", ruta);
            }
        }

        private ObjectResult ExcedeTamanoMasiva(string oc, long tamano)
        {
            _logger.LogWarning("Descarga masiva de la OC {Oc} excede el límite ({Tamano} bytes)", oc, tamano);
            string mensaje = $"Los archivos de la OC superan el tamaño máximo de descarga masiva ({TamanoMaximoDescargaMasivaBytes / (1024 * 1024)} MB). Descárguelos individualmente.";
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ApiRespuesta.Error(mensaje));
        }
    }
}
