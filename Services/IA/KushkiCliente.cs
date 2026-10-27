using esupplier.Models.IA;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace esupplier.Services.IA
{
    /// <summary>Rechazo o error de Kushki. El mensaje es apto para mostrarlo al cliente.</summary>
    public class KushkiException : Exception
    {
        /// <summary>true = la tarjeta fue rechazada; false = error de datos, conexión o configuración.</summary>
        public bool Rechazo { get; }

        public KushkiException(string mensaje, bool rechazo) : base(mensaje)
        {
            Rechazo = rechazo;
        }
    }

    /// <summary>Monto de Kushki en US$: subtotal gravado con IVA (sin ICE ni tarifa 0 %).</summary>
    public record MontoKushki(decimal Subtotal, decimal Iva)
    {
        public decimal Total => Subtotal + Iva;
    }

    /// <summary>Suscripción creada: id y la tarjeta que Kushki informa (marca y últimos 4).</summary>
    public record SuscripcionKushki(string Id, string Marca, string Ultimos4);

    /// <summary>Resultado de comprobar un par de llaves contra Kushki (sin cobrar nada).</summary>
    public enum VerificacionKushki
    {
        Valida,
        PublicaInvalida,
        PrivadaInvalida,
        /// <summary>Kushki no respondió: no se sabe si las llaves sirven.</summary>
        NoDisponible
    }

    public interface IKushkiCliente
    {
        /// <summary>
        /// Comprueba las llaves en el ambiente indicado: la pública con la configuración del comercio (lo que
        /// consulta Hosted Fields) y la privada consultando una suscripción inexistente (con una llave válida
        /// Kushki responde "no existe", con una inválida 401/403).
        /// </summary>
        Task<VerificacionKushki> VerificarAsync(bool prueba, string llavePublica, string llavePrivada);

        /// <summary>
        /// Crea una suscripción mensual con el token (de un solo uso) de Hosted Fields. Kushki valida la tarjeta
        /// al instante con un cargo que revierte, y cobra el primer mes en <paramref name="inicio"/> (fecha futura).
        /// </summary>
        Task<SuscripcionKushki> CrearSuscripcionAsync(string token, string nombrePlan, ContactoPlanIA contacto, MontoKushki monto,
            DateTime inicio, IDictionary<string, string> metadata);

        /// <summary>Cambia el plan y el monto: se aplica desde el próximo cobro.</summary>
        Task ActualizarSuscripcionAsync(string suscripcionId, string nombrePlan, MontoKushki monto);

        /// <summary>Reemplaza la tarjeta de la suscripción por la del token.</summary>
        Task CambiarTarjetaAsync(string suscripcionId, string token);

        /// <summary>Elimina la suscripción: Kushki no vuelve a cobrar.</summary>
        Task CancelarSuscripcionAsync(string suscripcionId);

        /// <summary>
        /// Verifica la firma del webhook (HMAC SHA256 en hex con el id de firma de webhooks de la consola de Kushki):
        /// X-Kushki-Signature = HMAC(body + "." + X-Kushki-Id); X-Kushki-SimpleSignature = HMAC(X-Kushki-Id).
        /// </summary>
        Task<bool> FirmaValidaAsync(string cuerpo, string kushkiId, string firma, string firmaSimple);
    }

    /// <summary>
    /// API de suscripciones de Kushki Ecuador (https://api.kushkipagos.com, pruebas: api-uat) con la llave
    /// privada (Private-Merchant-Id). Los datos de la tarjeta nunca pasan por el backend: el navegador los
    /// convierte en un token con Kushki Hosted Fields y aquí solo se usa ese token.
    /// Las llaves y el modo salen de <see cref="IConfiguracionKushki"/> (Plataforma > Pago con tarjeta).
    /// </summary>
    public class KushkiCliente : IKushkiCliente
    {
        public const string UrlProduccion = "https://api.kushkipagos.com/";
        public const string UrlPruebas = "https://api-uat.kushkipagos.com/";

        private const string MensajeNoDisponible = "No se pudo contactar a Kushki. Intente nuevamente en unos minutos.";
        private const string MensajeCredenciales = "El pago con tarjeta no está configurado correctamente. Contacte al administrador de la plataforma.";

        /// <summary>Id con formato válido que no corresponde a ninguna suscripción (para verificar la llave privada).</summary>
        private const string SuscripcionInexistente = "0000000000000000";

        private readonly HttpClient _http;
        private readonly IConfiguracionKushki _configuracion;
        private readonly ILogger<KushkiCliente> _logger;

        public KushkiCliente(HttpClient http, IConfiguracionKushki configuracion, ILogger<KushkiCliente> logger)
        {
            _http = http;
            // Dos llamadas por petición como máximo: deben caber en los 29 s de API Gateway
            _http.Timeout = TimeSpan.FromSeconds(12);
            _configuracion = configuracion;
            _logger = logger;
        }

        public async Task<VerificacionKushki> VerificarAsync(bool prueba, string llavePublica, string llavePrivada)
        {
            var baseUri = new Uri(prueba ? UrlPruebas : UrlProduccion);
            try
            {
                using var publica = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "merchant/v1/merchant/settings"));
                publica.Headers.TryAddWithoutValidation("Public-Merchant-Id", llavePublica);
                using HttpResponseMessage rPublica = await _http.SendAsync(publica);
                if ((int)rPublica.StatusCode >= 500) return VerificacionKushki.NoDisponible;
                if (!rPublica.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Kushki no aceptó la llave pública ({Status})", (int)rPublica.StatusCode);
                    return VerificacionKushki.PublicaInvalida;
                }

                using var privada = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "subscriptions/v1/card/" + SuscripcionInexistente));
                privada.Headers.TryAddWithoutValidation("Private-Merchant-Id", llavePrivada);
                using HttpResponseMessage rPrivada = await _http.SendAsync(privada);
                string texto = await rPrivada.Content.ReadAsStringAsync();
                string codigo = null;
                try
                {
                    codigo = Texto(string.IsNullOrWhiteSpace(texto) ? null : JObject.Parse(texto), "code");
                }
                catch (JsonReaderException)
                {
                }
                _logger.LogInformation("Verificación de la llave privada de Kushki: {Status} {Codigo}", (int)rPrivada.StatusCode, codigo);

                // K004: id de comercio no válido
                if (rPrivada.StatusCode == HttpStatusCode.Unauthorized || rPrivada.StatusCode == HttpStatusCode.Forbidden || codigo == "K004")
                    return VerificacionKushki.PrivadaInvalida;
                return (int)rPrivada.StatusCode >= 500 ? VerificacionKushki.NoDisponible : VerificacionKushki.Valida;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                _logger.LogWarning(ex, "Kushki no respondió al verificar las llaves");
                return VerificacionKushki.NoDisponible;
            }
        }

        public async Task<SuscripcionKushki> CrearSuscripcionAsync(string token, string nombrePlan, ContactoPlanIA contacto, MontoKushki monto,
            DateTime inicio, IDictionary<string, string> metadata)
        {
            var cuerpo = new JObject
            {
                ["token"] = token,
                ["planName"] = nombrePlan,
                ["periodicity"] = "monthly",
                ["contactDetails"] = Contacto(contacto),
                ["amount"] = Monto(monto),
                ["startDate"] = inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["language"] = "es",
                ["metadata"] = JObject.FromObject(metadata),
                // v3 devuelve la tarjeta (marca y últimos 4) en "details"
                ["fullResponse"] = "v3"
            };
            JObject r = await EnviarAsync(HttpMethod.Post, "subscriptions/v1/card", cuerpo);

            string id = Texto(r, "subscriptionId");
            if (string.IsNullOrEmpty(id))
            {
                // Rechazo al validar la tarjeta: Kushki responde 201 con status DECLINED, code y message
                _logger.LogWarning("Kushki rechazó la suscripción ({Codigo}): {Mensaje}", Texto(r, "code"), Texto(r, "message"));
                throw new KushkiException(Texto(r, "message") ?? "La tarjeta fue rechazada. Intente con otra tarjeta.", rechazo: true);
            }
            JObject detalle = r["details"] as JObject;
            return new SuscripcionKushki(id, Recortar(Texto(detalle, "paymentBrand"), 30), Recortar(Texto(detalle, "lastFourDigits"), 4));
        }

        public Task ActualizarSuscripcionAsync(string suscripcionId, string nombrePlan, MontoKushki monto) =>
            EnviarAsync(new HttpMethod("PATCH"), "subscriptions/v1/card/" + Uri.EscapeDataString(suscripcionId),
                new JObject { ["planName"] = nombrePlan, ["amount"] = Monto(monto) });

        public Task CambiarTarjetaAsync(string suscripcionId, string token) =>
            EnviarAsync(HttpMethod.Put, "subscriptions/v1/card/" + Uri.EscapeDataString(suscripcionId) + "/card",
                new JObject { ["token"] = token });

        public Task CancelarSuscripcionAsync(string suscripcionId) =>
            EnviarAsync(HttpMethod.Delete, "subscriptions/v1/card/" + Uri.EscapeDataString(suscripcionId), null);

        public async Task<bool> FirmaValidaAsync(string cuerpo, string kushkiId, string firma, string firmaSimple)
        {
            if (string.IsNullOrEmpty(kushkiId)) return false;
            string secreto = (await _configuracion.CredencialesAsync()).SecretoWebhook;
            if (string.IsNullOrEmpty(secreto)) return false;
            // Se acepta cualquiera de las dos firmas (la documentación permite usar una u otra)
            if (!string.IsNullOrEmpty(firma) && Iguales(Hmac(secreto, cuerpo + "." + kushkiId), firma)) return true;
            return !string.IsNullOrEmpty(firmaSimple) && Iguales(Hmac(secreto, kushkiId), firmaSimple);
        }

        private static string Hmac(string secreto, string texto)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
        }

        private static bool Iguales(string esperado, string recibido) =>
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(esperado), Encoding.ASCII.GetBytes(recibido.Trim().ToLowerInvariant()));

        // ================================ HTTP ================================

        private async Task<JObject> EnviarAsync(HttpMethod metodo, string ruta, JObject cuerpo)
        {
            CredencialesKushki credenciales = await _configuracion.CredencialesAsync();
            if (!credenciales.Completa) throw new KushkiException(MensajeCredenciales, rechazo: false);

            using var peticion = new HttpRequestMessage(metodo, new Uri(new Uri(credenciales.UrlBase), ruta));
            peticion.Headers.TryAddWithoutValidation("Private-Merchant-Id", credenciales.LlavePrivada);
            if (cuerpo != null)
            {
                peticion.Content = new StringContent(cuerpo.ToString(Formatting.None), Encoding.UTF8, "application/json");
            }

            HttpResponseMessage respuesta;
            string texto;
            try
            {
                respuesta = await _http.SendAsync(peticion);
                texto = await respuesta.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                _logger.LogError(ex, "Kushki no respondió ({Metodo} {Ruta})", metodo, RutaLog(ruta));
                throw new KushkiException(MensajeNoDisponible, rechazo: false);
            }

            JObject json = null;
            try
            {
                json = string.IsNullOrWhiteSpace(texto) ? new JObject() : JObject.Parse(texto);
            }
            catch (JsonReaderException)
            {
            }

            if (respuesta.IsSuccessStatusCode) return json ?? new JObject();

            string codigo = Texto(json, "code");
            string mensaje = Texto(json, "message");
            _logger.LogWarning("Kushki rechazó {Metodo} {Ruta} ({Status}, {Codigo}): {Mensaje}",
                metodo, RutaLog(ruta), (int)respuesta.StatusCode, codigo, mensaje ?? texto);

            if (respuesta.StatusCode == HttpStatusCode.Unauthorized || respuesta.StatusCode == HttpStatusCode.Forbidden)
                throw new KushkiException(MensajeCredenciales, rechazo: false);
            if ((int)respuesta.StatusCode >= 500) throw new KushkiException(MensajeNoDisponible, rechazo: false);
            // 402: tarjeta no habilitada por el emisor; K322: rechazo en la validación 3DS
            bool rechazo = respuesta.StatusCode == HttpStatusCode.PaymentRequired || codigo == "K322" || codigo == "K021";
            throw new KushkiException(mensaje ?? "Kushki rechazó la operación.", rechazo);
        }

        /// <summary>Para el log: sin el id de la suscripción.</summary>
        private static string RutaLog(string ruta) => ruta.StartsWith("subscriptions/v1/card/") ? "subscriptions/v1/card/{id}" : ruta;

        private static JObject Monto(MontoKushki m) => new JObject
        {
            ["subtotalIva"] = Math.Round(m.Subtotal, 2),
            ["subtotalIva0"] = 0m,
            ["iva"] = Math.Round(m.Iva, 2),
            ["ice"] = 0m,
            ["currency"] = "USD"
        };

        private static JObject Contacto(ContactoPlanIA c) => new JObject
        {
            ["firstName"] = c.nombres,
            ["lastName"] = c.apellidos,
            ["email"] = c.email,
            ["documentType"] = c.tipo_documento,
            ["documentNumber"] = c.numero_documento,
            ["phoneNumber"] = c.telefono
        };

        private static string Texto(JObject o, string propiedad) =>
            o?[propiedad] is JValue v && v.Type != JTokenType.Null ? Convert.ToString(v.Value, CultureInfo.InvariantCulture) : null;

        private static string Recortar(string t, int max) => t == null ? null : t.Length <= max ? t : t[..max];
    }
}
