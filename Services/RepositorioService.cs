using esupplier.Models.Response;
using esupplier.Repositorys;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;

namespace esupplier.Services
{
    public class RepositorioService : IRepositorioService
    {
        private IRepositorioRepository _repositorioRepository;

        public RepositorioService(IRepositorioRepository repositorioRepository)
        {
            _repositorioRepository = repositorioRepository;
        }
        public Mensaje obtenerRutaRepositorio(string usuario)
        {
            return _repositorioRepository.obtenerRutaRepositorio(usuario);
        }
    }
}
