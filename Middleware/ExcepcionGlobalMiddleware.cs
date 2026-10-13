using esupplier.Utils;

namespace esupplier.Middleware
{
    /// <summary>
    /// Captura cualquier excepción no controlada, la registra con el detalle completo
    /// y devuelve al cliente el formato estándar {"message","type":"E"} sin stack trace.
    /// </summary>
    public class ExcepcionGlobalMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExcepcionGlobalMiddleware> _logger;

        public ExcepcionGlobalMiddleware(RequestDelegate next, ILogger<ExcepcionGlobalMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (ex is CampoRequeridoException && !context.Response.HasStarted)
            {
                // Error de validación: el mensaje es seguro para el cliente.
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(ApiRespuesta.Error(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción no controlada en {Metodo} {Ruta}", context.Request.Method, context.Request.Path);

                if (context.Response.HasStarted) throw;

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(ApiRespuesta.Error(ApiRespuesta.MensajeErrorGenerico));
            }
        }
    }
}
