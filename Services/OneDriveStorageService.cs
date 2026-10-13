using esupplier.Config;
using esupplier.Models;
using esupplier.Services.IServices;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

namespace esupplier.Services
{
    public class OneDriveStorageService : IFileStorageService
    {
        private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };

        // Cliente propio solo cuando las credenciales vienen de appsettings (UseDatabase = false); se comparte entre requests.
        private static readonly object LockConfig = new object();
        private static GraphServiceClient? _clienteConfig;

        private readonly IGraphClientProvider _graphProvider;
        private readonly StorageSettings _storageSettings;
        private readonly ILogger<OneDriveStorageService> _logger;
        private GraphServiceClient? _graphClient;
        private string? _userEmail;

        public OneDriveStorageService(
            IGraphClientProvider graphProvider,
            IOptions<StorageSettings> storageSettings,
            ILogger<OneDriveStorageService> logger)
        {
            _graphProvider = graphProvider;
            _storageSettings = storageSettings.Value;
            _logger = logger;
        }

        private Task InitializeAsync()
        {
            if (_graphClient != null)
                return Task.CompletedTask;

            try
            {
                if (_storageSettings.OneDrive.UseDatabase)
                {
                    // Credenciales de cfg_constante (ONEDRIVE), cacheadas por el proveedor compartido.
                    _graphClient = _graphProvider.Cliente;
                    _userEmail = _graphProvider.Correo;
                }
                else
                {
                    _graphClient = ObtenerClienteDesdeConfiguracion();
                    _userEmail = _storageSettings.OneDrive.UserEmail;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar OneDrive");
                throw new InvalidOperationException("Error al inicializar OneDrive", ex);
            }

            return Task.CompletedTask;
        }

        private GraphServiceClient ObtenerClienteDesdeConfiguracion()
        {
            if (_clienteConfig != null) return _clienteConfig;

            lock (LockConfig)
            {
                if (_clienteConfig != null) return _clienteConfig;

                var oneDrive = _storageSettings.OneDrive;
                if (string.IsNullOrWhiteSpace(oneDrive.ClientId) || string.IsNullOrWhiteSpace(oneDrive.ClientSecret) || string.IsNullOrWhiteSpace(oneDrive.TenantId))
                {
                    throw new InvalidOperationException("Faltan credenciales de OneDrive en Storage:OneDrive (ClientId/ClientSecret/TenantId).");
                }

                IConfidentialClientApplication app = ConfidentialClientApplicationBuilder
                    .Create(oneDrive.ClientId)
                    .WithTenantId(oneDrive.TenantId)
                    .WithClientSecret(oneDrive.ClientSecret)
                    .Build();

                _clienteConfig = new GraphServiceClient(new DelegateAuthenticationProvider(async request =>
                {
                    // MSAL reutiliza el token en caché hasta que expira.
                    var resultado = await app.AcquireTokenForClient(Scopes).ExecuteAsync();
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resultado.AccessToken);
                }));
                return _clienteConfig;
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
                _logger.LogError(ex, "Error al subir {Archivo} a OneDrive", fileName);
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
                _logger.LogError(ex, "Error al descargar {Archivo} de OneDrive", fileName);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar {Archivo} de OneDrive", fileName);
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
            catch (ServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar {Archivo} en OneDrive", fileName);
                return false;
            }
        }
    }
}