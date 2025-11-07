using System.Text;
using static System.Net.WebRequestMethods;

namespace esupplier.Utils
{
    public class ConsultaUtil
    {

        public static string qrLeerOC = "";

        public static string generarCodigo(int longitud)
        {
            string codigo = "";

            var characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var Charsarr = new char[longitud];
            var random = new Random();

            for (int i = 0; i < Charsarr.Length; i++)
            {
                Charsarr[i] = characters[random.Next(characters.Length)];
            }

            codigo = new String(Charsarr);

            return codigo;
        }

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


        public static double retornaDouble(string stringValue,Boolean validaPorcentaje)
        {
            double multiplica = 1;
            
            if(validaPorcentaje)
            {
                if (stringValue.Contains("%"))
                {
                    stringValue = stringValue.Replace("%", "");
                }
                else
                {
                    multiplica = 100;
                }
                   
            }

            double valor=0; 

            if (int.TryParse(stringValue, out int intValue))
            {
                valor += intValue;
            }
            else if (double.TryParse(stringValue, out double doubleValue))
            {

                valor += doubleValue;
            }
            return valor* multiplica;
        }

        public static string GetFileExtension(string fileName)
        {
            // Extraer la extensión del archivo
            if (string.IsNullOrEmpty(fileName)) return string.Empty;

            int lastDotIndex = fileName.LastIndexOf('.');
            return (lastDotIndex == -1 || lastDotIndex == fileName.Length - 1) ? string.Empty : fileName.Substring(lastDotIndex + 1);
        }

    }
}
