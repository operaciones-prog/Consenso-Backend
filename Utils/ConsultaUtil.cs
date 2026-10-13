using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace esupplier.Utils
{
    public class ConsultaUtil
    {
        public static string generarCorreoRechazo(string numero_oc, string proveedor, string motivo)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Estimados,");
            sb.AppendLine("<br>");
            sb.Append("La orden N° ");
            sb.Append(numero_oc);
            sb.Append(" del proveedor ");
            sb.Append(proveedor);
            sb.Append(" fue RECHAZADA con el siguiente motivo: ");
            sb.Append(motivo);
            sb.Append(".");
            sb.AppendLine("<br>");
            sb.Append("Por favor su respectiva gestión.");

            return sb.ToString();
        }

        public static string generarCorreoOCPendiente(string numero_oc, string razon_social, string fecha_publicacion, string uri_portal, string idioma)
        {
            StringBuilder sb = new StringBuilder();
            if (idioma.Equals("ES"))
            {
                sb.Append("Estimados,");
                sb.AppendLine("<br>");
                sb.Append("La orden N° ");
                sb.Append(numero_oc);
                sb.Append(" correspondiente a ");
                sb.Append(razon_social);
                sb.Append(" fue generada y publicada con fecha: ");
                sb.Append(fecha_publicacion);
                sb.Append(", por el cual se requiere su pronta verificación através de la web:");
                sb.AppendLine("<br>");
                sb.Append(uri_portal);
                sb.AppendLine("<br>");
                sb.Append("Quedamos a la espera de su respectiva confirmación.");
            }
            else
            {
                sb.Append("Dear all,");
                sb.AppendLine("<br>");
                sb.Append("The order N° ");
                sb.Append(numero_oc);
                sb.Append(" referred to ");
                sb.Append(razon_social);
                sb.Append(" and published with date: ");
                sb.Append(fecha_publicacion);
                sb.Append(", is required your prompt verification through the web side:");
                sb.AppendLine("<br>");
                sb.Append(uri_portal);
                sb.AppendLine("<br>");
                sb.Append("We are waiting for your respective confirmation.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convierte un texto numérico (celda de Excel) a double, independiente de la cultura del servidor.
        /// Regla: si aparecen ',' y '.', el separador que aparece al final es el decimal y el otro se descarta (miles);
        /// si solo aparece ',' se toma como decimal ("85,5" = 85.5); si solo aparece '.' es decimal ("85.5" = 85.5).
        /// Con <paramref name="validaPorcentaje"/>: "85%" = 85; un valor sin '%' se considera fracción (0.85 = 85).
        /// Un texto vacío o no numérico devuelve 0.
        /// </summary>
        public static double retornaDouble(string stringValue, bool validaPorcentaje)
        {
            string texto = (stringValue ?? "").Trim().Replace(" ", "");
            double multiplica = 1;

            if (validaPorcentaje)
            {
                if (texto.Contains('%'))
                {
                    texto = texto.Replace("%", "");
                }
                else
                {
                    multiplica = 100;
                }
            }

            int ultimaComa = texto.LastIndexOf(',');
            int ultimoPunto = texto.LastIndexOf('.');
            if (ultimaComa >= 0 && ultimoPunto >= 0)
            {
                texto = ultimaComa > ultimoPunto
                    ? texto.Replace(".", "").Replace(',', '.')
                    : texto.Replace(",", "");
            }
            else if (ultimaComa >= 0)
            {
                texto = texto.Replace(',', '.');
            }

            if (!double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out double valor))
            {
                valor = 0;
            }
            return valor * multiplica;
        }

        public static string GetFileExtension(string fileName)
        {
            // Extraer la extensión del archivo
            if (string.IsNullOrEmpty(fileName)) return string.Empty;

            int lastDotIndex = fileName.LastIndexOf('.');
            return (lastDotIndex == -1 || lastDotIndex == fileName.Length - 1) ? string.Empty : fileName.Substring(lastDotIndex + 1);
        }

        /// <summary>
        /// POST JSON a un servicio de SAP CPI con el HttpClient nombrado <see cref="Constantes.HTTP_CLIENT_SAP"/>
        /// (Authorization y timeout ya configurados). Lanza <see cref="HttpRequestException"/> si el estado no es 2xx.
        /// </summary>
        public static async Task<string> PostSapAsync(HttpClient cliente, string url, string json)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("URL de SAP no configurada.");

            using var contenido = new StringContent(json ?? "", Encoding.UTF8);
            contenido.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = contenido };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage response = await cliente.SendAsync(request);
            string cuerpo = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"SAP respondió {(int)response.StatusCode} en {url}", null, response.StatusCode);
            }
            return cuerpo;
        }
    }
}
