using esupplier.Models.Response;

namespace esupplier.Models
{
    /// <summary>
    /// Resultado de la validación de credenciales: la respuesta para el frontend
    /// y los datos del usuario necesarios para emitir el JWT.
    /// </summary>
    public class ResultadoLogin
    {
        public Mensaje Mensaje { get; set; } = new Mensaje();
        public int RegId { get; set; }
        public int RolId { get; set; }
        public string Usuario { get; set; } = "";
        public string AdmClienteId { get; set; } = "";
        public string CodigoSociedad { get; set; } = "";
        public string CodigoReferencia { get; set; } = "";

        public bool Exitoso => Mensaje != null && "S".Equals(Mensaje.tipo);
    }
}
