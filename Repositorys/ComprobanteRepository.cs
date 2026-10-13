using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.Repositorys
{
    public class ComprobanteRepository : IComprobanteRepository
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ComprobanteRepository> _logger;

        public ComprobanteRepository(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<ComprobanteRepository> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<Mensaje> buscarComprobantes(JObject jObject)
        {
            string responseBody;
            try
            {
                HttpClient cliente = _httpClientFactory.CreateClient(Constantes.HTTP_CLIENT_SAP);
                responseBody = await ConsultaUtil.PostSapAsync(cliente, _configuration["Http:Consultarcomprobantes"], jObject.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar comprobantes en SAP");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new Mensaje("E", "Sin respuesta - null", "");
            }

            try
            {
                JObject objJsonAux = JObject.Parse(responseBody);
                if (!(objJsonAux["ET_DOCPAGO"]?["item"] is JArray arrayDOCPAGO))
                {
                    return new Mensaje("E", "No se encontraron registros", "");
                }

                return new Mensaje("S", "", "\"ET_DOCPAGO\":" + arrayDOCPAGO.ToString());
            }
            catch (JsonReaderException ex)
            {
                _logger.LogError(ex, "Respuesta de SAP (comprobantes) no es un JSON válido");
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }
        }
    }
}
