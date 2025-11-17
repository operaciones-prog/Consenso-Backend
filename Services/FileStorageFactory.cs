using esupplier.Config;
using esupplier.Services.IServices;
using Microsoft.Extensions.Options;

namespace esupplier.Services
{
    /// <summary>
    /// Factory para obtener el servicio de almacenamiento según configuración
    /// </summary>
    public class FileStorageFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly StorageSettings _storageSettings;

        public FileStorageFactory(
            IServiceProvider serviceProvider,
            IOptions<StorageSettings> storageSettings)
        {
            _serviceProvider = serviceProvider;
            _storageSettings = storageSettings.Value;
        }

        public IFileStorageService GetStorageService()
        {
            return GetStorageService(_storageSettings.Provider);
        }

        public IFileStorageService GetStorageService(string provider)
        {
            return provider.ToUpper() switch
            {
                "S3" => _serviceProvider.GetRequiredService<S3StorageService>(),
                "ONEDRIVE" => _serviceProvider.GetRequiredService<OneDriveStorageService>(),
                _ => throw new NotSupportedException($"Storage provider '{provider}' no está soportado")
            };
        }

        public string GetCurrentProvider()
        {
            return _storageSettings.Provider;
        }
    }
}