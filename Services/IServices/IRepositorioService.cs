using esupplier.Models.Response;

namespace esupplier.Services.IServices
{
    public interface IRepositorioService
    {
        public Mensaje obtenerRutaRepositorio(string usuario);
    }
}
