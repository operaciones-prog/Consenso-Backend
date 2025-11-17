namespace esupplier.Models
{
    public class FileItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public DateTime? CreatedDate { get; set; }
        public long? Size { get; set; }
    }
}