using esupplier.Services.IServices;

namespace esupplier.Services
{
    public class HeaderService : IHeaderService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HeaderService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string obtenerCabecera(string key)
        {
            return _httpContextAccessor.HttpContext.Request.Headers[key].ToString();
        }

        public IDictionary<string, string> obtenerCabeceras()
        {
            var headers = new Dictionary<string, string>();
            foreach (var header in _httpContextAccessor.HttpContext.Request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }
            return headers;
        }


        public string obtenerIdioma()
        {
            // Obtiene el valor del header "Idioma"
            if (_httpContextAccessor.HttpContext != null &&
             _httpContextAccessor.HttpContext.Request.Headers.ContainsKey("Idioma"))
            {
                // Retorna el valor del header "Idioma"
                return _httpContextAccessor.HttpContext.Request.Headers["Idioma"].ToString().ToUpper();
            }

            return "ES";
        }
    }
}
