using System.Text;
using esupplier.Models.Response;
using esupplier.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace esupplier.RestControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ComprobanteController : ControllerBase
    {
        private IComprobanteService _comprobanteService;

        public ComprobanteController(IComprobanteService comprobanteService)
        {
            _comprobanteService = comprobanteService;
        }

        [HttpPost("buscarComprobantes")]
        public ActionResult buscarComprobantes()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _comprobanteService.buscarComprobantes(jObject);

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
    }
}
