using System.Data;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using esupplier.Models.IA;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.Auth;
using esupplier.Utils;

namespace esupplier.Services.IA
{
    /// <summary>Lo que un agente le pide al motor.</summary>
    public class SolicitudAgente
    {
        public string CodigoAddon { get; init; } = "";
        public string Sistema { get; init; } = "";
        public List<MessageParam> Mensajes { get; init; } = new();
        /// <summary>Herramientas que Claude puede usar (consultas de solo lectura). null = sin herramientas.</summary>
        public List<ToolUnion> Herramientas { get; init; }
        /// <summary>Ejecuta una herramienta: (nombre, entrada) → texto o JSON con el resultado.</summary>
        public Func<string, IReadOnlyDictionary<string, JsonElement>, Task<string>> EjecutarHerramienta { get; init; }
        public int MaxTokens { get; init; } = 4000;
    }

    public class ResultadoAgente
    {
        public bool Exito { get; init; }
        /// <summary>Respuesta en Markdown (si Exito) o mensaje de error para el usuario.</summary>
        public string Texto { get; init; } = "";
        public static ResultadoAgente Error(string mensaje) => new() { Exito = false, Texto = mensaje };
    }

    public interface IMotorAgentesIA
    {
        Task<ResultadoAgente> EjecutarAsync(SolicitudAgente solicitud);
    }

    /// <summary>
    /// Motor común de los agentes IA:
    ///  1. el complemento debe estar activo en la sociedad y permitido para el rol de quien pregunta;
    ///  2. usa la clave propia de la empresa (si está verificada y no superó su límite mensual) o,
    ///     si no, la clave de la plataforma cuando la empresa tiene saldo;
    ///  3. llama a Claude, resolviendo las herramientas de consulta que pida (máximo <see cref="MaxVueltas"/>);
    ///  4. registra tokens y costo en tb_ia_uso y descuenta el saldo si usó la clave de la plataforma.
    /// Los agentes solo consultan y sugieren: ninguna herramienta modifica datos.
    /// </summary>
    public class MotorAgentesIA : IMotorAgentesIA
    {
        private const int MaxVueltas = 6;

        /// <summary>US$ por millón de tokens (entrada, salida).</summary>
        private static readonly Dictionary<string, (decimal entrada, decimal salida)> Precios = new()
        {
            ["claude-opus-5-5"] = (4m, 20m),
            ["claude-sonnet-5-5"] = (2m, 10m),
            ["claude-haiku-5-5"] = (0.10m, 0.50m),
        };

        private readonly IIARepository _repositorio;
        private readonly IdentidadSolicitud _usuario;
        private readonly IAlmacenSecretos _secretos;
        private readonly ILogger<MotorAgentesIA> _logger;

        public MotorAgentesIA(IIARepository repositorio, IdentidadSolicitud usuario, IAlmacenSecretos secretos, ILogger<MotorAgentesIA> logger)
        {
            _repositorio = repositorio;
            _usuario = usuario;
            _secretos = secretos;
            _logger = logger;
        }

        public async Task<ResultadoAgente> EjecutarAsync(SolicitudAgente solicitud)
        {
            string permiso = ValidarComplemento(solicitud.CodigoAddon);
            if (permiso != null) return ResultadoAgente.Error(permiso);

            var (clave, modelo, conSaldo, errorClave) = await ResolverClaveAsync();
            if (errorClave != null) return ResultadoAgente.Error(errorClave);

            var cliente = new AnthropicClient { ApiKey = clave, MaxRetries = 1 };
            var mensajes = new List<MessageParam>(solicitud.Mensajes);
            var texto = new StringBuilder();
            long tokensEntrada = 0, tokensSalida = 0;

            try
            {
                for (int vuelta = 1; vuelta <= MaxVueltas; vuelta++)
                {
                    var parametros = new MessageCreateParams
                    {
                        Model = modelo,
                        MaxTokens = solicitud.MaxTokens,
                        System = solicitud.Sistema,
                        // Respuestas rápidas: la API Gateway corta a los 29 s
                        OutputConfig = new OutputConfig { Effort = Effort.Low },
                        Messages = mensajes,
                    };
                    if (solicitud.Herramientas != null) parametros = parametros with { Tools = solicitud.Herramientas };

                    Message respuesta = await cliente.Messages.Create(parametros);
                    tokensEntrada += respuesta.Usage.InputTokens
                        + (respuesta.Usage.CacheCreationInputTokens ?? 0) + (respuesta.Usage.CacheReadInputTokens ?? 0);
                    tokensSalida += respuesta.Usage.OutputTokens;

                    var eco = new List<ContentBlockParam>();
                    var resultados = new List<ContentBlockParam>();
                    foreach (ContentBlock bloque in respuesta.Content)
                    {
                        if (bloque.TryPickText(out TextBlock t))
                        {
                            eco.Add(new TextBlockParam { Text = t.Text });
                            texto.Append(t.Text);
                        }
                        else if (bloque.TryPickThinking(out ThinkingBlock th))
                        {
                            eco.Add(new ThinkingBlockParam { Thinking = th.Thinking, Signature = th.Signature });
                        }
                        else if (bloque.TryPickRedactedThinking(out RedactedThinkingBlock rt))
                        {
                            eco.Add(new RedactedThinkingBlockParam { Data = rt.Data });
                        }
                        else if (bloque.TryPickToolUse(out ToolUseBlock uso))
                        {
                            eco.Add(new ToolUseBlockParam { ID = uso.ID, Name = uso.Name, Input = uso.Input });
                            resultados.Add(await EjecutarHerramientaSeguraAsync(solicitud, uso));
                        }
                    }

                    string motivo = respuesta.StopReason?.Raw() ?? "";
                    if (motivo == "refusal")
                    {
                        RegistrarUso(solicitud.CodigoAddon, modelo, tokensEntrada, tokensSalida, conSaldo);
                        return ResultadoAgente.Error("El asistente no puede responder esa consulta. Intente reformularla.");
                    }
                    if (motivo == "tool_use" && resultados.Count > 0 && vuelta < MaxVueltas)
                    {
                        texto.Clear(); // el texto previo a las herramientas es solo avance
                        mensajes.Add(new AssistantMessageParam { Content = eco });
                        mensajes.Add(new UserMessageParam { Content = resultados });
                        continue;
                    }
                    if (motivo == "max_tokens") texto.Append("\n\n_(Respuesta recortada por longitud.)_");
                    break;
                }
            }
            catch (AnthropicUnauthorizedException)
            {
                return ResultadoAgente.Error("La API key ya no es válida. Un administrador debe revisarla en Complementos IA.");
            }
            catch (AnthropicRateLimitException)
            {
                RegistrarUso(solicitud.CodigoAddon, modelo, tokensEntrada, tokensSalida, conSaldo);
                return ResultadoAgente.Error("El proveedor de IA está recibiendo muchas consultas. Intente en un momento.");
            }
            catch (Exception ex) when (ex is Anthropic5xxException || ex is AnthropicIOException || ex is AnthropicApiException || ex is TaskCanceledException)
            {
                _logger.LogWarning(ex, "Fallo la llamada a Claude en {Addon}", solicitud.CodigoAddon);
                RegistrarUso(solicitud.CodigoAddon, modelo, tokensEntrada, tokensSalida, conSaldo);
                return ResultadoAgente.Error("No se pudo contactar al proveedor de IA. Intente nuevamente.");
            }

            RegistrarUso(solicitud.CodigoAddon, modelo, tokensEntrada, tokensSalida, conSaldo);
            string final = texto.ToString().Trim();
            return final == ""
                ? ResultadoAgente.Error("El asistente no devolvió una respuesta. Intente nuevamente.")
                : new ResultadoAgente { Exito = true, Texto = final };
        }

        /// <summary>null si el usuario puede usar el complemento; si no, el motivo.</summary>
        private string ValidarComplemento(string codigoAddon)
        {
            DataTable addons = _repositorio.listarAddon(_usuario.Cliente, _usuario.Sociedad, "ES");
            DataRow fila = addons.AsEnumerable().FirstOrDefault(r => (r["codigo_addon"] as string) == codigoAddon);
            if (fila == null || !(fila["activo"] is bool activo && activo))
            {
                return "Este complemento no está activo en su empresa. Un administrador puede activarlo en Complementos IA.";
            }
            var roles = ((fila["roles"] as string) ?? "").Split(',').Select(r => r.Trim());
            return roles.Contains(_usuario.RolId) ? null : "Este complemento no está disponible para su perfil.";
        }

        /// <summary>Clave propia si está verificada y dentro del límite; si no, la de la plataforma con saldo.</summary>
        private async Task<(string clave, string modelo, bool conSaldo, string error)> ResolverClaveAsync()
        {
            CredencialIA propia = _repositorio.obtenerCredencial(_usuario.Cliente, _usuario.Sociedad);
            if (propia != null && propia.estado == EstadoCredencialIA.Verificada)
            {
                if (propia.limite_mensual_usd is decimal limite && propia.consumo_mes_usd >= limite)
                {
                    return (null, null, false, "Se alcanzó el límite mensual de consumo de IA de su empresa.");
                }
                string clave = await _secretos.LeerAsync(propia.secreto_ref);
                if (clave != null) return (clave, propia.modelo, false, null);
            }

            CredencialIA plataforma = _repositorio.obtenerCredencial(Constantes.AMBITO_PLATAFORMA, Constantes.AMBITO_PLATAFORMA);
            if (plataforma == null || plataforma.estado != EstadoCredencialIA.Verificada)
            {
                return (null, null, false, "No hay una conexión de IA disponible. Un administrador debe configurar la API key o recargar saldo.");
            }
            if (_repositorio.obtenerSaldo(_usuario.Cliente, _usuario.Sociedad).saldo_usd <= 0)
            {
                return (null, null, false, "Su empresa no tiene saldo de IA. Un administrador puede recargarlo en Complementos IA.");
            }
            if (plataforma.limite_mensual_usd is decimal tope && plataforma.consumo_mes_usd >= tope)
            {
                return (null, null, false, "El servicio de IA de la plataforma alcanzó su límite mensual. Intente más tarde.");
            }
            string clavePlataforma = await _secretos.LeerAsync(plataforma.secreto_ref);
            return clavePlataforma == null
                ? (null, null, false, "No hay una conexión de IA disponible.")
                : (clavePlataforma, plataforma.modelo, true, null);
        }

        /// <summary>Un error de la herramienta se devuelve a Claude como resultado con is_error, sin cortar la respuesta.</summary>
        private async Task<ContentBlockParam> EjecutarHerramientaSeguraAsync(SolicitudAgente solicitud, ToolUseBlock uso)
        {
            try
            {
                if (solicitud.EjecutarHerramienta == null) throw new InvalidOperationException("Herramienta no disponible.");
                string salida = await solicitud.EjecutarHerramienta(uso.Name, uso.Input);
                return new ToolResultBlockParam { ToolUseID = uso.ID, Content = Recortar(salida) };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falló la herramienta {Herramienta} del agente {Addon}", uso.Name, solicitud.CodigoAddon);
                return new ToolResultBlockParam { ToolUseID = uso.ID, Content = "No se pudo obtener la información: " + ex.Message, IsError = true };
            }
        }

        /// <summary>Evita mandar resultados enormes (costo y tiempo).</summary>
        public static string Recortar(string texto, int maximo = 60000) =>
            texto == null ? "" : texto.Length <= maximo ? texto : texto[..maximo] + "\n…(resultado recortado)";

        private void RegistrarUso(string addon, string modelo, long entrada, long salida, bool conSaldo)
        {
            if (entrada == 0 && salida == 0) return;
            var (pEntrada, pSalida) = Precios.TryGetValue(modelo, out var p) ? p : (4m, 20m);
            decimal costo = Math.Round(entrada * pEntrada / 1_000_000m + salida * pSalida / 1_000_000m, 6);
            try
            {
                _repositorio.registrarUso(_usuario.Cliente, _usuario.Sociedad, _usuario.Usuario, addon, modelo,
                    (int)Math.Min(entrada, int.MaxValue), (int)Math.Min(salida, int.MaxValue), costo, conSaldo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar el uso de IA de {Addon}", addon);
            }
        }
    }
}
