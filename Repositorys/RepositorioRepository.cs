using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;

namespace esupplier.Repositorys
{
    public class RepositorioRepository : IRepositorioRepository
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RepositorioRepository> _logger;

        public RepositorioRepository(IConfiguration configuration, ILogger<RepositorioRepository> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Mensaje obtenerRutaRepositorio(string usuario)
        {
            Mensaje mensaje = new Mensaje();
            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    using SqlCommand bdComando = new SqlCommand("sp_obtenerRutaRepositorio", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@usuario", (object)usuario ?? DBNull.Value));
                    using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data = JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sp_obtenerRutaRepositorio (usuario {Usuario})", usuario);
                return new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
            }

            return mensaje;
        }
    }
}
