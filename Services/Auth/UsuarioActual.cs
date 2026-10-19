using System.Security.Claims;
using esupplier.Utils;

namespace esupplier.Services.Auth
{
    /// <summary>
    /// Datos del usuario que hace la petición, tomados del JWT (ver <see cref="GeneradorTokenJwt"/>).
    /// Mientras Auth:Enabled=false no llega token y <see cref="Autenticado"/> es false:
    /// en ese caso los filtros de autorización no hacen nada (comportamiento histórico).
    /// </summary>
    public interface IUsuarioActual
    {
        bool Autenticado { get; }

        /// <summary>Usuario de un proveedor externo (codigo_referencia distinto de "0").</summary>
        bool EsProveedor { get; }

        /// <summary>Usuario interno de la empresa (codigo_referencia = "0").</summary>
        bool EsInterno { get; }

        /// <summary>Código SAP del proveedor (8 dígitos) cuando <see cref="EsProveedor"/>; vacío si no.</summary>
        string CodigoProveedor { get; }

        string Usuario { get; }
        string RegId { get; }
        string RolId { get; }
        string AdmClienteId { get; }
        string CodigoSociedad { get; }
    }

    public class UsuarioActual : IUsuarioActual
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UsuarioActual(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal Principal => _httpContextAccessor.HttpContext?.User;

        private string Claim(string tipo) => Principal?.FindFirst(tipo)?.Value?.Trim() ?? "";

        public bool Autenticado => Principal?.Identity?.IsAuthenticated == true;

        public bool EsProveedor => Autenticado && Referencia != "" && Referencia != Constantes.NO_REFERIDO;

        public bool EsInterno => Autenticado && Referencia == Constantes.NO_REFERIDO;

        public string CodigoProveedor => EsProveedor ? Referencia : "";

        public string Usuario => Claim("usuario");
        public string RegId => Claim("reg_id");
        public string RolId => Claim("rol_id");
        public string AdmClienteId => Claim("adm_cliente_id");
        public string CodigoSociedad => Claim("codigo_sociedad");

        private string Referencia => Claim("codigo_referencia");
    }
}
