using esupplier.Models;

namespace esupplier.Services.IServices
{
    /// <summary>
    /// Interfaz común para servicios de almacenamiento de archivos (OneDrive, S3, etc.)
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Sube un archivo al almacenamiento
        /// </summary>
        /// <param name="fileContent">Contenido del archivo en bytes</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <param name="folderPath">Ruta de la carpeta</param>
        /// <returns>URL o ID del archivo subido</returns>
        Task<FileUploadResult> UploadFileAsync(byte[] fileContent, string fileName, string folderPath);

        /// <summary>
        /// Descarga un archivo del almacenamiento
        /// </summary>
        /// <param name="filePath">Ruta completa del archivo</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <returns>Stream del archivo</returns>
        Task<FileDownloadResult> DownloadFileAsync(string filePath, string fileName);

        /// <summary>
        /// Genera una URL de descarga temporal (pre-signed URL)
        /// </summary>
        /// <param name="filePath">Ruta del archivo</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <param name="expirationMinutes">Minutos de expiración (default: 60)</param>
        /// <returns>URL temporal de descarga</returns>
        Task<string> GenerateDownloadUrlAsync(string filePath, string fileName, int expirationMinutes = 60);

        /// <summary>
        /// Lista archivos en una carpeta
        /// </summary>
        /// <param name="folderPath">Ruta de la carpeta</param>
        /// <returns>Lista de archivos</returns>
        Task<List<FileItem>> ListFilesAsync(string folderPath);

        /// <summary>
        /// Elimina un archivo
        /// </summary>
        /// <param name="filePath">Ruta del archivo</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <returns>True si se eliminó correctamente</returns>
        Task<bool> DeleteFileAsync(string filePath, string fileName);

        /// <summary>
        /// Verifica si un archivo existe
        /// </summary>
        /// <param name="filePath">Ruta del archivo</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <returns>True si existe</returns>
        Task<bool> FileExistsAsync(string filePath, string fileName);
    }
}