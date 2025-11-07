using esupplier.Repositorys.IRepositorys;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Net;
using System.Net.Mail;

namespace esupplier.Repositorys
{
    public class EmailRepository : IEmailRepository
    {
        private IConfiguration _configuration;

        public EmailRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string SendEmail(string sociedad, string destinatarios, string asunto, string mensaje)
        {
            string rpta = "";
            string data = "";
            DataTable dt = new DataTable();

            string servidor = "";
            int puerto = 587;
            string usuario = "";
            string clave = "";
            string seudo = "";

            try
            {
                using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
                {
                    bdSql.Open();
                    SqlCommand bdComando = new SqlCommand("sp_obtenerConstantesCorreo", bdSql);
                    bdComando.CommandType = CommandType.StoredProcedure;
                    bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                    using (SqlDataAdapter adapter = new SqlDataAdapter((SqlCommand)bdComando))
                    {
                        adapter.Fill(dt);
                        data = JsonConvert.SerializeObject(dt, Formatting.Indented);

                        JArray arrayConstantes = JArray.Parse(data);

                        for (int i = 0; i < arrayConstantes.Count; i++)
                        {
                            if (arrayConstantes[i]["tipo_constante"].ToString().Equals("SMTP") &&
                                arrayConstantes[i]["codigo_constante"].ToString().Equals("HOST")) 
                            {
                                
                                servidor = arrayConstantes[i]["valor_constante"].ToString();

                            } else if (arrayConstantes[i]["tipo_constante"].ToString().Equals("SMTP") &&
                                arrayConstantes[i]["codigo_constante"].ToString().Equals("PORT"))
                            {
                                
                                puerto = Int32.Parse(arrayConstantes[i]["valor_constante"].ToString());

                            } else if (arrayConstantes[i]["tipo_constante"].ToString().Equals("SMTP") &&
                                arrayConstantes[i]["codigo_constante"].ToString().Equals("USER"))
                            {
                                
                                usuario = arrayConstantes[i]["valor_constante"].ToString();

                            } else if (arrayConstantes[i]["tipo_constante"].ToString().Equals("SMTP") &&
                                arrayConstantes[i]["codigo_constante"].ToString().Equals("PASS"))
                            {
                                
                                clave = arrayConstantes[i]["valor_constante"].ToString();

                            } else if (arrayConstantes[i]["tipo_constante"].ToString().Equals("SMTP") &&
                                arrayConstantes[i]["codigo_constante"].ToString().Equals("SEUD"))
                            {
                                
                                seudo = arrayConstantes[i]["valor_constante"].ToString();

                            } else if (arrayConstantes[i]["tipo_constante"].ToString().Equals("CORREO") &&
                                arrayConstantes[i]["codigo_constante"].ToString().Equals(sociedad) &&
                                destinatarios.Equals(Constantes.NO_INICIA))
                            {
                                
                                destinatarios = arrayConstantes[i]["valor_constante"].ToString();

                            }
                        }

                    }
                    bdSql.Close();
                }

                var mail = new MailMessage(seudo, destinatarios);
                mail.Subject = asunto;
                mail.Body = mensaje;
                mail.IsBodyHtml = true;

                using (var smtp = new SmtpClient(servidor, puerto))
                {
                    smtp.Credentials = new NetworkCredential(usuario, clave);
                    smtp.EnableSsl = true;
                    smtp.Send(mail);
                }

            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message);
            }

            return rpta;

        }
    }
}
