using esupplier.Models;
using esupplier.Models.Response;
using esupplier.Services;
using esupplier.Services.IServices;
using esupplier.Utils;
using esupplier.Middleware.Autorizacion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.RestControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepositorioController : ControllerBase
    {
        private readonly IGraphClientProvider _graphProvider;
        private readonly IRepositorioService _repositorioService;
        private readonly IHeaderService _headerService;
        private readonly ILogger<RepositorioController> _logger;

        public RepositorioController(
            IGraphClientProvider graphProvider,
            IRepositorioService repositorioService,
            IHeaderService headerService,
            ILogger<RepositorioController> logger)
        {
            _graphProvider = graphProvider;
            _repositorioService = repositorioService;
            _headerService = headerService;
            _logger = logger;
        }

        /// <summary>Configuración de OneDrive del usuario devuelta por sp_obtenerRutaRepositorio.</summary>
        private sealed class RutaRepositorio
        {
            public int Admin { get; set; }
            public string Carga { get; set; } = "";
            public string Ruta { get; set; } = "";
            public string Correo { get; set; } = "";
        }

        /// <summary>Obtiene la ruta del repositorio del usuario. Devuelve null y un mensaje si no existe o hubo error.</summary>
        private RutaRepositorio ObtenerRuta(string usuario, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(usuario))
            {
                error = "Falta el campo obligatorio 'usuario'.";
                return null;
            }

            Mensaje msj = _repositorioService.obtenerRutaRepositorio(usuario.Trim());
            if (msj == null || msj.tipo != "S" || string.IsNullOrWhiteSpace(msj.data))
            {
                error = msj?.mensaje ?? ApiRespuesta.MensajeErrorGenerico;
                return null;
            }

            JArray filas = JArray.Parse(msj.data);
            if (filas.Count == 0)
            {
                error = "No se encontró la configuración del repositorio para el usuario.";
                return null;
            }

            // Se mantiene el criterio original: la última fila devuelta por el SP.
            JToken item = filas[filas.Count - 1];
            return new RutaRepositorio
            {
                Admin = LeerAdmin(item["admin"]),
                Carga = ((string)item["carga"] ?? "").TrimEnd('/'),
                Ruta = ((string)item["ruta"] ?? "").TrimEnd('/'),
                Correo = (string)item["correo"] ?? ""
            };
        }

        private static int LeerAdmin(JToken valor)
        {
            if (valor == null || valor.Type == JTokenType.Null) return 0;
            if (valor.Type == JTokenType.Integer || valor.Type == JTokenType.Boolean || valor.Type == JTokenType.Float)
            {
                return Convert.ToInt32(((JValue)valor).Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            return int.TryParse((string)valor, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int a) ? a : 0;
        }

        [HttpGet("listarArchivos")]
        public async Task<ActionResult> listarArchivos([FromQuery] string usuario)
        {
            try
            {
                RutaRepositorio ruta = ObtenerRuta(usuario, out string error);
                if (ruta == null)
                {
                    return Ok(ApiRespuesta.Error(error));
                }

                List<OneDriveItem> lista = new List<OneDriveItem>();
                if (ruta.Admin == 0)
                {
                    lista.AddRange(await listarArchivosDrive(ruta.Ruta, ruta.Correo, ruta.Admin));
                }
                lista.AddRange(await listarArchivosDrive(ruta.Carga, ruta.Correo, ruta.Admin));

                return Ok(ApiRespuesta.ExitoSoloData(JsonConvert.SerializeObject(lista)));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Repositorio.listarArchivos"));
            }
        }

        private async Task<List<OneDriveItem>> listarArchivosDrive(string ruta, string correo, int admin)
        {
            List<OneDriveItem> lista = new List<OneDriveItem>();

            try
            {
                var driveItems = await _graphProvider.Cliente.Users[correo].Drive.Root
                    .ItemWithPath(ruta)
                    .Children
                    .Request()
                    .GetAsync();

                foreach (var item in driveItems)
                {
                    if (item.File != null) // Filtrar solo archivos
                    {
                        lista.Add(new OneDriveItem
                        {
                            id = item.Id,
                            nombre = item.Name,
                            carpeta = item.ParentReference?.Name,
                            fec = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:yyyy/MM/dd}", item.CreatedDateTime),
                            ext = ConsultaUtil.GetFileExtension(item.Name)
                        });
                    }
                    else if (admin == 1)
                    {
                        lista.AddRange(await listarArchivosDrive(ruta + '/' + item.Name, correo, admin));
                    }
                }
            }
            catch (ServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Carpeta inexistente: se considera vacía (comportamiento histórico).
                _logger.LogWarning("Carpeta de OneDrive no encontrada: {Ruta}", ruta);
            }
            catch (Exception ex)
            {
                // Se mantiene el comportamiento histórico (se omite la carpeta) pero se registra el error.
                _logger.LogError(ex, "Error al listar la carpeta de OneDrive {Ruta}", ruta);
            }

            return lista;
        }

        /// <summary>Normaliza una ruta de OneDrive para comparar (sin "/drive/root:", sin barras extremas, decodificada).</summary>
        private static string NormalizarRuta(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return "";
            string r = Uri.UnescapeDataString(ruta);
            int idx = r.IndexOf("root:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) r = r.Substring(idx + "root:".Length);
            return r.Trim('/');
        }

        private static bool EstaDentroDe(string carpeta, string raiz)
        {
            string c = NormalizarRuta(carpeta);
            string r = NormalizarRuta(raiz);
            if (r.Length == 0) return false;
            return c.Equals(r, StringComparison.OrdinalIgnoreCase)
                || c.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet("descargarArchivo/{usuario}/{id}")]
        public async Task<IActionResult> DescargarArchivo([FromRoute] string usuario, [FromRoute] string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id) || id.Contains('/') || id.Contains('\\') || id.Contains(".."))
                {
                    return NotFound();
                }

                RutaRepositorio ruta = ObtenerRuta(usuario, out string error);
                if (ruta == null || string.IsNullOrEmpty(ruta.Correo))
                {
                    _logger.LogWarning("Descarga rechazada: sin repositorio para el usuario {Usuario}. {Error}", usuario, error);
                    return NotFound();
                }

                var cliente = _graphProvider.Cliente;
                var file = await cliente.Users[ruta.Correo].Drive.Items[id].Request().GetAsync();

                // El archivo debe estar dentro de las carpetas que el usuario puede listar.
                string carpeta = file.ParentReference?.Path;
                bool permitido = EstaDentroDe(carpeta, ruta.Carga) || (ruta.Admin == 0 && EstaDentroDe(carpeta, ruta.Ruta));
                if (!permitido)
                {
                    _logger.LogWarning("Descarga rechazada: el archivo {Id} ({Carpeta}) no pertenece al repositorio del usuario {Usuario}", id, carpeta, usuario);
                    return NotFound();
                }

                var stream = await cliente.Users[ruta.Correo].Drive.Items[id].Content.Request().GetAsync();
                return File(stream, "application/octet-stream", file.Name);
            }
            catch (ServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Archivo {Id} no encontrado en OneDrive", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar el archivo {Id} de OneDrive", id);
            }

            // Si no se puede descargar el archivo, devolver un error
            return NotFound();
        }

        [SoloInternos]
        [HttpPost("subirOneDrive")]
        public async Task<ActionResult> SubirOneDrive()
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);

                if (!(jObject["file"] is JObject file))
                {
                    throw new CampoRequeridoException("file");
                }

                string fileName = ArchivoUtil.NombreSeguro(JsonUtil.Requerido(file, "fileName"));
                byte[] byteArray = ArchivoUtil.DecodificarBase64(JsonUtil.Requerido(file, "fileContent"));
                string usuario = JsonUtil.Requerido(jObject, "usuario").Trim();

                RutaRepositorio ruta = ObtenerRuta(usuario, out string error);
                if (ruta == null)
                {
                    return Ok(ApiRespuesta.Error(error));
                }

                using MemoryStream stream = new MemoryStream(byteArray);

                var uploadSession = await _graphProvider.Cliente.Users[ruta.Correo].Drive.Root
                    .ItemWithPath(ruta.Carga + "/" + fileName)
                    .CreateUploadSession()
                    .Request()
                    .PostAsync();

                var maxChunkSize = 320 * 1024; // Tamaño máximo del fragmento: 320 KB
                var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxChunkSize);
                var uploadResult = await fileUploadTask.UploadAsync();

                bool espanol = _headerService.obtenerIdioma() == "ES";
                if (uploadResult.UploadSucceeded)
                {
                    return Ok(ApiRespuesta.Exito(espanol ? "Se subio el archivo correctamente" : "The file was uploaded successfully"));
                }

                _logger.LogWarning("La subida de {Archivo} a OneDrive no se completó", fileName);
                return Ok(ApiRespuesta.Error(espanol ? "error al subir el archivo" : "error uploading file"));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (FormatException)
            {
                return Ok(ApiRespuesta.Error("El contenido del archivo no es un base64 válido."));
            }
            catch (JsonReaderException e)
            {
                _logger.LogWarning(e, "Body inválido en subirOneDrive");
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(_logger, e, "Repositorio.subirOneDrive"));
            }
        }
    }
}
