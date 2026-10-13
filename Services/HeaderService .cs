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
            // Fuera de un request (p. ej. el job de Lambda) no hay HttpContext.
            return _httpContextAccessor.HttpContext?.Request.Headers[key].ToString() ?? "";
        }

        public IDictionary<string, string> obtenerCabeceras()
        {
            var headers = new Dictionary<string, string>();
            var contexto = _httpContextAccessor.HttpContext;
            if (contexto == null) return headers;
            foreach (var header in contexto.Request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }
            return headers;
        }


        public string obtenerIdioma()
        {
            // Valor del header "Idioma" (por defecto "ES")
            var contexto = _httpContextAccessor.HttpContext;
            if (contexto != null && contexto.Request.Headers.TryGetValue("Idioma", out var idioma) && !string.IsNullOrWhiteSpace(idioma))
            {
                return idioma.ToString().Trim().ToUpperInvariant();
            }

            return "ES";
        }
    }
}
