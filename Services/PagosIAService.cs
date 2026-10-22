using esupplier.Models.IA;
using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.Auth;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace esupplier.Services
{
    public class PagosIAService : IPagosIAService
    {
        private const int ErrorComprobanteDuplicado = 50811;
        private const int ErrorRecargaYaValidada = 50812;
        private const int ErrorMontoInvalido = 50813;

        private const decimal MontoMaximo = 100000m;
        /// <summary>Lambda acepta hasta 6 MB por petición y el archivo viaja en base64 (+33 %).</summary>
        private const int TamanoMaximoBytes = 4 * 1024 * 1024;
        private static readonly string[] ExtensionesPermitidas = { "pdf", "jpg", "jpeg", "png", "xml" };

        private readonly IIARepository _repositorio;
        private readonly IdentidadSolicitud _usuario;
        private readonly IFileStorageService _archivos;
        private readonly ILogger<PagosIAService> _logger;

        public PagosIAService(IIARepository repositorio, IdentidadSolicitud usuario, IFileStorageService archivos, ILogger<PagosIAService> logger)
        {
            _repositorio = repositorio;
            _usuario = usuario;
            _archivos = archivos;
            _logger = logger;
        }

        public async Task<Mensaje> registrarRecargaAsync(JObject jObject)
        {
            if (!decimal.TryParse(MontoTexto(jObject["monto_usd"]), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal monto)
                || monto <= 0 || monto > MontoMaximo)
            {
                return new Mensaje("E", "Ingrese un monto válido.", "");
            }

            string banco = JsonUtil.Requerido(jObject, "banco").Trim();
            string referencia = JsonUtil.Requerido(jObject, "referencia").Trim();
            if (banco == "" || banco.Length > 60) return new Mensaje("E", "Ingrese el banco o medio de pago.", "");
            if (referencia == "" || referencia.Length > 60) return new Mensaje("E", "Ingrese el número de comprobante.", "");

            if (!DateTime.TryParseExact(JsonUtil.Requerido(jObject, "fecha_pago"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime fechaPago)
                || fechaPago.Date > FechaUtil.AhoraEcuador().Date || fechaPago.Date < FechaUtil.AhoraEcuador().Date.AddYears(-1))
            {
                return new Mensaje("E", "La fecha de pago no es válida.", "");
            }

            if (jObject["file"] is not JObject file) throw new CampoRequeridoException("file");
            string nombre = ArchivoUtil.NombreSeguro(JsonUtil.Requerido(file, "fileName"));
            string extension = ConsultaUtil.GetFileExtension(nombre).ToLowerInvariant();
            if (!ExtensionesPermitidas.Contains(extension))
            {
                return new Mensaje("E", "El comprobante debe ser PDF, imagen (JPG o PNG) o XML.", "");
            }
            byte[] contenido = ArchivoUtil.DecodificarBase64(JsonUtil.Requerido(file, "fileContent"));
            if (contenido.Length == 0 || contenido.Length > TamanoMaximoBytes)
            {
                return new Mensaje("E", "El comprobante debe pesar como máximo 4 MB.", "");
            }

            string carpeta = $"ia-comprobantes/{ArchivoUtil.SegmentoSeguro(_usuario.Cliente)}/{ArchivoUtil.SegmentoSeguro(_usuario.Sociedad)}/";
            string nombreGuardado = FechaUtil.AhoraEcuador().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "_" + nombre;
            var subida = await _archivos.UploadFileAsync(contenido, nombreGuardado, carpeta);
            if (!subida.Success)
            {
                _logger.LogError("No se pudo guardar el comprobante de recarga: {Error}", subida.ErrorMessage);
                return new Mensaje("E", "No se pudo guardar el comprobante. Intente nuevamente.", "");
            }

            var recarga = new RecargaIA
            {
                adm_cliente_id = _usuario.Cliente,
                codigo_sociedad = _usuario.Sociedad,
                monto_usd = Math.Round(monto, 2),
                banco = banco,
                referencia = referencia,
                fecha_pago = fechaPago,
                archivo_ruta = carpeta,
                archivo_nombre = nombreGuardado
            };
            try
            {
                _repositorio.registrarRecarga(recarga, _usuario.Usuario);
            }
            catch (SqlException ex) when (ex.Number == ErrorComprobanteDuplicado)
            {
                await _archivos.DeleteFileAsync(carpeta, nombreGuardado);
                return new Mensaje("E", "Ese comprobante ya fue registrado.", "");
            }

            return new Mensaje("S", "Comprobante enviado. El saldo se acreditará cuando se valide el pago.", FragmentoEmpresa());
        }

        public Mensaje listarRecargasEmpresa()
        {
            return new Mensaje("S", "", FragmentoEmpresa());
        }

        public Mensaje listarRecargasPlataforma(JObject jObject)
        {
            string estado = JsonUtil.Opcional(jObject, "estado").Trim();
            var recargas = _repositorio.listarRecargas(null, null, estado == "" ? null : estado);
            return new Mensaje("S", "", "\"recargas\":" + JsonConvert.SerializeObject(recargas));
        }

        public async Task<Mensaje> descargarComprobanteAsync(JObject jObject)
        {
            RecargaIA recarga = _repositorio.obtenerRecarga(RegId(jObject));
            if (recarga == null) return new Mensaje("E", "La recarga no existe.", "");

            var descarga = await _archivos.DownloadFileAsync(recarga.archivo_ruta, recarga.archivo_nombre);
            if (!descarga.Success || descarga.FileStream == null)
            {
                return new Mensaje("E", "No se encontró el archivo del comprobante.", "");
            }
            using var memoria = new MemoryStream();
            await descarga.FileStream.CopyToAsync(memoria);
            descarga.FileStream.Dispose();

            var datos = new
            {
                archivo_nombre = recarga.archivo_nombre,
                content_type = descarga.ContentType ?? "application/octet-stream",
                contenido = Convert.ToBase64String(memoria.ToArray())
            };
            return new Mensaje("S", "", "\"comprobante\":" + JsonConvert.SerializeObject(datos));
        }

        public Mensaje aprobarRecarga(JObject jObject)
        {
            Mensaje error = ValidarClave(jObject);
            if (error != null) return error;

            if (!decimal.TryParse(MontoTexto(jObject["monto_aprobado"]), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal monto)
                || monto <= 0 || monto > MontoMaximo)
            {
                return new Mensaje("E", "Ingrese el monto a acreditar.", "");
            }

            try
            {
                _repositorio.aprobarRecarga(RegId(jObject), Math.Round(monto, 2), _usuario.Usuario);
            }
            catch (SqlException ex) when (ex.Number == ErrorRecargaYaValidada || ex.Number == ErrorMontoInvalido)
            {
                return new Mensaje("E", ex.Message, "");
            }
            return new Mensaje("S", "Recarga aprobada. El saldo ya está disponible para la empresa.", "");
        }

        public Mensaje rechazarRecarga(JObject jObject)
        {
            Mensaje error = ValidarClave(jObject);
            if (error != null) return error;

            string motivo = JsonUtil.Opcional(jObject, "motivo").Trim();
            if (motivo.Length < 5 || motivo.Length > 300)
            {
                return new Mensaje("E", "Indique el motivo del rechazo (entre 5 y 300 caracteres).", "");
            }

            try
            {
                _repositorio.rechazarRecarga(RegId(jObject), motivo, _usuario.Usuario);
            }
            catch (SqlException ex) when (ex.Number == ErrorRecargaYaValidada)
            {
                return new Mensaje("E", ex.Message, "");
            }
            return new Mensaje("S", "Recarga rechazada. La empresa verá el motivo.", "");
        }

        /// <summary>
        /// Pide de nuevo la contraseña del administrador de plataforma: mientras no haya JWT,
        /// la identidad viene de cabeceras y no basta para mover dinero.
        /// </summary>
        private Mensaje ValidarClave(JObject jObject)
        {
            string clave = JsonUtil.Opcional(jObject, "clave");
            if (string.IsNullOrEmpty(clave)) return new Mensaje("E", "Ingrese su contraseña para confirmar.", "");

            var almacenadas = _repositorio.obtenerClavesUsuario(_usuario.Cliente, _usuario.Sociedad, _usuario.Usuario);
            bool valida = almacenadas.Any(a => PasswordHasher.Verificar(clave, a, out _));
            return valida ? null : new Mensaje("E", "La contraseña no es correcta.", "");
        }

        private string FragmentoEmpresa()
        {
            var recargas = _repositorio.listarRecargas(_usuario.Cliente, _usuario.Sociedad, null);
            // La empresa no necesita ver dónde se guardó el archivo ni quién de la plataforma lo validó
            foreach (var columna in new[] { "usuario_valida" })
            {
                if (recargas.Columns.Contains(columna)) recargas.Columns.Remove(columna);
            }
            return "\"recargas\":" + JsonConvert.SerializeObject(recargas)
                + ",\"saldo\":" + JsonConvert.SerializeObject(_repositorio.obtenerSaldo(_usuario.Cliente, _usuario.Sociedad));
        }

        private static int RegId(JObject jObject)
        {
            if (!int.TryParse(JsonUtil.Requerido(jObject, "reg_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int regId))
            {
                throw new ArgumentException("El campo 'reg_id' no es válido.");
            }
            return regId;
        }

        /// <summary>Número JSON o texto con punto decimal, sin depender de la cultura del servidor.</summary>
        private static string MontoTexto(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return "";
            return token.Type == JTokenType.String ? token.ToString() : token.ToString(Formatting.None);
        }
    }
}
