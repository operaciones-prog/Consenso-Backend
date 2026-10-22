using esupplier.Services.Auth;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace esupplier.Middleware.Autorizacion
{
    /// <summary>
    /// Exige un JWT válido aunque Auth:Enabled=false (funciones nuevas que no deben quedar abiertas,
    /// como la gestión de la API key de IA). Sin token responde 401; con soloAdministrador, un usuario
    /// que no sea administrador interno (rol 1 o 2) recibe 403.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequiereSesionAttribute : Attribute, IAsyncActionFilter
    {
        public const string MensajeSinSesion = "Su sesión no es válida o expiró. Vuelva a iniciar sesión.";
        public const string MensajeSoloAdministrador = "Solo un administrador puede realizar esta operación.";

        private static readonly string[] RolesAdministrador = { "1", "2" };

        public bool SoloAdministrador { get; }

        public RequiereSesionAttribute(bool soloAdministrador = false)
        {
            SoloAdministrador = soloAdministrador;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var usuario = context.HttpContext.RequestServices.GetRequiredService<IUsuarioActual>();
            if (!usuario.Autenticado)
            {
                context.Result = Respuesta(StatusCodes.Status401Unauthorized, MensajeSinSesion);
                return;
            }
            if (SoloAdministrador && (!usuario.EsInterno || !RolesAdministrador.Contains(usuario.RolId)))
            {
                context.Result = Respuesta(StatusCodes.Status403Forbidden, MensajeSoloAdministrador);
                return;
            }
            await next();
        }

        private static ContentResult Respuesta(int estado, string mensaje) => new ContentResult
        {
            StatusCode = estado,
            ContentType = "application/json",
            Content = ApiRespuesta.Error(mensaje)
        };
    }
}
