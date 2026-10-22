using Anthropic;
using Anthropic.Exceptions;

namespace esupplier.Services.IA
{
    public enum ResultadoVerificacion
    {
        Valida,
        /// <summary>La clave existe pero el proveedor limitó las peticiones (429): se acepta.</summary>
        ValidaConLimite,
        Invalida,
        SinPermisos,
        NoDisponible
    }

    public interface IVerificadorClaveIA
    {
        /// <summary>Comprueba la clave sin consumir tokens.</summary>
        Task<ResultadoVerificacion> VerificarAsync(string proveedor, string apiKey);
    }

    /// <summary>
    /// Verifica la clave listando los modelos (GET /v1/models): no genera texto ni consume tokens.
    /// </summary>
    public class VerificadorClaveIA : IVerificadorClaveIA
    {
        private readonly ILogger<VerificadorClaveIA> _logger;

        public VerificadorClaveIA(ILogger<VerificadorClaveIA> logger)
        {
            _logger = logger;
        }

        public async Task<ResultadoVerificacion> VerificarAsync(string proveedor, string apiKey)
        {
            if (proveedor != ProveedoresIA.Anthropic)
            {
                throw new ArgumentException("Proveedor de IA no soportado.");
            }

            try
            {
                var cliente = new AnthropicClient { ApiKey = apiKey };
                await cliente.Models.List();
                return ResultadoVerificacion.Valida;
            }
            catch (AnthropicUnauthorizedException)
            {
                return ResultadoVerificacion.Invalida;
            }
            catch (AnthropicForbiddenException)
            {
                return ResultadoVerificacion.SinPermisos;
            }
            catch (AnthropicRateLimitException)
            {
                return ResultadoVerificacion.ValidaConLimite;
            }
            catch (Exception ex) when (ex is Anthropic5xxException || ex is AnthropicIOException || ex is AnthropicApiException || ex is TaskCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo verificar la API key de {Proveedor}", proveedor);
                return ResultadoVerificacion.NoDisponible;
            }
        }
    }

    public static class ProveedoresIA
    {
        public const string Anthropic = "anthropic";

        /// <summary>Modelos que el administrador puede elegir; el primero es el predeterminado.</summary>
        public static readonly string[] ModelosAnthropic = { "claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5" };
    }
}
