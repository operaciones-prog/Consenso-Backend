using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using Newtonsoft.Json.Linq;
using System.Net;

namespace esupplier.Repositorys
{
    public class HESRepository : IHESRepository
    {
        private IConfiguration _configuration;
        private readonly IHeaderService _headerService;

        public HESRepository(IConfiguration configuration,IHeaderService headerService)
        {
            _configuration = configuration;
            _headerService = headerService;
        }

        public Mensaje buscarHES(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            var url = _configuration["Http:Consultahesmigo"];
            var request = (HttpWebRequest)WebRequest.Create(url);
            string json = jObject.ToString();
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers.Add("Authorization", _configuration["Http:Autorizacion"]);
            string idioma = _headerService.obtenerIdioma();
            string mercaderia = "\"TIPO_SERVICIO\": \"Mercaderia\"";
            string mercaderiaEN = "\"TIPO_SERVICIO\": \"Merchandise\"";
            string servicio = "\"TIPO_SERVICIO\": \"Servicio\"";
            string servicioEN = "\"TIPO_SERVICIO\": \"Service\"";
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream strReader = response.GetResponseStream())
                    {
                        if (strReader != null)
                        {
                            using (StreamReader objReader = new StreamReader(strReader))
                            {
                                string responseBody = objReader.ReadToEnd();
                                JObject objJsonAux = JObject.Parse(responseBody);

                                mensaje.tipo = "S";
                                mensaje.mensaje = "";

                                try
                                {
                                    mensaje.data = "\"E_TOTALROWS\":" + objJsonAux["E_TOTALROWS"].ToString();

                                    JArray arrayMHCAB = new JArray();
                                    arrayMHCAB = (JArray)objJsonAux["ET_MHCAB"]["item"];

                                    mensaje.data = mensaje.data + ",\"ET_MHCAB\":" + arrayMHCAB.ToString();
                                     if (idioma != "ES")
                                    {
                                        mensaje.data = mensaje.data.Replace(mercaderia, mercaderiaEN).Replace(servicio, servicioEN);

                                    }

                                    if (jObject["I_INDICADOR"].ToString().Equals("2"))
                                    {
                                        JArray arrayMHDET = new JArray();
                                        arrayMHDET = (JArray)objJsonAux["ET_MHDET"]["item"];

                                        mensaje.data = mensaje.data + ",\"ET_MHDET\":" + arrayMHDET.ToString();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    mensaje.tipo = "E";
                                    mensaje.mensaje = "No se encontraron registros";
                                    mensaje.data = "";
                                }

                            }
                        }
                        else
                        {
                            mensaje.tipo = "E";
                            mensaje.mensaje = "Sin respuesta - null";
                            mensaje.data = "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje.tipo = "E";
                mensaje.mensaje = "No se encontraron registros";
                mensaje.data = "";
            }

            return mensaje;
        }
    }
}
