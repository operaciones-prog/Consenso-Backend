using esupplier.Repositorys.IRepositorys;
using esupplier.Services.Auth;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace esupplier.Middleware.Autorizacion
{
    /// <summary>
    /// Identifica a quien llama y deja el resultado en <see cref="IdentidadSolicitud"/>.
    ///  - Con JWT: se usan sus claims.
    ///  - Sin JWT y con Auth:Enabled=false (transición): el navegador envía X-Usuario-Id, X-Usuario,
    ///    X-Adm-Cliente-Id y X-Codigo-Sociedad, y se comprueba en tb_usuario que el usuario exista y esté
    ///    activo; el rol se lee de la BD, nunca de la petición. Con Auth:Enabled=true este modo se apaga.
    /// Sin identidad responde 401; si no tiene el <see cref="Acceso"/> pedido, 403.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequiereSesionAttribute : Attribute, IAsyncActionFilter
    {
        public const string MensajeSinSesion = "Su sesión no es válida o expiró. Vuelva a iniciar sesión.";
        public const string MensajeSoloAdministrador = "Solo un administrador puede realizar esta operación.";
        public const string MensajeSoloPlataforma = "Solo el administrador de plataforma puede realizar esta operación.";

        public const string CabeceraRegId = "X-Usuario-Id";
        public const string CabeceraUsuario = "X-Usuario";
        public const string CabeceraCliente = "X-Adm-Cliente-Id";
        public const string CabeceraSociedad = "X-Codigo-Sociedad";

        private static readonly string[] RolesAdministrador = { "1", "2" };

        public Acceso Acceso { get; }

        public RequiereSesionAttribute(Acceso acceso = Acceso.Sesion)
        {
            Acceso = acceso;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var servicios = context.HttpContext.RequestServices;
            var identidad = servicios.GetRequiredService<IdentidadSolicitud>();
            var usuario = servicios.GetRequiredService<IUsuarioActual>();

            if (usuario.Autenticado)
            {
                identidad.Establecer(true, usuario.AdmClienteId, usuario.CodigoSociedad, usuario.Usuario, usuario.RolId,
                    usuario.EsInterno ? Constantes.NO_REFERIDO : usuario.CodigoProveedor);
            }
            else if (!servicios.GetRequiredService<IConfiguration>().GetValue<bool>("Auth:Enabled"))
            {
                ResolverPorCabeceras(context.HttpContext.Request, servicios.GetRequiredService<IIARepository>(), identidad);
            }

            if (!identidad.Resuelta)
            {
                context.Result = Respuesta(StatusCodes.Status401Unauthorized, MensajeSinSesion);
                return;
            }
            if (Acceso == Acceso.AdministradorEmpresa && (!identidad.EsInterno || !RolesAdministrador.Contains(identidad.RolId)))
            {
                context.Result = Respuesta(StatusCodes.Status403Forbidden, MensajeSoloAdministrador);
                return;
            }
            if (Acceso == Acceso.Plataforma && !identidad.EsPlataforma)
            {
                context.Result = Respuesta(StatusCodes.Status403Forbidden, MensajeSoloPlataforma);
                return;
            }
            await next();
        }

        private static void ResolverPorCabeceras(HttpRequest request, IIARepository repositorio, IdentidadSolicitud identidad)
        {
            string Cabecera(string nombre) => Uri.UnescapeDataString(request.Headers[nombre].ToString()).Trim();

            string cliente = Cabecera(CabeceraCliente);
            string sociedad = Cabecera(CabeceraSociedad);
            string nombreUsuario = Cabecera(CabeceraUsuario);
            if (cliente == "" || sociedad == "" || nombreUsuario == "" || !int.TryParse(Cabecera(CabeceraRegId), out int regId))
            {
                return;
            }

            var encontrado = repositorio.obtenerUsuarioIA(cliente, sociedad, regId, nombreUsuario);
            if (encontrado != null)
            {
                identidad.Establecer(false, cliente, sociedad, encontrado.usuario, encontrado.rol_id, encontrado.codigo_referencia);
            }
        }

        private static ContentResult Respuesta(int estado, string mensaje) => new ContentResult
        {
            StatusCode = estado,
            ContentType = "application/json",
            Content = ApiRespuesta.Error(mensaje)
        };
    }

    public enum Acceso
    {
        /// <summary>Cualquier usuario identificado.</summary>
        Sesion,
        /// <summary>Administrador interno de la empresa cliente (rol 1 o 2).</summary>
        AdministradorEmpresa,
        /// <summary>Administrador de plataforma (rol 6), el proveedor del sistema.</summary>
        Plataforma
    }
}
