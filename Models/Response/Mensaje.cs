namespace esupplier.Models.Response
{
    public class Mensaje
    {
        public string tipo { get; set; }
        public string mensaje { get; set; }
        public string data { get; set; }

        public Mensaje(string tipo, string mensaje, string data)
        {
            this.tipo = tipo;
            this.mensaje = mensaje;
            this.data = data;
        }

        public Mensaje()
        {
        }
    }
}
