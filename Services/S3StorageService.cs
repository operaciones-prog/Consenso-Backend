using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using esupplier.Config;
using esupplier.Models;
using esupplier.Services.IServices;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace esupplier.Services
{
    public class S3StorageService : IFileStorageService
    {
        // AmazonS3Client es thread-safe y costoso de crear: se comparte entre requests (por configuración).
        private static readonly ConcurrentDictionary<string, Lazy<IAmazonS3>> Clientes = new();

        private readonly S3Settings _s3Settings;
        private readonly ILogger<S3StorageService> _logger;

        public S3StorageService(IOptions<StorageSettings> storageSettings, ILogger<S3StorageService> logger)
        {
            // No se valida aquí: el servicio se registra aunque el proveedor activo sea OneDrive.
            _s3Settings = storageSettings.Value.S3;
            _logger = logger;
        }

        /// <summary>Cliente S3 compartido. Valida la configuración al usarse, no al construir el servicio.</summary>
        private IAmazonS3 Cliente
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_s3Settings.BucketName))
                {
                    throw new InvalidOperationException("S3 BucketName no está configurado");
                }
                if (!_s3Settings.UseIAMRole &&
                    (string.IsNullOrWhiteSpace(_s3Settings.AccessKeyId) || string.IsNullOrWhiteSpace(_s3Settings.SecretAccessKey)))
                {
                    throw new InvalidOperationException("AccessKeyId y SecretAccessKey son requeridos cuando UseIAMRole es false");
                }

                string clave = $"{_s3Settings.Region}|{_s3Settings.UseIAMRole}|{_s3Settings.AccessKeyId}";
                return Clientes.GetOrAdd(clave, _ => new Lazy<IAmazonS3>(CrearCliente)).Value;
            }
        }

        private IAmazonS3 CrearCliente()
        {
            var region = RegionEndpoint.GetBySystemName(_s3Settings.Region);

            if (_s3Settings.UseIAMRole)
            {
                // Usa credenciales del IAM Role (recomendado para Lambda)
                return new AmazonS3Client(region);
            }

            // Usa Access Keys (desarrollo local)
            var credentials = new BasicAWSCredentials(_s3Settings.AccessKeyId, _s3Settings.SecretAccessKey);
            return new AmazonS3Client(credentials, region);
        }

        private string NormalizePath(string folderPath, string fileName)
        {
            string basePrefix = _s3Settings.BasePrefix?.TrimEnd('/') ?? "";
            string cleanFolder = folderPath.Trim('/').Replace("\\", "/");
            string cleanFile = fileName.TrimStart('/');

            if (!string.IsNullOrEmpty(basePrefix))
            {
                return $"{basePrefix}/{cleanFolder}/{cleanFile}";
            }

            return string.IsNullOrEmpty(cleanFolder) ? cleanFile : $"{cleanFolder}/{cleanFile}";
        }

        private string GetContentType(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".zip" => "application/zip",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }

        public async Task<FileUploadResult> UploadFileAsync(byte[] fileContent, string fileName, string folderPath)
        {
            try
            {
                string key = NormalizePath(folderPath, fileName);

                using var stream = new MemoryStream(fileContent);

                var request = new PutObjectRequest
                {
                    BucketName = _s3Settings.BucketName,
                    Key = key,
                    InputStream = stream,
                    ContentType = GetContentType(fileName),
                    Metadata =
                    {
                        ["original-name"] = fileName,
                        ["upload-date"] = DateTime.UtcNow.ToString("O")
                    }
                };

                var response = await Cliente.PutObjectAsync(request);

                return new FileUploadResult
                {
                    Success = response.HttpStatusCode == System.Net.HttpStatusCode.OK,
                    FileId = response.ETag,
                    FilePath = folderPath,
                    FileName = fileName,
                    UploadedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir {Archivo} a S3", fileName);
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<FileDownloadResult> DownloadFileAsync(string filePath, string fileName)
        {
            try
            {
                string key = NormalizePath(filePath, fileName);

                var request = new GetObjectRequest
                {
                    BucketName = _s3Settings.BucketName,
                    Key = key
                };

                var response = await Cliente.GetObjectAsync(request);

                return new FileDownloadResult
                {
                    Success = true,
                    FileStream = response.ResponseStream,
                    FileName = fileName,
                    ContentType = response.Headers.ContentType,
                    FileSize = response.ContentLength
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new FileDownloadResult
                {
                    Success = false,
                    ErrorMessage = "Archivo no encontrado"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar {Archivo} de S3", fileName);
                return new FileDownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<string> GenerateDownloadUrlAsync(string filePath, string fileName, int expirationMinutes = 60)
        {
            try
            {
                string key = NormalizePath(filePath, fileName);

                // Verificar que existe
                if (!await FileExistsAsync(filePath, fileName))
                {
                    throw new FileNotFoundException($"El archivo no existe en S3: {key}");
                }

                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _s3Settings.BucketName,
                    Key = key,
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    Verb = HttpVerb.GET
                };

                string presignedUrl = Cliente.GetPreSignedURL(request);
                return presignedUrl;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al generar URL de descarga: {ex.Message}", ex);
            }
        }

        public async Task<List<FileItem>> ListFilesAsync(string folderPath)
        {
            try
            {
                string prefix = NormalizePath(folderPath, "").TrimEnd('/') + "/";

                var request = new ListObjectsV2Request
                {
                    BucketName = _s3Settings.BucketName,
                    Prefix = prefix,
                    MaxKeys = 1000
                };

                var response = await Cliente.ListObjectsV2Async(request);

                var fileList = new List<FileItem>();

                foreach (var s3Object in response.S3Objects)
                {
                    // Filtrar carpetas (keys que terminan en /)
                    if (s3Object.Key.EndsWith("/"))
                        continue;

                    string fileName = Path.GetFileName(s3Object.Key);

                    fileList.Add(new FileItem
                    {
                        Id = s3Object.ETag.Trim('"'),
                        Name = fileName,
                        Path = folderPath,
                        FolderName = Path.GetFileName(Path.GetDirectoryName(s3Object.Key)) ?? "",
                        Extension = Path.GetExtension(fileName),
                        CreatedDate = s3Object.LastModified,
                        Size = s3Object.Size
                    });
                }

                return fileList;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al listar archivos: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath, string fileName)
        {
            try
            {
                string key = NormalizePath(filePath, fileName);

                var request = new DeleteObjectRequest
                {
                    BucketName = _s3Settings.BucketName,
                    Key = key
                };

                var response = await Cliente.DeleteObjectAsync(request);
                return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar {Archivo} de S3", fileName);
                return false;
            }
        }

        public async Task<bool> FileExistsAsync(string filePath, string fileName)
        {
            try
            {
                string key = NormalizePath(filePath, fileName);

                var request = new GetObjectMetadataRequest
                {
                    BucketName = _s3Settings.BucketName,
                    Key = key
                };

                await Cliente.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar {Archivo} en S3", fileName);
                return false;
            }
        }
    }
}