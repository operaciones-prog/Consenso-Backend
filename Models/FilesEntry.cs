namespace esupplier.Models
{
    public class FilesEntry
    {
        public byte[] FileBytes { get; set; }
        public string FileName { get; set; }
        public string FileId { get; set; }



        public FilesEntry(string fileId, string fileName)
        {
            FileId = fileId;
            FileName = fileName;
        }
    }
}
