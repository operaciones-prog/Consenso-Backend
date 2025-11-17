using esupplier.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace esupplier.RestControllers
{
    /// <summary>
    /// Controlador de prueba para verificar funcionalidad de almacenamiento (OneDrive/S3)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class StorageTestController : ControllerBase
    {
        private readonly FileStorageFactory _storageFactory;

        public StorageTestController(FileStorageFactory storageFactory)
        {
            _storageFactory = storageFactory;
        }

        /// <summary>
        /// GET /api/StorageTest/info
        /// Obtiene información del proveedor de almacenamiento actual
        /// </summary>
        [HttpGet("info")]
        public ActionResult GetStorageInfo()
        {
            try
            {
                string currentProvider = _storageFactory.GetCurrentProvider();

                var response = new
                {
                    type = "S",
                    message = "Información obtenida correctamente",
                    data = new
                    {
                        currentProvider = currentProvider,
                        timestamp = DateTime.UtcNow,
                        availableProviders = new[] { "OneDrive", "S3" }
                    }
                };

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    type = "E",
                    message = $"Error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// POST /api/StorageTest/upload
        /// Prueba de subida de archivo (requiere autenticación)
        /// Body: { "fileName": "test.txt", "folderPath": "test", "provider": "S3" }
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult> UploadTestFile()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                var requestData = JsonConvert.DeserializeObject<dynamic>(content);

                string fileName = requestData?.fileName ?? "test.txt";
                string folderPath = requestData?.folderPath ?? "test";
                string? provider = requestData?.provider;

                // Crear contenido de prueba
                string testContent = $"Test file uploaded at {DateTime.UtcNow:O}";
                byte[] fileBytes = Encoding.UTF8.GetBytes(testContent);

                // Obtener servicio de almacenamiento
                var storageService = provider != null
                    ? _storageFactory.GetStorageService(provider.ToString())
                    : _storageFactory.GetStorageService();

                // Subir archivo
                var result = await storageService.UploadFileAsync(fileBytes, fileName, folderPath);

                if (result.Success)
                {
                    rpta = JsonConvert.SerializeObject(new
                    {
                        type = "S",
                        message = "Archivo subido correctamente",
                        data = new
                        {
                            fileId = result.FileId,
                            fileName = result.FileName,
                            filePath = result.FilePath,
                            uploadedAt = result.UploadedAt,
                            provider = provider ?? _storageFactory.GetCurrentProvider()
                        }
                    });
                }
                else
                {
                    rpta = JsonConvert.SerializeObject(new
                    {
                        type = "E",
                        message = $"Error al subir archivo: {result.ErrorMessage}"
                    });
                }
            }
            catch (Exception ex)
            {
                rpta = JsonConvert.SerializeObject(new
                {
                    type = "E",
                    message = $"Excepción: {ex.Message}"
                });
            }

            return Ok(rpta);
        }

        /// <summary>
        /// GET /api/StorageTest/list?folderPath=test&provider=S3
        /// Lista archivos en una carpeta (requiere autenticación)
        /// </summary>
        [HttpGet("list")]
        public async Task<ActionResult> ListFiles([FromQuery] string folderPath = "test", [FromQuery] string? provider = null)
        {
            try
            {
                var storageService = provider != null
                    ? _storageFactory.GetStorageService(provider)
                    : _storageFactory.GetStorageService();

                var files = await storageService.ListFilesAsync(folderPath);

                var response = new
                {
                    type = "S",
                    message = $"Se encontraron {files.Count} archivos",
                    data = new
                    {
                        folderPath = folderPath,
                        count = files.Count,
                        files = files.Select(f => new
                        {
                            id = f.Id,
                            name = f.Name,
                            extension = f.Extension,
                            createdDate = f.CreatedDate,
                            size = f.Size
                        }),
                        provider = provider ?? _storageFactory.GetCurrentProvider()
                    }
                };

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    type = "E",
                    message = $"Error al listar archivos: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// GET /api/StorageTest/download-url?fileName=test.txt&folderPath=test&provider=S3
        /// Genera URL de descarga temporal
        /// </summary>
        [HttpGet("download-url")]
        public async Task<ActionResult> GetDownloadUrl(
            [FromQuery] string fileName,
            [FromQuery] string folderPath = "test",
            [FromQuery] string? provider = null,
            [FromQuery] int expirationMinutes = 60)
        {
            try
            {
                var storageService = provider != null
                    ? _storageFactory.GetStorageService(provider)
                    : _storageFactory.GetStorageService();

                string downloadUrl = await storageService.GenerateDownloadUrlAsync(folderPath, fileName, expirationMinutes);

                var response = new
                {
                    type = "S",
                    message = "URL generada correctamente",
                    data = new
                    {
                        fileName = fileName,
                        folderPath = folderPath,
                        url = downloadUrl,
                        expiresIn = $"{expirationMinutes} minutos",
                        provider = provider ?? _storageFactory.GetCurrentProvider()
                    }
                };

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (FileNotFoundException)
            {
                return NotFound(new
                {
                    type = "E",
                    message = "Archivo no encontrado"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    type = "E",
                    message = $"Error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// DELETE /api/StorageTest/delete?fileName=test.txt&folderPath=test&provider=S3
        /// Elimina un archivo de prueba
        /// </summary>
        [HttpDelete("delete")]
        public async Task<ActionResult> DeleteFile(
            [FromQuery] string fileName,
            [FromQuery] string folderPath = "test",
            [FromQuery] string? provider = null)
        {
            try
            {
                var storageService = provider != null
                    ? _storageFactory.GetStorageService(provider)
                    : _storageFactory.GetStorageService();

                bool deleted = await storageService.DeleteFileAsync(folderPath, fileName);

                if (deleted)
                {
                    var response = new
                    {
                        type = "S",
                        message = "Archivo eliminado correctamente",
                        data = new
                        {
                            fileName = fileName,
                            folderPath = folderPath,
                            provider = provider ?? _storageFactory.GetCurrentProvider()
                        }
                    };

                    return Ok(JsonConvert.SerializeObject(response));
                }
                else
                {
                    return NotFound(new
                    {
                        type = "E",
                        message = "Archivo no encontrado o ya fue eliminado"
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    type = "E",
                    message = $"Error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// GET /api/StorageTest/exists?fileName=test.txt&folderPath=test&provider=S3
        /// Verifica si un archivo existe
        /// </summary>
        [HttpGet("exists")]
        public async Task<ActionResult> FileExists(
            [FromQuery] string fileName,
            [FromQuery] string folderPath = "test",
            [FromQuery] string? provider = null)
        {
            try
            {
                var storageService = provider != null
                    ? _storageFactory.GetStorageService(provider)
                    : _storageFactory.GetStorageService();

                bool exists = await storageService.FileExistsAsync(folderPath, fileName);

                var response = new
                {
                    type = "S",
                    message = exists ? "El archivo existe" : "El archivo no existe",
                    data = new
                    {
                        fileName = fileName,
                        folderPath = folderPath,
                        exists = exists,
                        provider = provider ?? _storageFactory.GetCurrentProvider()
                    }
                };

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    type = "E",
                    message = $"Error: {ex.Message}"
                });
            }
        }
    }
}