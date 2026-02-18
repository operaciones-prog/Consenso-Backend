namespace esupplier.Config
{
    public class StorageSettings
    {
        public string Provider { get; set; } = "OneDrive"; // "OneDrive" o "S3"
        public OneDriveSettings OneDrive { get; set; } = new();
        public S3Settings S3 { get; set; } = new();
    }

    public class OneDriveSettings
    {
        /// <summary>
        /// Se obtienen de la BD via ConfiguracionService
        /// </summary>
        public bool UseDatabase { get; set; } = true;

        /// <summary>
        /// Valores opcionales si no se usa BD
        /// </summary>
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? TenantId { get; set; }
        public string? UserEmail { get; set; }
        public string? TemporalPath { get; set; } = "/temporal/";
    }

    public class S3Settings
    {
        public string BucketName { get; set; } = string.Empty;
        public string Region { get; set; } = "us-east-1";
        public bool UseIAMRole { get; set; } = true;
        public string? AccessKeyId { get; set; }
        public string? SecretAccessKey { get; set; }
        public string? BasePrefix { get; set; } = "consenso/";
    }
}