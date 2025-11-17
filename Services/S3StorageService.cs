using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using esupplier.Config;
using esupplier.Models;
using esupplier.Services.IServices;
using Microsoft.Extensions.Options;

namespace esupplier.Services
{
    public class S3StorageService : IFileStorageService
    {
        private readonly S3Settings _s3Settings;
        private IAmazonS3? _s3Client;

        public S3StorageService(IOptions<StorageSettings> storageSettings)
        {
            _s3Settings = storageSettings.Value.S3;
            InitializeS3Client();
        }

        private void InitializeS3Client()
        {
            if (string.IsNullOrWhiteSpace(_s3Settings.BucketName))
            {
                throw new InvalidOperationException("S3 BucketName no está configurado");
            }

            var region = RegionEndpoint.GetBySystemName(_s3Settings.Region);

            if (_s3Settings.UseIAMRole)
            {
                // Usa credenciales del IAM Role (recomendado para Lambda)
                _s3Client = new AmazonS3Client(region);
            }
            else
            {
                // Usa Access Keys (desarrollo local)
                if (string.IsNullOrWhiteSpace(_s3Settings.AccessKeyId) ||
                    string.IsNullOrWhiteSpace(_s3Settings.SecretAccessKey))
                {
                    throw new InvalidOperationException("AccessKeyId y SecretAccessKey son requeridos cuando UseIAMRole es false");
                }

                var credentials = new BasicAWSCredentials(
                    _s3Settings.AccessKeyId,
                    _s3Settings.SecretAccessKey
                );

                _s3Client = new AmazonS3Client(credentials, region);
            }
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

                var response = await _s3Client!.PutObjectAsync(request);

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

                var response = await _s3Client!.GetObjectAsync(request);

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

                string presignedUrl = _s3Client!.GetPreSignedURL(request);
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

                var response = await _s3Client!.ListObjectsV2Async(request);

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

                var response = await _s3Client!.DeleteObjectAsync(request);
                return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
            }
            catch
            {
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

                await _s3Client!.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}