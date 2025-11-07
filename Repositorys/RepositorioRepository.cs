using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Data;

namespace esupplier.Repositorys
{
    public class RepositorioRepository : IRepositorioRepository
    {
        private IConfiguration _configuration;

        public RepositorioRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Mensaje obtenerRutaRepositorio(string  usuario)
        {
            Mensaje mensaje = new Mensaje();

            DataTable dt = new DataTable();

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_obtenerRutaRepositorio", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@usuario", usuario));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);

                        mensaje.tipo = "S";
                        mensaje.mensaje = "";
                        mensaje.data =  JsonConvert.SerializeObject(dt, Formatting.Indented);
                    }
                    bdSql.Close();
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron constantes";
                mensaje.data = "";
            }

            return mensaje;
        }
    }
}
