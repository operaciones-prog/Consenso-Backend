namespace esupplier.Repositorys.IRepositorys
{
    public interface IEmailRepository
    {
        /// <summary>Envía un correo cargando la configuración SMTP en cada llamada (uso puntual).</summary>
        public string SendEmail(string sociedad, string destinatarios, string asunto, string mensaje);

        /// <summary>Carga una vez la configuración SMTP y los correos por sociedad, para envíos en lote.</summary>
        public ConfiguracionCorreo ObtenerConfiguracion();

        /// <summary>Envía un correo con una configuración ya cargada. Lanza excepción si falla el envío.</summary>
        public void SendEmail(ConfiguracionCorreo configuracion, string sociedad, string destinatarios, string asunto, string mensaje);
    }

    /// <summary>Configuración SMTP leída de sp_obtenerConstantesCorreo.</summary>
    public class ConfiguracionCorreo
    {
        public string Servidor { get; set; } = "";
        public int Puerto { get; set; } = 587;
        public string Usuario { get; set; } = "";
        public string Clave { get; set; } = "";
        public string Remitente { get; set; } = "";

        /// <summary>Correo por defecto de cada sociedad (constante CORREO/{sociedad}).</summary>
        public Dictionary<string, string> CorreosSociedad { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
