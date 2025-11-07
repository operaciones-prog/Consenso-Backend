using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using Newtonsoft.Json.Linq;
using System.Net;

namespace esupplier.Repositorys
{
    public class ComprobanteRepository : IComprobanteRepository
    {
        private IConfiguration _configuration;

        public ComprobanteRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Mensaje buscarComprobantes(JObject jObject)
        {
            Mensaje mensaje = new Mensaje();

            var url = _configuration["Http:Consultarcomprobantes"];
            var request = (HttpWebRequest)WebRequest.Create(url);
            string json = jObject.ToString();
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers.Add("Authorization", _configuration["Http:Autorizacion"]);

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
                                    JArray arrayDOCPAGO = new JArray();
                                    arrayDOCPAGO = (JArray)objJsonAux["ET_DOCPAGO"]["item"];

                                    mensaje.data = "\"ET_DOCPAGO\":" + arrayDOCPAGO.ToString();

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
                mensaje.mensaje = "No se encuntraron comprobantes";
                mensaje.data = "";
            }

            return mensaje;
        }
    }
}
