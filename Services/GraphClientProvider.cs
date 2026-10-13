using System.Net.Http.Headers;
using esupplier.Services.IServices;
using esupplier.Utils;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;

namespace esupplier.Services
{
    /// <summary>
    /// Provee un GraphServiceClient compartido (singleton).
    /// Las credenciales de OneDrive se leen de cfg_constante una sola vez y se cachean;
    /// MSAL reutiliza el token mientras sea válido, en lugar de pedir uno nuevo por request.
    /// </summary>
    public interface IGraphClientProvider
    {
        /// <summary>Cliente de Microsoft Graph listo para usar.</summary>
        GraphServiceClient Cliente { get; }

        /// <summary>Correo (usuario de OneDrive) configurado en la constante ONEDRIVE/MAIL.</summary>
        string Correo { get; }
    }

    public class GraphClientProvider : IGraphClientProvider
    {
        private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };
        private static readonly TimeSpan VigenciaCredenciales = TimeSpan.FromMinutes(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GraphClientProvider> _logger;
        private readonly object _lock = new object();

        private IConfidentialClientApplication _app;
        private GraphServiceClient _cliente;
        private string _correo;
        private DateTime _cargadoUtc = DateTime.MinValue;

        public GraphClientProvider(IServiceScopeFactory scopeFactory, ILogger<GraphClientProvider> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public GraphServiceClient Cliente
        {
            get { AsegurarCargado(); return _cliente; }
        }

        public string Correo
        {
            get { AsegurarCargado(); return _correo; }
        }

        private void AsegurarCargado()
        {
            if (_cliente != null && DateTime.UtcNow - _cargadoUtc < VigenciaCredenciales) return;

            lock (_lock)
            {
                if (_cliente != null && DateTime.UtcNow - _cargadoUtc < VigenciaCredenciales) return;

                string clientId = "", clientSecret = "", tenantId = "", correo = "";

                using (var scope = _scopeFactory.CreateScope())
                {
                    var configuracionService = scope.ServiceProvider.GetRequiredService<IConfiguracionService>();
                    var filtro = new JObject
                    {
                        ["adm_cliente_id"] = Constantes.ADM_CLIENTE,
                        ["tipo_constante"] = "ONEDRIVE",
                        ["codigo_constante"] = ""
                    };

                    var msj = configuracionService.listarConstantes(filtro);
                    if (msj == null || msj.tipo != "S" || string.IsNullOrEmpty(msj.data))
                    {
                        throw new InvalidOperationException("No se pudieron cargar las credenciales de OneDrive desde cfg_constante: " + msj?.mensaje);
                    }

                    var constantes = (JArray)JObject.Parse("{" + msj.data + "}")["constantes"] ?? new JArray();
                    foreach (JObject item in constantes)
                    {
                        string codigo = (string)item["codigo_constante"];
                        string valor = (string)item["valor_constante"];
                        switch (codigo)
                        {
                            case "CLIENT_ID": clientId = valor; break;
                            case "SECRECT": clientSecret = valor; break;
                            case "TENANT_ID": tenantId = valor; break;
                            case "MAIL": correo = valor; break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tenantId))
                {
                    throw new InvalidOperationException("Faltan credenciales de OneDrive (CLIENT_ID/SECRECT/TENANT_ID) en cfg_constante.");
                }

                var app = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithTenantId(tenantId)
                    .Build();

                _app = app;
                _cliente = new GraphServiceClient(new DelegateAuthenticationProvider(async request =>
                {
                    // MSAL usa su caché en memoria: solo pide un token nuevo cuando el actual expira.
                    var resultado = await app.AcquireTokenForClient(Scopes).ExecuteAsync();
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resultado.AccessToken);
                }));
                _correo = correo;
                _cargadoUtc = DateTime.UtcNow;
                _logger.LogInformation("Credenciales de OneDrive cargadas desde cfg_constante.");
            }
        }
    }
}
