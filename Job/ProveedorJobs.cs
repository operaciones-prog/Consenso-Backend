using esupplier.Models.Response;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services.IServices;
using esupplier.Utils;
using ExcelDataReader;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace esupplier.Job
{
    public class ProveedorJobs: IHostedService, IDisposable
    {
        private Timer _timer;
        private List<TimeSpan> _executionTimes;
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly IConfiguration _configuration;

        public ProveedorJobs(IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _executionTimes = new List<TimeSpan>();

            // Cargar las horas de ejecución desde appsettings.json
            var executionTimesConfig = _configuration.GetSection("ExecutionTimesJob").Get<List<string>>();
            foreach (var time in executionTimesConfig)
            {
                if (TimeSpan.TryParse(time, out var parsedTime))
                {
                    _executionTimes.Add(parsedTime);
                }
            }

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
           

            // Configurar el job para que se verifique cada minuto
            _timer = new Timer(CheckTimeToExecute, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            return Task.CompletedTask;
        }

        private void CheckTimeToExecute(object state)
        {
            var currentTime = DateTime.Now.TimeOfDay;

            // Verifica si la hora actual coincide con alguna de las horas configuradas
            foreach (var executionTime in _executionTimes)
            {
                if (currentTime.Hours == executionTime.Hours && currentTime.Minutes == executionTime.Minutes)
                {
                    DoWorkAsync();
                    break;
                }
            }
        }

        private async Task DoWorkAsync()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var proveedorService = scope.ServiceProvider.GetRequiredService<IProveedorService>();
                var configuracionService = scope.ServiceProvider.GetRequiredService<IConfiguracionService>();
                JObject jObject = new JObject();
                jObject["adm_cliente_id"] = Constantes.ADM_CLIENTE;
                jObject["tipo_constante"] = "PROOVEDORES";
                jObject["codigo_constante"] = "";

                Mensaje msj = configuracionService.listarConstantes(jObject);

                JObject jObjectList = JObject.Parse("{" + msj.data + "}");
                JArray jsonArray = (JArray)jObjectList["constantes"];

                string correo = "";
                string archivo = "";

                foreach (JObject item in jsonArray)
                {
                    string codigo_constante = (string)item["codigo_constante"];
                    if ("CORREO" == codigo_constante)
                    {
                        correo = (string)item["valor_constante"];
                    }
                    else if ("ARCHIVO" == codigo_constante)
                    {
                        archivo = (string)item["valor_constante"];
                    }

                }


                var accessToken = await GetAccessTokenAsync();

                var graphClient = new GraphServiceClient(
                    new DelegateAuthenticationProvider((requestMessage) =>
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        return Task.CompletedTask;
                    })
                );

                var driveItems = await graphClient.Users[correo].Drive.Root
                          .ItemWithPath(archivo)
                          .Request()
                          .GetAsync();

                if (driveItems != null)
                {
                    var fileId = driveItems.Id;
                    var downloadUrl = driveItems.AdditionalData["@microsoft.graph.downloadUrl"].ToString();

                    // Descargar el archivo
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.GetAsync(downloadUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            // Convertir el contenido del archivo a un arreglo de bytes
                            var fileBytes = await response.Content.ReadAsByteArrayAsync();
                            /*string filePath = @"C:\DATOS\" + driveItems.Name;
                            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                            {
                                await fileStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                            }*/
                            // Devolver el archivo como una respuesta HTTP
                            try
                            {
                                using (MemoryStream memoryStream = new MemoryStream(fileBytes))
                                {
                                    using (var reader = ExcelReaderFactory.CreateReader(memoryStream))
                                    {
                                        var result = new List<Dictionary<string, object>>();

                                        int rowIndex = 0;
                                        while (reader.Read())
                                        {

                                            if (rowIndex > 0)
                                            {
                                                var row = new Dictionary<string, object>();

                                                row["origen"] = reader.GetValue(0).ToString();
                                                row["nombre"] = reader.GetValue(1).ToString();
                                                row["codigo"] = reader.GetValue(2).ToString();
                                                row["annio"] = reader.GetValue(3).ToString();
                                                row["mes"] = reader.GetValue(4).ToString();
                                                row["set_calificacion"] = reader.GetValue(5).ToString();
                                                row["calidad"] = ConsultaUtil.retornaDouble(reader.GetValue(7).ToString(),false);
                                                row["contrato"] = ConsultaUtil.retornaDouble(reader.GetValue(8).ToString(),false);
                                                row["credito"] = ConsultaUtil.retornaDouble(reader.GetValue(9).ToString(),false);
                                                row["on_time"] = ConsultaUtil.retornaDouble(reader.GetValue(10).ToString(),true);
                                                row["in_full"] = ConsultaUtil.retornaDouble(reader.GetValue(11).ToString(),true);
                                                row["calificacion"] = ConsultaUtil.retornaDouble(reader.GetValue(12).ToString(),false);


                                                result.Add(row);
                                            }
                                            rowIndex++;
                                        }

                                        proveedorService.registrarCalificacion(result);
                                    }

                                }
                            }
                            catch (Exception ex)
                            {

                            }
                        }
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
        
        private async Task<string> GetAccessTokenAsync()
        {
            string clientId = "";
            string clientSecret = "";
            string tenantId = "";
            using (var scope = _scopeFactory.CreateScope())
            {
                var configuracionService = scope.ServiceProvider.GetRequiredService<IConfiguracionService>();
                JObject jObject = new JObject();
                jObject["adm_cliente_id"] = Constantes.ADM_CLIENTE;
                jObject["tipo_constante"] = "ONEDRIVE";
                jObject["codigo_constante"] = "";

                Mensaje msj = configuracionService.listarConstantes(jObject);

                JObject jObjectList = JObject.Parse("{" + msj.data + "}");
                JArray jsonArray = (JArray)jObjectList["constantes"];

             

                foreach (JObject item in jsonArray)
                {
                    string codigo_constante = (string)item["codigo_constante"];
                    if ("CLIENT_ID" == codigo_constante)
                    {
                        clientId = (string)item["valor_constante"];
                    }
                    else if ("SECRECT" == codigo_constante)
                    {
                        clientSecret = (string)item["valor_constante"];
                    }
                    else if ("TENANT_ID" == codigo_constante)
                    {
                        tenantId = (string)item["valor_constante"];
                    }
                }
            }
                IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithTenantId(tenantId)
                    .WithClientSecret(clientSecret)
                    .Build();

                string[] scopes = { "https://graph.microsoft.com/.default" };
                var authResult = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();
            
            return authResult.AccessToken;
        }
    }
}
