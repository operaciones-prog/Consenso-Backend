namespace esupplier.Models
{
    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string? FileId { get; set; }
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}