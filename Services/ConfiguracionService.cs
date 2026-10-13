using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using esupplier.Utils;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    public class ConfiguracionService : IConfiguracionService
    {
        private IConfiguracionRepository _configuracionRepository;

        public ConfiguracionService(IConfiguracionRepository configuracionRepository)
        {
            _configuracionRepository = configuracionRepository;
        }

        public Mensaje listarConstantes(JObject jObject)
        {
            return _configuracionRepository.listarConstantes(jObject);
        }

        public Mensaje listarConstantesPublico(JObject jObject)
        {
            Mensaje msj = _configuracionRepository.listarConstantes(jObject);
            if (msj == null || msj.tipo != "S" || string.IsNullOrEmpty(msj.data)) return msj;

            JObject contenedor = JObject.Parse("{" + msj.data + "}");
            if (contenedor["constantes"] is JArray constantes)
            {
                foreach (JObject item in constantes)
                {
                    if (ConstantesSensibles.EsSensible((string)item["tipo_constante"], (string)item["codigo_constante"]))
                    {
                        item["valor_constante"] = ConstantesSensibles.Mascara;
                    }
                }
            }
            string json = contenedor.ToString(Newtonsoft.Json.Formatting.None);
            msj.data = json.Substring(1, json.Length - 2); // mismo formato de fragmento que devuelve el repositorio
            return msj;
        }

        public Mensaje mantenerConstante(JObject jObject)
        {
            // El frontend recibe los secretos enmascarados; si los devuelve sin cambios se conserva el valor real.
            if (jObject != null && JsonUtil.Opcional(jObject, "valor_constante") == ConstantesSensibles.Mascara)
            {
                string tipo = JsonUtil.Opcional(jObject, "tipo_constante");
                string codigo = JsonUtil.Opcional(jObject, "codigo_constante");
                var filtro = new JObject
                {
                    ["adm_cliente_id"] = JsonUtil.Opcional(jObject, "adm_cliente_id", Constantes.ADM_CLIENTE),
                    ["tipo_constante"] = tipo,
                    ["codigo_constante"] = codigo
                };

                Mensaje actual = _configuracionRepository.listarConstantes(filtro);
                if (actual == null || actual.tipo != "S" || string.IsNullOrEmpty(actual.data))
                {
                    return actual ?? new Mensaje("E", ApiRespuesta.MensajeErrorGenerico, "");
                }

                JToken encontrado = null;
                if (JObject.Parse("{" + actual.data + "}")["constantes"] is JArray constantes)
                {
                    encontrado = constantes.FirstOrDefault(c =>
                        string.Equals((string)c["tipo_constante"], tipo, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((string)c["codigo_constante"], codigo, StringComparison.OrdinalIgnoreCase));
                }

                if (encontrado == null)
                {
                    return new Mensaje("E", "Debe ingresar el valor de la constante.", "");
                }
                jObject["valor_constante"] = (string)encontrado["valor_constante"] ?? "";
            }

            return _configuracionRepository.mantenerConstante(jObject);
        }

        public Mensaje listarSociedad(JObject jObject)
        {
            return _configuracionRepository.listarSociedad(jObject);
        }

        public Mensaje mantenerSociedad(JObject jObject)
        {
            return _configuracionRepository.mantenerSociedad(jObject);
        }

        public Mensaje registrarCondicionPago(JObject jObject)
        {
            return _configuracionRepository.registrarCondicionPago(jObject);
        }

        public Mensaje listarCondicionPago()
        {
            return _configuracionRepository.listarCondicionPago();
        }

        public Mensaje eliminarCondicionPago(int id)
        {
            return _configuracionRepository.eliminarCondicionPago(id);
        }

        public Mensaje buscarCondicionPagoPorCodigo(string codigo)
        {
            return _configuracionRepository.buscarCondicionPagoPorCodigo(codigo);
        }
    }
}
