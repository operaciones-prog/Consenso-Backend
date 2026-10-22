using esupplier.Models.IA;
using System.Data;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IIARepository
    {
        DataTable listarAddon(string admClienteId, string codigoSociedad, string idioma);
        void mantenerAddonActivacion(string admClienteId, string codigoSociedad, string codigoAddon, bool activo, string usuario);

        /// <summary>Conexión vigente de la sociedad, o null si no hay.</summary>
        CredencialIA obtenerCredencial(string admClienteId, string codigoSociedad);
        void mantenerCredencial(string admClienteId, string codigoSociedad, CredencialIA credencial, string usuario);
        void actualizarEstadoCredencial(string admClienteId, string codigoSociedad, string estado);

        /// <summary>Da de baja la conexión y devuelve la referencia del secreto (null si no había).</summary>
        string eliminarCredencial(string admClienteId, string codigoSociedad, string usuario);
    }
}
