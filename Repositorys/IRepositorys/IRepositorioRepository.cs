using esupplier.Models.Response;

namespace esupplier.Repositorys.IRepositorys
{
    public interface IRepositorioRepository
    {
        public Mensaje obtenerRutaRepositorio(string usuario);
    }
}
