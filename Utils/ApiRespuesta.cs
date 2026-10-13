using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.Utils
{
    /// <summary>
    /// Construye las respuestas JSON de la API con el mismo formato que espera el frontend
    /// ({"message","type","data"}), pero serializando correctamente (comillas, saltos de línea, etc.)
    /// en lugar de concatenar strings.
    /// </summary>
    public static class ApiRespuesta
    {
        public const string MensajeErrorGenerico = "Ocurrió un error inesperado. Intente nuevamente o contacte al administrador.";

        /// <summary>{"message":"...","type":"S"}</summary>
        public static string Exito(string mensaje)
        {
            return new JObject
            {
                ["message"] = mensaje ?? "",
                ["type"] = "S"
            }.ToString(Formatting.None);
        }

        /// <summary>
        /// {"message":"...","type":"S","data":{ fragmento }}
        /// <paramref name="fragmentoObjeto"/> es el contenido interno de un objeto JSON, p. ej. "\"constantes\":[...]".
        /// </summary>
        public static string ExitoConObjeto(string mensaje, string fragmentoObjeto)
        {
            return new JObject
            {
                ["message"] = mensaje ?? "",
                ["type"] = "S",
                ["data"] = ParsearLiteral("{" + (fragmentoObjeto ?? "") + "}")
            }.ToString(Formatting.None);
        }

        /// <summary>{"message":"...","type":"S","data": json }  (json completo: arreglo, objeto o valor)</summary>
        public static string ExitoConJson(string mensaje, string json)
        {
            return new JObject
            {
                ["message"] = mensaje ?? "",
                ["type"] = "S",
                ["data"] = string.IsNullOrWhiteSpace(json) ? JValue.CreateNull() : ParsearLiteral(json)
            }.ToString(Formatting.None);
        }

        /// <summary>{"type":"S","data": json } (variante sin "message" usada por algunos endpoints)</summary>
        public static string ExitoSoloData(string json)
        {
            return new JObject
            {
                ["type"] = "S",
                ["data"] = string.IsNullOrWhiteSpace(json) ? JValue.CreateNull() : ParsearLiteral(json)
            }.ToString(Formatting.None);
        }

        /// <summary>{"type":"S","data": token }</summary>
        public static string ExitoSoloData(JToken data)
        {
            return new JObject
            {
                ["type"] = "S",
                ["data"] = data ?? JValue.CreateNull()
            }.ToString(Formatting.None);
        }

        /// <summary>
        /// Parsea JSON sin convertir fechas ni números, para que los valores lleguen al frontend
        /// exactamente como los generó el repositorio (mismo formato que la concatenación anterior).
        /// </summary>
        private static JToken ParsearLiteral(string json)
        {
            using var lector = new JsonTextReader(new StringReader(json))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal
            };
            return JToken.ReadFrom(lector);
        }

        /// <summary>{"message":"...","type":"E"}</summary>
        public static string Error(string mensaje)
        {
            return new JObject
            {
                ["message"] = mensaje ?? "",
                ["type"] = "E"
            }.ToString(Formatting.None);
        }

        /// <summary>
        /// Registra la excepción completa en el log y devuelve un mensaje genérico,
        /// sin exponer el stack trace al cliente.
        /// </summary>
        public static string ErrorInesperado(ILogger logger, Exception ex, string contexto)
        {
            logger?.LogError(ex, "Error no controlado en {Contexto}", contexto);
            return Error(MensajeErrorGenerico);
        }

        /// <summary>Convierte un Mensaje de la capa de servicio en la respuesta estándar con data de tipo objeto.</summary>
        public static string DesdeMensajeConObjeto(Models.Response.Mensaje msj)
        {
            if (msj == null) return Error(MensajeErrorGenerico);
            return msj.tipo == "S" ? ExitoConObjeto(msj.mensaje, msj.data) : Error(msj.mensaje);
        }

        /// <summary>Convierte un Mensaje de la capa de servicio en la respuesta estándar sin data.</summary>
        public static string DesdeMensaje(Models.Response.Mensaje msj)
        {
            if (msj == null) return Error(MensajeErrorGenerico);
            return msj.tipo == "S" ? Exito(msj.mensaje) : Error(msj.mensaje);
        }
    }
}
