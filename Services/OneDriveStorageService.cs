using esupplier.Config;
using esupplier.Models;
using esupplier.Models.Response;
using esupplier.Services.IServices;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace esupplier.Services
{
    public class OneDriveStorageService : IFileStorageService
    {
        private readonly IConfiguracionService _configuracionService;
        private readonly StorageSettings _storageSettings;
        private GraphServiceClient? _graphClient;
        private string? _userEmail;
        private bool _isInitialized = false;

        public OneDriveStorageService(
            IConfiguracionService configuracionService,
            IOptions<StorageSettings> storageSettings)
        {
            _configuracionService = configuracionService;
            _storageSettings = storageSettings.Value;
        }

        private async Task InitializeAsync()
        {
            if (_isInitialized && _graphClient != null)
                return;

            try
            {
                string clientId, clientSecret, tenantId, userEmail;

                if (_storageSettings.OneDrive.UseDatabase)
                {
                    // Obtener credenciales de la BD (método actual)
                    JObject jObject = new JObject
                    {
                        ["adm_cliente_id"] = "1001",
                        ["tipo_constante"] = "ONEDRIVE",
                        ["codigo_constante"] = ""
                    };

                    Mensaje msj = _configuracionService.listarConstantes(jObject);
                    JObject jObjectList = JObject.Parse("{" + msj.data + "}");
                    JArray jsonArray = (JArray)jObjectList["constantes"];

                    clientId = jsonArray.First(x => x["codigo_constante"]!.ToString() == "CLIENT_ID")!["valor_constante"]!.ToString();
                    clientSecret = jsonArray.First(x => x["codigo_constante"]!.ToString() == "SECRECT")!["valor_constante"]!.ToString();
                    tenantId = jsonArray.First(x => x["codigo_constante"]!.ToString() == "TENANT_ID")!["valor_constante"]!.ToString();
                    userEmail = jsonArray.First(x => x["codigo_constante"]!.ToString() == "MAIL")!["valor_constante"]!.ToString();
                }
                else
                {
                    // Usar valores de appsettings
                    clientId = _storageSettings.OneDrive.ClientId!;
                    clientSecret = _storageSettings.OneDrive.ClientSecret!;
                    tenantId = _storageSettings.OneDrive.TenantId!;
                    userEmail = _storageSettings.OneDrive.UserEmail!;
                }

                _userEmail = userEmail;

                // Crear cliente MSAL
                IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithTenantId(tenantId)
                    .WithClientSecret(clientSecret)
                    .Build();

                // Obtener token
                string[] scopes = { "https://graph.microsoft.com/.default" };
                var authResult = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();

                // Crear GraphServiceClient
                _graphClient = new GraphServiceClient(
                    new DelegateAuthenticationProvider((requestMessage) =>
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
                        return Task.CompletedTask;
                    })
                );

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al inicializar OneDrive: {ex.Message}", ex);
            }
        }

        public async Task<FileUploadResult> UploadFileAsync(byte[] fileContent, string fileName, string folderPath)
        {
            await InitializeAsync();

            try
            {
                string fullPath = $"{folderPath.TrimEnd('/')}/{fileName}";

                using var stream = new MemoryStream(fileContent);

                var uploadSession = await _graphClient!.Users[_userEmail].Drive.Root
                    .ItemWithPath(fullPath)
                    .CreateUploadSession()
                    .Request()
                    .PostAsync();

                var maxChunkSize = 320 * 1024; // 320 KB
                var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxChunkSize);
                var uploadResult = await fileUploadTask.UploadAsync();

                if (uploadResult.UploadSucceeded)
                {
                    return new FileUploadResult
                    {
                        Success = true,
                        FileId = uploadResult.ItemResponse.Id,
                        FilePath = folderPath,
                        FileName = fileName,
                        UploadedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    return new FileUploadResult
                    {
                        Success = false,
                        ErrorMessage = "Upload failed"
                    };
                }
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
            await InitializeAsync();

            try
            {
                string fullPath = $"{filePath.TrimEnd('/')}/{fileName}";

                var driveItem = await _graphClient!.Users[_userEmail].Drive.Root
                    .ItemWithPath(fullPath)
                    .Request()
                    .GetAsync();

                if (driveItem != null)
                {
                    var stream = await _graphClient.Users[_userEmail].Drive.Items[driveItem.Id].Content.Request().GetAsync();

                    return new FileDownloadResult
                    {
                        Success = true,
                        FileStream = stream,
                        FileName = driveItem.Name,
                        ContentType = driveItem.File?.MimeType ?? "application/octet-stream",
                        FileSize = driveItem.Size
                    };
                }

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
            await InitializeAsync();

            try
            {
                string fullPath = $"{filePath.TrimEnd('/')}/{fileName}";

                var driveItem = await _graphClient!.Users[_userEmail].Drive.Root
                    .ItemWithPath(fullPath)
                    .Request()
                    .GetAsync();

                if (driveItem != null && driveItem.AdditionalData.ContainsKey("@microsoft.graph.downloadUrl"))
                {
                    return driveItem.AdditionalData["@microsoft.graph.downloadUrl"].ToString()!;
                }

                throw new FileNotFoundException("Archivo no encontrado en OneDrive");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al generar URL de descarga: {ex.Message}", ex);
            }
        }

        public async Task<List<FileItem>> ListFilesAsync(string folderPath)
        {
            await InitializeAsync();

            var fileList = new List<FileItem>();

            try
            {
                var driveItems = await _graphClient!.Users[_userEmail].Drive.Root
                    .ItemWithPath(folderPath)
                    .Children
                    .Request()
                    .GetAsync();

                foreach (var item in driveItems)
                {
                    if (item.File != null) // Solo archivos, no carpetas
                    {
                        fileList.Add(new FileItem
                        {
                            Id = item.Id,
                            Name = item.Name,
                            Path = folderPath,
                            FolderName = item.ParentReference?.Name ?? "",
                            Extension = Path.GetExtension(item.Name),
                            CreatedDate = item.CreatedDateTime?.DateTime,
                            Size = item.Size
                        });
                    }
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
            await InitializeAsync();

            try
            {
                string fullPath = $"{filePath.TrimEnd('/')}/{fileName}";

                var driveItem = await _graphClient!.Users[_userEmail].Drive.Root
                    .ItemWithPath(fullPath)
                    .Request()
                    .GetAsync();

                if (driveItem != null)
                {
                    await _graphClient.Users[_userEmail].Drive.Items[driveItem.Id].Request().DeleteAsync();
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> FileExistsAsync(string filePath, string fileName)
        {
            await InitializeAsync();

            try
            {
                string fullPath = $"{filePath.TrimEnd('/')}/{fileName}";

                var driveItem = await _graphClient!.Users[_userEmail].Drive.Root
                    .ItemWithPath(fullPath)
                    .Request()
                    .GetAsync();

                return driveItem != null;
            }
            catch
            {
                return false;
            }
        }
    }
}