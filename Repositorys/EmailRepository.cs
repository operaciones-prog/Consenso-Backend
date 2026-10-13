using esupplier.Repositorys.IRepositorys;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Mail;

namespace esupplier.Repositorys
{
    public class EmailRepository : IEmailRepository
    {
        // Menor al límite de API Gateway (30 s) para no dejar la petición colgada.
        private const int TimeoutSmtpMs = 20000;

        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailRepository> _logger;

        public EmailRepository(IConfiguration configuration, ILogger<EmailRepository> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string SendEmail(string sociedad, string destinatarios, string asunto, string mensaje)
        {
            ConfiguracionCorreo configuracion = ObtenerConfiguracion();
            SendEmail(configuracion, sociedad, destinatarios, asunto, mensaje);
            return "";
        }

        public ConfiguracionCorreo ObtenerConfiguracion()
        {
            var configuracion = new ConfiguracionCorreo();
            DataTable dt = new DataTable();

            using (SqlConnection bdSql = new SqlConnection(_configuration.GetConnectionString("esupplier")))
            {
                bdSql.Open();
                using SqlCommand bdComando = new SqlCommand("sp_obtenerConstantesCorreo", bdSql);
                bdComando.CommandType = CommandType.StoredProcedure;
                bdComando.Parameters.Add(new SqlParameter("@adm_cliente_id", Constantes.ADM_CLIENTE));
                using (SqlDataAdapter adapter = new SqlDataAdapter(bdComando))
                {
                    adapter.Fill(dt);
                }
            }

            foreach (DataRow fila in dt.Rows)
            {
                string tipo = Convert.ToString(fila["tipo_constante"], CultureInfo.InvariantCulture) ?? "";
                string codigo = Convert.ToString(fila["codigo_constante"], CultureInfo.InvariantCulture) ?? "";
                string valor = Convert.ToString(fila["valor_constante"], CultureInfo.InvariantCulture) ?? "";

                if (tipo == "SMTP")
                {
                    switch (codigo)
                    {
                        case "HOST": configuracion.Servidor = valor; break;
                        case "PORT": configuracion.Puerto = int.Parse(valor, CultureInfo.InvariantCulture); break;
                        case "USER": configuracion.Usuario = valor; break;
                        case "PASS": configuracion.Clave = valor; break;
                        case "SEUD": configuracion.Remitente = valor; break;
                    }
                }
                else if (tipo == "CORREO")
                {
                    configuracion.CorreosSociedad.TryAdd(codigo, valor);
                }
            }

            return configuracion;
        }

        public void SendEmail(ConfiguracionCorreo configuracion, string sociedad, string destinatarios, string asunto, string mensaje)
        {
            // Sin destinatario explícito se usa el correo configurado para la sociedad
            if (string.IsNullOrEmpty(destinatarios) && sociedad != null &&
                configuracion.CorreosSociedad.TryGetValue(sociedad, out string correoSociedad))
            {
                destinatarios = correoSociedad;
            }

            if (string.IsNullOrWhiteSpace(destinatarios))
            {
                throw new InvalidOperationException("No hay destinatarios configurados para la sociedad " + sociedad + ".");
            }

            try
            {
                using var mail = new MailMessage(configuracion.Remitente, destinatarios);
                mail.Subject = asunto;
                mail.Body = mensaje;
                mail.IsBodyHtml = true;

                using var smtp = new SmtpClient(configuracion.Servidor, configuracion.Puerto);
                smtp.Credentials = new NetworkCredential(configuracion.Usuario, configuracion.Clave);
                smtp.EnableSsl = true;
                smtp.Timeout = TimeoutSmtpMs;
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo '{Asunto}' a {Destinatarios}", asunto, destinatarios);
                throw;
            }
        }
    }
}
