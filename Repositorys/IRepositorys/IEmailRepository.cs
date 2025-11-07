namespace esupplier.Repositorys.IRepositorys
{
    public interface IEmailRepository
    {
        public string SendEmail(string sociedad, string destinatarios, string asunto, string mensaje);
    }
}
