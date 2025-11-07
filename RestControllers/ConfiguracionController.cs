using System.Text;
using esupplier.Models.Response;
using esupplier.Services;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace esupplier.ResControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfiguracionController : ControllerBase
    {
        private IConfiguracionService _configuracionService;

        public ConfiguracionController(IConfiguracionService configuracionService)
        {
            _configuracionService = configuracionService;
        }

        [HttpPost("listarConstantes")]
        public ActionResult listarConstantes()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _configuracionService.listarConstantes(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\",\"data\":{" + msj.data + "}}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("mantenerConstante")]
        public ActionResult mantenerConstante()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _configuracionService.mantenerConstante(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\"}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("listarSociedad")]
        public ActionResult listarSociedad()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _configuracionService.listarSociedad(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\",\"data\":{" + msj.data + "}}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("mantenerSociedad")]
        public ActionResult mantenerSociedad()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _configuracionService.mantenerSociedad(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\"}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }



        [HttpGet("listarCalificacionPro")]
        public ActionResult listarCalificacionPro()
        {
            string rpta = "";
            try
            {
                JObject jObject = new JObject();
                jObject["adm_cliente_id"] = Constantes.ADM_CLIENTE;
                jObject["tipo_constante"] = "PRO_CALI";
                jObject["codigo_constante"] = "";
                Mensaje msj = _configuracionService.listarConstantes(jObject);

                JObject jObjectList = JObject.Parse("{" + msj.data + "}");
                JArray jsonArray = (JArray)jObjectList["constantes"];


                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\",\"data\":" + jsonArray + "}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }


        [HttpGet("listarCondicionPago")]
        public ActionResult listarCondicionPago()
        {
            string rpta = "";
            try
            {
                Mensaje msj = _configuracionService.listarCondicionPago();

                JObject jObjectList = JObject.Parse("{" + msj.data + "}");
                JArray jsonArray = (JArray)jObjectList["condiciones"];


                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\",\"data\":" + jsonArray + "}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("registrarCondicionPago")]
        public ActionResult registrarCondicionPago()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _configuracionService.registrarCondicionPago(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\"}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }


        [HttpGet("eliminarCondicionPago/{id}")]
        // [Authorize]
        public ActionResult listarArchivo( [FromRoute] int id)
        {
            string rpta = "";
            try
            {
                //var claims = HttpContext.User.Claims;
                Mensaje msj = new Mensaje();
                msj = _configuracionService.eliminarCondicionPago(id);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\"}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

    }
}
