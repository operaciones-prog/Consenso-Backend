using esupplier.Services.Auth;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace esupplier.Middleware.Autorizacion
{
    /// <summary>
    /// Restringe el endpoint a usuarios internos (codigo_referencia = "0").
    /// Un usuario de proveedor recibe 403 con el formato estándar {"message","type":"E"}.
    /// Sin token (Auth:Enabled=false) no hace nada, para no cambiar el comportamiento actual.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class SoloInternosAttribute : Attribute, IAsyncActionFilter
    {
        public const string MensajeNoAutorizado = "No tiene permisos para realizar esta operación.";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var usuario = context.HttpContext.RequestServices.GetRequiredService<IUsuarioActual>();
            if (usuario.EsProveedor)
            {
                context.Result = new ContentResult
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    ContentType = "application/json",
                    Content = ApiRespuesta.Error(MensajeNoAutorizado)
                };
                return;
            }
            await next();
        }
    }
}
