using System.Text;
using esupplier.Services.Auth;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.Middleware.Autorizacion
{
    /// <summary>
    /// Cuando quien llama es un proveedor, reemplaza el código de proveedor que envía el navegador
    /// por el de su token, para que no pueda consultar datos de otro proveedor cambiando la petición.
    ///
    /// Cada campo se indica como "nombre" o "nombre:largo". Con largo se completa con ceros a la
    /// izquierda (SAP espera LIFNR de 10 dígitos: "10004065" → "0010004065").
    /// Se aplica a:
    ///  - campos del cuerpo JSON (los controladores lo leen con JsonUtil.LeerBodyAsync): se
    ///    escriben siempre, aunque el navegador no los envíe, para que la consulta quede acotada;
    ///  - rutas JSON que empiezan con "$" (p. ej. "$.Cabecera[*].Proveedor"): solo se reemplazan
    ///    los valores que existen; y
    ///  - argumentos de la acción con el mismo nombre (p. ej. [FromQuery] codigo).
    ///
    /// Usuarios internos y peticiones sin token (Auth:Enabled=false) pasan sin cambios.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ProveedorDelTokenAttribute : Attribute, IAsyncActionFilter
    {
        private readonly (string nombre, int largo)[] _campos;

        public ProveedorDelTokenAttribute(params string[] campos)
        {
            _campos = campos.Select(c =>
            {
                string[] partes = c.Split(':');
                return (partes[0], partes.Length > 1 && int.TryParse(partes[1], out int n) ? n : 0);
            }).ToArray();
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var usuario = context.HttpContext.RequestServices.GetRequiredService<IUsuarioActual>();
            if (usuario.EsProveedor)
            {
                string codigo = usuario.CodigoProveedor;

                // 1) Argumentos ya enlazados (query string / ruta)
                foreach (var (nombre, largo) in _campos.Where(c => !c.nombre.StartsWith("$")))
                {
                    if (context.ActionArguments.ContainsKey(nombre))
                    {
                        context.ActionArguments[nombre] = Formatear(codigo, largo);
                    }
                }

                // 2) Cuerpo JSON: se reescribe antes de que la acción lo lea
                var request = context.HttpContext.Request;
                if (request.ContentLength != 0 && (request.ContentType ?? "").Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    string cuerpo;
                    using (var lector = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
                    {
                        cuerpo = await lector.ReadToEndAsync();
                    }

                    JObject json;
                    try
                    {
                        json = string.IsNullOrWhiteSpace(cuerpo) ? new JObject() : JObject.Parse(cuerpo);
                    }
                    catch (JsonReaderException)
                    {
                        // JSON inválido: se deja tal cual para que la acción responda su error habitual
                        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(cuerpo));
                        await next();
                        return;
                    }

                    foreach (var (nombre, largo) in _campos)
                    {
                        if (nombre.StartsWith("$"))
                        {
                            foreach (JToken token in json.SelectTokens(nombre).ToList())
                            {
                                token.Replace(Formatear(codigo, largo));
                            }
                        }
                        else
                        {
                            json[nombre] = Formatear(codigo, largo);
                        }
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
                    request.Body = new MemoryStream(bytes);
                    request.ContentLength = bytes.Length;
                }
            }

            await next();
        }

        private static string Formatear(string codigo, int largo)
        {
            return largo > 0 ? codigo.PadLeft(largo, '0') : codigo;
        }
    }
}
