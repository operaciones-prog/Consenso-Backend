using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using esupplier.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys
{
    public class HESRepository : IHESRepository
    {
        // Traducción de TIPO_SERVICIO cuando el idioma no es español.
        private static readonly Dictionary<string, string> TipoServicioEN = new()
        {
            ["Mercaderia"] = "Merchandise",
            ["Servicio"] = "Service"
        };

        private readonly IConfiguration _configuration;
        private readonly IHeaderService _headerService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HESRepository> _logger;

        public HESRepository(IConfiguration configuration, IHeaderService headerService, IHttpClientFactory httpClientFactory, ILogger<HESRepository> logger)
        {
            _configuration = configuration;
            _headerService = headerService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<Mensaje> buscarHES(JObject jObject)
        {
            string responseBody;
            try
            {
                HttpClient cliente = _httpClientFactory.CreateClient(Constantes.HTTP_CLIENT_SAP);
                responseBody = await ConsultaUtil.PostSapAsync(cliente, _configuration["Http:Consultahesmigo"], jObject.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar HES en SAP");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new Mensaje("E", "Sin respuesta - null", "");
            }

            try
            {
                JObject objJsonAux = JObject.Parse(responseBody);

                JToken totalRows = objJsonAux["E_TOTALROWS"];
                if (totalRows == null || !(objJsonAux["ET_MHCAB"]?["item"] is JArray arrayMHCAB))
                {
                    return new Mensaje("E", "No se encontraron registros", "");
                }

                if (_headerService.obtenerIdioma() != "ES")
                {
                    foreach (JObject cabecera in arrayMHCAB.OfType<JObject>())
                    {
                        if (cabecera["TIPO_SERVICIO"] is JValue valor && valor.Type == JTokenType.String
                            && TipoServicioEN.TryGetValue((string)valor, out string traducido))
                        {
                            cabecera["TIPO_SERVICIO"] = traducido;
                        }
                    }
                }

                // SAP envía E_TOTALROWS como texto; históricamente se devuelve como número sin comillas.
                string totalRowsJson = totalRows.Type == JTokenType.String
                    && long.TryParse(((string)totalRows).Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long filas)
                    ? filas.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : totalRows.ToString(Formatting.None);

                string data = "\"E_TOTALROWS\":" + totalRowsJson
                            + ",\"ET_MHCAB\":" + arrayMHCAB.ToString();

                if (JsonUtil.Opcional(jObject, "I_INDICADOR") == "2")
                {
                    if (!(objJsonAux["ET_MHDET"]?["item"] is JArray arrayMHDET))
                    {
                        return new Mensaje("E", "No se encontraron registros", "");
                    }
                    data += ",\"ET_MHDET\":" + arrayMHDET.ToString();
                }

                return new Mensaje("S", "", data);
            }
            catch (JsonReaderException ex)
            {
                _logger.LogError(ex, "Respuesta de SAP (HES) no es un JSON válido");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }
        }
    }
}
