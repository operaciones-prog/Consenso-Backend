namespace esupplier.Models
{
    public class FileDownloadResult
    {
        public bool Success { get; set; }
        public Stream? FileStream { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSize { get; set; }
        public string? ErrorMessage { get; set; }
    }
}