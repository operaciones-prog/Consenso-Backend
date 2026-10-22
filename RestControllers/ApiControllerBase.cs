using esupplier.Models.Response;
using esupplier.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace esupplier.ResControllers
{
    /// <summary>
    /// Base de los controladores nuevos: lee el body, ejecuta la acción y responde con el formato
    /// estándar {"message","type","data"} (siempre HTTP 200), registrando los errores inesperados.
    /// </summary>
    public abstract class ApiControllerBase : ControllerBase
    {
        protected abstract ILogger Logger { get; }

        protected async Task<ActionResult> Ejecutar(string contexto, Func<JObject, Task<Mensaje>> accion, bool conObjeto)
        {
            try
            {
                JObject jObject = await JsonUtil.LeerBodyAsync(Request);
                Mensaje msj = await accion(jObject);
                if (msj == null) return Ok(ApiRespuesta.Error(ApiRespuesta.MensajeErrorGenerico));
                if (msj.tipo != "S")
                {
                    return Ok(string.IsNullOrEmpty(msj.data)
                        ? ApiRespuesta.Error(msj.mensaje)
                        : ApiRespuesta.ErrorConObjeto(msj.mensaje, msj.data));
                }
                return Ok(conObjeto ? ApiRespuesta.ExitoConObjeto(msj.mensaje, msj.data) : ApiRespuesta.Exito(msj.mensaje));
            }
            catch (Exception e) when (e is CampoRequeridoException || e is ArgumentException || e is FormatException)
            {
                return Ok(ApiRespuesta.Error(e.Message));
            }
            catch (JsonReaderException e)
            {
                Logger.LogWarning(e, "Body inválido en {Contexto}", contexto);
                return Ok(ApiRespuesta.Error("El cuerpo de la petición no es un JSON válido."));
            }
            catch (Exception e)
            {
                return Ok(ApiRespuesta.ErrorInesperado(Logger, e, contexto));
            }
        }

        protected Task<ActionResult> Ejecutar(string contexto, Func<JObject, Mensaje> accion, bool conObjeto) =>
            Ejecutar(contexto, json => Task.FromResult(accion(json)), conObjeto);
    }
}
