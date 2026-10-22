namespace esupplier.Services.Auth
{
    /// <summary>
    /// Quién hace la petición, ya validado por <see cref="Middleware.Autorizacion.RequiereSesionAttribute"/>.
    /// Sale del JWT o, mientras Auth:Enabled=false, de las cabeceras X-Usuario-* comprobadas contra tb_usuario.
    /// Los servicios lo usan en lugar de leer cliente, sociedad o usuario del body.
    /// </summary>
    public class IdentidadSolicitud
    {
        public bool Resuelta { get; private set; }
        /// <summary>true si vino del JWT; false si vino de las cabeceras (modo sin JWT).</summary>
        public bool PorToken { get; private set; }
        public string Cliente { get; private set; } = "";
        public string Sociedad { get; private set; } = "";
        public string Usuario { get; private set; } = "";
        public string RolId { get; private set; } = "";
        public bool EsInterno { get; private set; }
        /// <summary>Administrador de plataforma (rol 6): el proveedor del sistema.</summary>
        public bool EsPlataforma => RolId == Utils.Constantes.ROL_PLATAFORMA;

        public void Establecer(bool porToken, string cliente, string sociedad, string usuario, string rolId, string codigoReferencia)
        {
            Resuelta = true;
            PorToken = porToken;
            Cliente = cliente ?? "";
            Sociedad = sociedad ?? "";
            Usuario = usuario ?? "";
            RolId = (rolId ?? "").Trim();
            EsInterno = (codigoReferencia ?? "").Trim() == Utils.Constantes.NO_REFERIDO;
        }
    }
}
