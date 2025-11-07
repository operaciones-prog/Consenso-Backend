namespace esupplier.Services.IServices
{
    public interface IHeaderService
    {
        IDictionary<string, string> obtenerCabeceras();

        string obtenerCabecera(string key);

        string obtenerIdioma();
    }
}
