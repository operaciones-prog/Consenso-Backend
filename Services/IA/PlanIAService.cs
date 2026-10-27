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
using System.Text.RegularExpressions;

namespace esupplier.Services.IA
{
    public interface IPlanIAService
    {
        /// <summary>Planes con precio, la suscripción de la sociedad, sus cobros y el saldo.</summary>
        Mensaje plan();

        /// <summary>
        /// Contrata un plan o cambia de plan. Body: {"codigo_plan","token","contacto":{nombres, apellidos, email,
        /// tipo_documento, numero_documento, telefono},"tarjeta":{marca, ultimos4}}.
        /// Sin suscripción viva hacen falta token y contacto; con suscripción, el cambio no vuelve a pedir la tarjeta.
        /// </summary>
        Task<Mensaje> suscribirAsync(JObject jObject);

        /// <summary>Body: {"token","tarjeta":{marca, ultimos4}}.</summary>
        Task<Mensaje> cambiarTarjetaAsync(JObject jObject);

        Task<Mensaje> cancelarAsync();

        Task<Mensaje> anularCambioPlanAsync();

        /// <summary>Webhook de Kushki. false si la firma no es válida (no se procesa nada).</summary>
        Task<bool> procesarWebhookAsync(string cuerpo, string kushkiId, string firma, string firmaSimple);
    }

    /// <summary>
    /// Plan mensual de IA de la empresa, cobrado con tarjeta por Kushki:
    ///  - El precio SIEMPRE lo calcula el backend (tb_ia_plan + IVA); el navegador solo manda el token de la
    ///    tarjeta (Kushki Hosted Fields) y qué plan quiere.
    ///  - Kushki guarda la tarjeta y cobra cada mes por su cuenta. El plan se activa con el primer cobro
    ///    (a partir del día siguiente) y se renueva con cada cobro confirmado por webhook.
    ///  - Cambiar de plan se aplica en el próximo cobro (sin prorrateos); cancelar deja el plan hasta fin de periodo.
    ///  - El saldo del plan se usa antes que el recargado con comprobante y no se acumula entre periodos.
    /// </summary>
    public class PlanIAService : IPlanIAService
    {
        private const int ErrorSuscripcionExistente = 50901;
        private const int ErrorSuscripcionNoRegistrada = 50904;
        private const string UsuarioWebhook = "kushki-webhook";

        private static readonly Regex PatronToken = new("^[A-Za-z0-9_-]{16,100}$");
        private static readonly Regex PatronEmail = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        private readonly IPlanIARepository _repositorio;
        private readonly IIARepository _ia;
        private readonly IdentidadSolicitud _usuario;
        private readonly IHeaderService _header;
        private readonly IKushkiCliente _kushki;
        private readonly IConfiguracionKushki _configuracionKushki;
        private readonly ILogger<PlanIAService> _logger;

        public PlanIAService(IPlanIARepository repositorio, IIARepository ia, IdentidadSolicitud usuario, IHeaderService header,
            IKushkiCliente kushki, IConfiguracionKushki configuracionKushki, ILogger<PlanIAService> logger)
        {
            _repositorio = repositorio;
            _ia = ia;
            _usuario = usuario;
            _header = header;
            _kushki = kushki;
            _configuracionKushki = configuracionKushki;
            _logger = logger;
        }

        private static DateTime Hoy => FechaUtil.AhoraEcuador().Date;

        // ================================ Consulta ================================

        public Mensaje plan() => new Mensaje("S", "", Fragmento());

        /// <summary>"plan":{planes, suscripcion, pagos, saldo, iva_pct, dias_gracia, kushki}</summary>
        private string Fragmento()
        {
            ConfigPlanIA config = _repositorio.obtenerConfig();
            List<PlanIA> planes = _repositorio.listarPlanes(soloActivos: false);
            SuscripcionIA s = _repositorio.obtenerSuscripcion(_usuario.Cliente, _usuario.Sociedad);
            bool ingles = _header.obtenerIdioma() == "EN";

            KushkiPublico kushki = _configuracionKushki.Publico();

            var pagos = _repositorio.listarPagos(_usuario.Cliente, _usuario.Sociedad);
            foreach (var columna in new[] { "adm_cliente_id", "codigo_sociedad" })
            {
                if (pagos.Columns.Contains(columna)) pagos.Columns.Remove(columna);
            }

            var datos = new
            {
                // Los inactivos solo se muestran si son el plan actual o el programado
                planes = planes
                    .Where(p => p.activo || p.codigo_plan == s?.codigo_plan || p.codigo_plan == s?.codigo_plan_siguiente)
                    .Select(p =>
                    {
                        MontoKushki m = Monto(p, config);
                        return new
                        {
                            p.codigo_plan,
                            nombre = ingles ? p.nombre_en : p.nombre,
                            descripcion = ingles ? p.descripcion_en : p.descripcion,
                            subtotal_usd = m.Subtotal,
                            iva_usd = m.Iva,
                            total_usd = m.Total,
                            p.saldo_mes_usd,
                            p.recomendado,
                            p.activo
                        };
                    }),
                suscripcion = Publica(s, planes, ingles, config),
                pagos,
                saldo = _ia.obtenerSaldo(_usuario.Cliente, _usuario.Sociedad),
                config.iva_pct,
                config.dias_gracia,
                kushki = new
                {
                    llave_publica = kushki.LlavePublica,
                    prueba = kushki.Prueba,
                    configurado = kushki.Configurado
                }
            };
            return "\"plan\":" + JsonConvert.SerializeObject(datos);
        }

        /// <summary>Lo que ve la empresa de su suscripción (sin el id de Kushki). null si no tiene.</summary>
        private static object Publica(SuscripcionIA s, List<PlanIA> planes, bool ingles, ConfigPlanIA config)
        {
            string estado = EstadoSuscripcionIA.De(s, Hoy);
            if (estado == EstadoSuscripcionIA.Ninguna) return null;
            string Nombre(string codigo) => planes.FirstOrDefault(p => p.codigo_plan == codigo) is PlanIA p
                ? (ingles ? p.nombre_en : p.nombre) : codigo;
            return new
            {
                estado,
                s.codigo_plan,
                plan_nombre = Nombre(s.codigo_plan),
                s.codigo_plan_siguiente,
                plan_siguiente_nombre = s.codigo_plan_siguiente == null ? null : Nombre(s.codigo_plan_siguiente),
                inicio_periodo = Dia(s.inicio_periodo),
                fin_periodo = Dia(s.fin_periodo),
                proximo_cobro = Dia(s.proximo_cobro),
                // Último día en que el plan sigue funcionando mientras Kushki reintenta el cobro
                fin_gracia = estado == EstadoSuscripcionIA.Gracia ? Dia(s.fin_periodo?.AddDays(config.dias_gracia)) : null,
                tarjeta = s.kushki_suscripcion_id == null || s.tarjeta_ultimos4 == null
                    ? null : new { marca = s.tarjeta_marca, ultimos4 = s.tarjeta_ultimos4 },
                s.intentos_fallidos,
                s.ultimo_error,
                // Para no volver a pedirlos al reactivar
                contacto = new ContactoPlanIA
                {
                    nombres = s.nombres,
                    apellidos = s.apellidos,
                    email = s.email,
                    tipo_documento = s.tipo_documento,
                    numero_documento = s.numero_documento,
                    telefono = s.telefono
                }
            };
        }

        // ================================ Contratar / cambiar plan ================================

        public async Task<Mensaje> suscribirAsync(JObject jObject)
        {
            string codigo = JsonUtil.Requerido(jObject, "codigo_plan").Trim().ToLowerInvariant();
            PlanIA plan = _repositorio.listarPlanes(soloActivos: true).FirstOrDefault(p => p.codigo_plan == codigo);
            if (plan == null) return new Mensaje("E", "El plan no existe.", "");

            ConfigPlanIA config = _repositorio.obtenerConfig();
            SuscripcionIA s = _repositorio.obtenerSuscripcion(_usuario.Cliente, _usuario.Sociedad);

            try
            {
                return s?.kushki_suscripcion_id != null
                    ? await CambiarPlanAsync(s, plan, config)
                    : await NuevaSuscripcionAsync(jObject, s, plan, config);
            }
            catch (KushkiException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
        }

        /// <summary>Con la suscripción viva: Kushki cobrará el plan nuevo desde el próximo cobro.</summary>
        private async Task<Mensaje> CambiarPlanAsync(SuscripcionIA s, PlanIA plan, ConfigPlanIA config)
        {
            if (s.codigo_plan_siguiente != null && plan.codigo_plan == s.codigo_plan)
            {
                return await anularCambioPlanAsync();
            }
            if (plan.codigo_plan == (s.codigo_plan_siguiente ?? s.codigo_plan))
            {
                return new Mensaje("E", "Ya tiene este plan.", "");
            }

            await _kushki.ActualizarSuscripcionAsync(s.kushki_suscripcion_id, NombreKushki(plan), Monto(plan, config));

            string fecha = Dia(s.proximo_cobro, "dd/MM/yyyy");
            if (s.inicio_periodo == null)
            {
                // Todavía no se cobró nada: el cambio es inmediato
                _repositorio.actualizarSuscripcion(_usuario.Cliente, _usuario.Sociedad, "cambiarPlan", plan.codigo_plan, null, null, _usuario.Usuario);
                return new Mensaje("S", $"Plan cambiado a {plan.nombre}. El primer cobro se hará el {fecha}.", Fragmento());
            }
            _repositorio.actualizarSuscripcion(_usuario.Cliente, _usuario.Sociedad, "programarPlan", plan.codigo_plan, null, null, _usuario.Usuario);
            return new Mensaje("S", $"El cambio al plan {plan.nombre} se aplicará en la próxima renovación ({fecha}).", Fragmento());
        }

        /// <summary>Registra la tarjeta en Kushki con una suscripción nueva (contratar o reactivar).</summary>
        private async Task<Mensaje> NuevaSuscripcionAsync(JObject jObject, SuscripcionIA anterior, PlanIA plan, ConfigPlanIA config)
        {
            if (!_configuracionKushki.Publico().Configurado)
            {
                return new Mensaje("E", "El pago con tarjeta aún no está habilitado. Contacte al administrador de la plataforma.", "");
            }
            string token = Token(jObject);
            ContactoPlanIA contacto = ValidarContacto(jObject["contacto"] as JObject);
            var (marca, ultimos4) = Tarjeta(jObject);

            // Si el plan sigue en su periodo (p. ej. se había cancelado), el cobro empieza al terminar: sin cortes ni doble cobro
            DateTime inicio = Hoy.AddDays(1);
            bool continua = anterior?.fin_periodo is DateTime fin && fin.Date >= Hoy;
            if (continua && anterior.fin_periodo.Value.Date.AddDays(1) > inicio) inicio = anterior.fin_periodo.Value.Date.AddDays(1);

            var metadata = new Dictionary<string, string>
            {
                ["adm_cliente_id"] = _usuario.Cliente,
                ["codigo_sociedad"] = _usuario.Sociedad,
                ["codigo_plan"] = plan.codigo_plan
            };
            SuscripcionKushki nueva = await _kushki.CrearSuscripcionAsync(token, NombreKushki(plan), contacto, Monto(plan, config), inicio, metadata);

            try
            {
                _repositorio.registrarSuscripcion(new SuscripcionIA
                {
                    adm_cliente_id = _usuario.Cliente,
                    codigo_sociedad = _usuario.Sociedad,
                    codigo_plan = plan.codigo_plan,
                    kushki_suscripcion_id = nueva.Id,
                    proximo_cobro = inicio,
                    tarjeta_marca = nueva.Marca ?? marca,
                    tarjeta_ultimos4 = nueva.Ultimos4 ?? ultimos4,
                    email = contacto.email,
                    nombres = contacto.nombres,
                    apellidos = contacto.apellidos,
                    tipo_documento = contacto.tipo_documento,
                    numero_documento = contacto.numero_documento,
                    telefono = contacto.telefono
                }, _usuario.Usuario);
            }
            catch (Exception ex)
            {
                // Sin registro en la BD la suscripción de Kushki quedaría cobrando sin dar el plan: se anula
                await AnularHuerfanaAsync(nueva.Id);
                if (ex is SqlException sql && sql.Number == ErrorSuscripcionExistente)
                {
                    return new Mensaje("E", "La empresa ya tiene una suscripción activa.", Fragmento());
                }
                throw;
            }

            string fecha = inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            return new Mensaje("S", continua
                ? $"Suscripción reactivada. El próximo cobro será el {fecha}."
                : $"Tarjeta registrada. El primer cobro se hará el {fecha} y el plan se activará apenas se confirme el pago.",
                Fragmento());
        }

        private async Task AnularHuerfanaAsync(string suscripcionId)
        {
            try
            {
                await _kushki.CancelarSuscripcionAsync(suscripcionId);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Suscripción {Suscripcion} creada en Kushki sin registro en la BD: anularla manualmente", suscripcionId);
            }
        }

        public async Task<Mensaje> cambiarTarjetaAsync(JObject jObject)
        {
            SuscripcionIA s = _repositorio.obtenerSuscripcion(_usuario.Cliente, _usuario.Sociedad);
            if (s?.kushki_suscripcion_id == null)
            {
                return new Mensaje("E", "No tiene una suscripción activa. Elija un plan para registrar su tarjeta.", "");
            }
            string token = Token(jObject);
            var (marca, ultimos4) = Tarjeta(jObject);
            try
            {
                await _kushki.CambiarTarjetaAsync(s.kushki_suscripcion_id, token);
            }
            catch (KushkiException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            _repositorio.actualizarSuscripcion(_usuario.Cliente, _usuario.Sociedad, "tarjeta", null, marca, ultimos4, _usuario.Usuario);
            return new Mensaje("S", "Tarjeta actualizada. Se usará desde el próximo cobro.", Fragmento());
        }

        public async Task<Mensaje> cancelarAsync()
        {
            SuscripcionIA s = _repositorio.obtenerSuscripcion(_usuario.Cliente, _usuario.Sociedad);
            if (s?.kushki_suscripcion_id == null) return new Mensaje("E", "No tiene una suscripción activa.", "");
            try
            {
                await _kushki.CancelarSuscripcionAsync(s.kushki_suscripcion_id);
            }
            catch (KushkiException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            _repositorio.actualizarSuscripcion(_usuario.Cliente, _usuario.Sociedad, "cancelar", null, null, null, _usuario.Usuario);
            return new Mensaje("S", s.inicio_periodo == null
                ? "Suscripción cancelada. No se realizó ningún cobro."
                : $"Suscripción cancelada. El plan seguirá activo hasta el {Dia(s.fin_periodo, "dd/MM/yyyy")}.",
                Fragmento());
        }

        public async Task<Mensaje> anularCambioPlanAsync()
        {
            SuscripcionIA s = _repositorio.obtenerSuscripcion(_usuario.Cliente, _usuario.Sociedad);
            if (s?.kushki_suscripcion_id == null || s.codigo_plan_siguiente == null)
            {
                return new Mensaje("E", "No hay un cambio de plan programado.", "");
            }
            PlanIA actual = _repositorio.listarPlanes(soloActivos: false).First(p => p.codigo_plan == s.codigo_plan);
            try
            {
                await _kushki.ActualizarSuscripcionAsync(s.kushki_suscripcion_id, NombreKushki(actual), Monto(actual, _repositorio.obtenerConfig()));
            }
            catch (KushkiException ex)
            {
                return new Mensaje("E", ex.Message, "");
            }
            _repositorio.actualizarSuscripcion(_usuario.Cliente, _usuario.Sociedad, "programarPlan", null, null, null, _usuario.Usuario);
            return new Mensaje("S", $"Se mantiene el plan {actual.nombre}.", Fragmento());
        }

        // ================================ Webhook ================================

        public async Task<bool> procesarWebhookAsync(string cuerpo, string kushkiId, string firma, string firmaSimple)
        {
            if (!await _kushki.FirmaValidaAsync(cuerpo, kushkiId, firma, firmaSimple)) return false;

            JObject raiz = JObject.Parse(cuerpo);
            string nombre = Texto(raiz, "name") ?? "";
            JObject evento = raiz["event"] as JObject ?? raiz;
            string suscripcionId = Texto(evento, "subscriptionId");
            string eventoId = Texto(evento, "id");
            if (string.IsNullOrEmpty(suscripcionId))
            {
                _logger.LogInformation("Webhook de Kushki {Nombre} sin subscriptionId: se ignora", nombre);
                return true;
            }

            var (subtotal, iva, total) = MontoEvento(evento["amount"] as JObject);
            try
            {
                switch (nombre)
                {
                    // "succesfullCharge" es como lo escribe Kushki
                    case "succesfullCharge":
                    case "successfulCharge":
                        bool aplicado = _repositorio.acreditarCobro(suscripcionId, Texto(evento, "ticketNumber"), eventoId,
                            FechaCobro(evento["lastChargeDate"]), subtotal, iva, total, UsuarioWebhook);
                        _logger.LogInformation("Cobro de la suscripción {Suscripcion} {Resultado}", suscripcionId,
                            aplicado ? "acreditado" : "ya procesado");
                        break;

                    case "failedRetry":
                    case "declinedCharge":
                        _repositorio.registrarCobroFallido(suscripcionId, eventoId, subtotal, iva, total,
                            MotivoRechazo(raiz, evento), ultimo: false, UsuarioWebhook);
                        break;

                    case "lastRetry":
                        // Se agotaron los reintentos: la suscripción se da de baja y la empresa debe volver a registrar su tarjeta
                        try
                        {
                            await _kushki.CancelarSuscripcionAsync(suscripcionId);
                        }
                        catch (KushkiException ex)
                        {
                            _logger.LogWarning(ex, "No se pudo dar de baja en Kushki la suscripción {Suscripcion} tras el último reintento", suscripcionId);
                        }
                        _repositorio.registrarCobroFallido(suscripcionId, eventoId, subtotal, iva, total,
                            "No se pudo cobrar la renovación después de varios intentos. Registre una tarjeta para continuar.",
                            ultimo: true, UsuarioWebhook);
                        break;

                    case "subscriptionDelete":
                        SuscripcionIA s = _repositorio.obtenerSuscripcionKushki(suscripcionId);
                        if (s != null)
                        {
                            _repositorio.actualizarSuscripcion(s.adm_cliente_id, s.codigo_sociedad, "baja", null, null, null, UsuarioWebhook);
                        }
                        break;

                    default:
                        _logger.LogInformation("Webhook de Kushki {Nombre} sin acción", nombre);
                        break;
                }
            }
            catch (SqlException ex) when (ex.Number == ErrorSuscripcionNoRegistrada)
            {
                // Suscripción de otro ambiente o ya reemplazada: se confirma para que Kushki no reintente
                _logger.LogWarning("Webhook de Kushki {Nombre} de una suscripción no registrada", nombre);
            }
            return true;
        }

        private static (decimal subtotal, decimal iva, decimal total) MontoEvento(JObject monto)
        {
            decimal Valor(string campo) => monto?[campo] is JValue v && v.Type != JTokenType.Null
                ? Convert.ToDecimal(v.Value, CultureInfo.InvariantCulture) : 0m;
            decimal subtotal = Valor("subtotalIva") + Valor("subtotalIva0");
            decimal iva = Valor("iva");
            return (subtotal, iva, subtotal + iva + Valor("ice"));
        }

        /// <summary>Día de Ecuador del cobro: fecha ISO o milisegundos Unix; si no viene, hoy.</summary>
        private static DateTime FechaCobro(JToken valor)
        {
            if (valor is JValue v && v.Type != JTokenType.Null)
            {
                if ((v.Type == JTokenType.Integer || v.Type == JTokenType.Float)
                    && Convert.ToDouble(v.Value, CultureInfo.InvariantCulture) is double ms && ms > 1e11)
                {
                    return FechaUtil.AEcuador(DateTimeOffset.FromUnixTimeMilliseconds((long)ms).UtcDateTime).Date;
                }
                string texto = Convert.ToString(v.Value, CultureInfo.InvariantCulture);
                if (Regex.IsMatch(texto ?? "", @"^\d{4}-\d{2}-\d{2}$")
                    && DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dia))
                {
                    return dia;
                }
                if (DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset instante))
                {
                    return FechaUtil.AEcuador(instante.UtcDateTime).Date;
                }
            }
            return Hoy;
        }

        private static string MotivoRechazo(JObject raiz, JObject evento)
        {
            string texto = Texto(raiz, "responseText") ?? Texto(evento, "responseText")
                ?? (raiz["processor"] as JObject)?["message"]?.ToString();
            texto = string.IsNullOrWhiteSpace(texto) ? "El banco rechazó el cobro." : texto.Trim();
            return texto.Length <= 300 ? texto : texto[..300];
        }

        // ================================ Comunes ================================

        /// <summary>Precio SIN IVA del plan + IVA de la configuración.</summary>
        private static MontoKushki Monto(PlanIA plan, ConfigPlanIA config) =>
            new MontoKushki(plan.precio_mensual_usd,
                Math.Round(plan.precio_mensual_usd * config.iva_pct / 100m, 2, MidpointRounding.AwayFromZero));

        private static string NombreKushki(PlanIA plan) => "IA Consenso - " + plan.nombre;

        private static string Token(JObject jObject)
        {
            string token = JsonUtil.Opcional(jObject, "token").Trim();
            if (token == "") throw new ArgumentException("Ingrese los datos de la tarjeta.");
            if (!PatronToken.IsMatch(token)) throw new ArgumentException("El token de la tarjeta no es válido.");
            return token;
        }

        /// <summary>Marca y últimos 4 que Kushki Hosted Fields informa al navegador (solo para mostrar).</summary>
        private static (string marca, string ultimos4) Tarjeta(JObject jObject)
        {
            JObject t = jObject["tarjeta"] as JObject;
            string marca = JsonUtil.Opcional(t, "marca").Trim();
            string ultimos4 = JsonUtil.Opcional(t, "ultimos4").Trim();
            return (marca == "" ? null : marca.Length <= 30 ? marca : marca[..30],
                    Regex.IsMatch(ultimos4, @"^\d{4}$") ? ultimos4 : null);
        }

        /// <summary>Datos que Kushki exige para la suscripción.</summary>
        public static ContactoPlanIA ValidarContacto(JObject c)
        {
            if (c == null) throw new CampoRequeridoException("contacto");
            string Campo(string campo, int min, int max, string nombre)
            {
                string t = JsonUtil.Opcional(c, campo).Trim();
                if (t.Length < min || t.Length > max) throw new ArgumentException($"Ingrese {nombre} ({min} a {max} caracteres).");
                return t;
            }

            string nombres = Campo("nombres", 2, 60, "los nombres");
            string apellidos = Campo("apellidos", 2, 60, "los apellidos");

            string email = JsonUtil.Opcional(c, "email").Trim();
            if (email.Length < 5 || email.Length > 150 || !PatronEmail.IsMatch(email))
                throw new ArgumentException("Ingrese un correo válido (para el comprobante de pago).");

            string tipo = JsonUtil.Opcional(c, "tipo_documento").Trim().ToUpperInvariant();
            string documento = JsonUtil.Opcional(c, "numero_documento").Trim().ToUpperInvariant();
            bool documentoValido = tipo switch
            {
                "CI" => Regex.IsMatch(documento, @"^\d{10}$"),
                "RUC" => Regex.IsMatch(documento, @"^\d{13}$"),
                "PAS" => Regex.IsMatch(documento, "^[A-Z0-9]{5,20}$"),
                _ => throw new ArgumentException("Elija el tipo de documento (cédula, RUC o pasaporte).")
            };
            if (!documentoValido)
                throw new ArgumentException(tipo == "CI" ? "La cédula debe tener 10 dígitos."
                    : tipo == "RUC" ? "El RUC debe tener 13 dígitos." : "Ingrese un pasaporte válido.");

            return new ContactoPlanIA
            {
                nombres = nombres,
                apellidos = apellidos,
                email = email,
                tipo_documento = tipo,
                numero_documento = documento,
                telefono = Telefono(JsonUtil.Opcional(c, "telefono"))
            };
        }

        /// <summary>Teléfono en E.164 (+593...). Acepta 09XXXXXXXX, 593..., o +código de país.</summary>
        public static string Telefono(string valor)
        {
            string t = (valor ?? "").Trim();
            string digitos = new string(t.Where(char.IsDigit).ToArray());
            string e164 = t.StartsWith("+") ? "+" + digitos
                : digitos.StartsWith("593") && digitos.Length >= 11 ? "+" + digitos
                : digitos.StartsWith("0") ? "+593" + digitos[1..]
                : "+593" + digitos;
            if (!Regex.IsMatch(e164, @"^\+\d{8,15}$")) throw new ArgumentException("Ingrese un teléfono válido.");
            return e164;
        }

        private static string Dia(DateTime? fecha, string formato = "yyyy-MM-dd") =>
            fecha?.ToString(formato, CultureInfo.InvariantCulture);

        private static string Texto(JObject o, string propiedad) =>
            o?[propiedad] is JValue v && v.Type != JTokenType.Null ? Convert.ToString(v.Value, CultureInfo.InvariantCulture) : null;
    }
}
