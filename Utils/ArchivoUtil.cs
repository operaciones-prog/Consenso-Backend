namespace esupplier.Utils
{
    /// <summary>
    /// Validaciones de archivos subidos por los usuarios: nombre seguro (sin rutas), extensión permitida y tamaño máximo.
    /// </summary>
    public static class ArchivoUtil
    {
        /// <summary>Tamaño máximo por archivo. Lambda limita el payload a 6 MB y el base64 agrega ~33 %.</summary>
        public const long TamanoMaximoBytes = 4 * 1024 * 1024;

        private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".xml", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff",
            ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".ppt", ".pptx", ".zip", ".rar", ".7z", ".msg", ".eml"
        };

        /// <summary>
        /// Quita cualquier ruta del nombre (../, /, \) y caracteres no válidos.
        /// Lanza <see cref="ArgumentException"/> si el nombre queda vacío o la extensión no está permitida.
        /// </summary>
        public static string NombreSeguro(string nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo)) throw new ArgumentException("El nombre del archivo es obligatorio.");

            string nombre = Path.GetFileName(nombreArchivo.Replace('\\', '/'));
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                nombre = nombre.Replace(c, '_');
            }
            nombre = nombre.Trim().TrimStart('.');

            if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre del archivo no es válido.");

            string extension = Path.GetExtension(nombre);
            if (!ExtensionesPermitidas.Contains(extension))
            {
                throw new ArgumentException($"El tipo de archivo '{extension}' no está permitido.");
            }
            return nombre;
        }

        /// <summary>Limpia un segmento de ruta (sociedad, proveedor, carpeta) para que no contenga separadores ni '..'.</summary>
        public static string SegmentoSeguro(string segmento)
        {
            if (string.IsNullOrWhiteSpace(segmento)) throw new ArgumentException("Segmento de ruta vacío.");
            string limpio = segmento.Replace("..", "").Replace("/", "").Replace("\\", "").Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                limpio = limpio.Replace(c.ToString(), "");
            }
            if (string.IsNullOrWhiteSpace(limpio)) throw new ArgumentException("Segmento de ruta no válido.");
            return limpio;
        }

        /// <summary>Decodifica base64 validando el tamaño máximo.</summary>
        public static byte[] DecodificarBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) throw new ArgumentException("El contenido del archivo está vacío.");
            int coma = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma > 0)
            {
                base64 = base64.Substring(coma + 1);
            }
            byte[] bytes = Convert.FromBase64String(base64);
            if (bytes.LongLength > TamanoMaximoBytes)
            {
                throw new ArgumentException($"El archivo supera el tamaño máximo de {TamanoMaximoBytes / (1024 * 1024)} MB.");
            }
            return bytes;
        }
    }
}
