using Newtonsoft.Json.Linq;

namespace esupplier.Utils
{
    /// <summary>
    /// Lectura segura de campos de los JObject recibidos en los requests.
    /// Evita NullReferenceException por campos ausentes y da un mensaje claro de qué falta.
    /// </summary>
    public static class JsonUtil
    {
        /// <summary>Devuelve el valor del campo como string; lanza <see cref="CampoRequeridoException"/> si no existe o es null.</summary>
        public static string Requerido(JObject obj, string campo)
        {
            JToken token = obj?[campo];
            if (token == null || token.Type == JTokenType.Null)
            {
                throw new CampoRequeridoException(campo);
            }
            return token.ToString();
        }

        /// <summary>Devuelve el valor del campo como string, o <paramref name="porDefecto"/> si no existe o es null.</summary>
        public static string Opcional(JObject obj, string campo, string porDefecto = "")
        {
            JToken token = obj?[campo];
            return token == null || token.Type == JTokenType.Null ? porDefecto : token.ToString();
        }

        /// <summary>Lee y parsea el body de la petición como JObject.</summary>
        public static async Task<JObject> LeerBodyAsync(HttpRequest request)
        {
            using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8);
            string contenido = await reader.ReadToEndAsync();
            return string.IsNullOrWhiteSpace(contenido) ? new JObject() : JObject.Parse(contenido);
        }
    }

    /// <summary>Se lanza cuando falta un campo obligatorio en el request. Su mensaje es seguro para mostrar al cliente.</summary>
    public class CampoRequeridoException : Exception
    {
        public string Campo { get; }

        public CampoRequeridoException(string campo) : base($"Falta el campo obligatorio '{campo}'.")
        {
            Campo = campo;
        }
    }
}
