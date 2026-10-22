using esupplier.Models.IA;
using System.Data;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IIARepository
    {
        /// <summary>Usuario activo con ese reg_id y nombre, o null (modo sin JWT).</summary>
        UsuarioIA obtenerUsuarioIA(string admClienteId, string codigoSociedad, int regId, string usuario);

        DataTable listarAddon(string admClienteId, string codigoSociedad, string idioma);
        void mantenerAddonActivacion(string admClienteId, string codigoSociedad, string codigoAddon, bool activo, string usuario);

        /// <summary>Conexión vigente de la sociedad, o null si no hay.</summary>
        CredencialIA obtenerCredencial(string admClienteId, string codigoSociedad);
        void mantenerCredencial(string admClienteId, string codigoSociedad, CredencialIA credencial, string usuario);
        void actualizarEstadoCredencial(string admClienteId, string codigoSociedad, string estado);

        SaldoIA obtenerSaldo(string admClienteId, string codigoSociedad);
        int registrarRecarga(RecargaIA recarga, string usuario);
        /// <summary>codigoSociedad null = todas las sociedades (plataforma).</summary>
        DataTable listarRecargas(string admClienteId, string codigoSociedad, string estado);
        RecargaIA obtenerRecarga(int regId);
        void aprobarRecarga(int regId, decimal montoAprobado, string usuario);
        void rechazarRecarga(int regId, string motivo, string usuario);

        /// <summary>Claves almacenadas (hash o texto heredado) del usuario activo, para pedir la contraseña de nuevo.</summary>
        List<string> obtenerClavesUsuario(string admClienteId, string codigoSociedad, string usuario);

        /// <summary>Da de baja la conexión y devuelve la referencia del secreto (null si no había).</summary>
        string eliminarCredencial(string admClienteId, string codigoSociedad, string usuario);
    }
}
