using Amazon.Lambda.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using esupplier.Services.IServices;
using esupplier.Services;
using esupplier.Repositorys.IRepositorys;
using esupplier.Repositorys;
using esupplier.Models.Response;
using esupplier.Utils;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using ExcelDataReader;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

// This assembly attribute defines the Lambda serializer to use
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace esupplier.Job
{
    /// <summary>
    /// Lambda function handler for scheduled background job
    /// This replaces the ProveedorJobs HostedService when running in Lambda
    /// </summary>
    public class ProveedorJobsLambda
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance.
        /// </summary>
        public ProveedorJobsLambda()
        {
            // Setup Dependency Injection container
            var services = new ServiceCollection();
            
            // Add configuration
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .AddEnvironmentVariables();
            
            var configuration = configBuilder.Build();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddHttpContextAccessor();
            
            // Register all services (copied from Startup.cs)
            services.AddScoped<IProveedorService, ProveedorService>();
            services.AddScoped<IProveedorRepository, ProveedorRepository>();
            services.AddScoped<IOCService, OCService>();
            services.AddScoped<IOCRepository, OCRepository>();
            services.AddScoped<IHESService, HESService>();
            services.AddScoped<IHESRepository, HESRepository>();
            services.AddScoped<IComprobanteService, ComprobanteService>();
            services.AddScoped<IComprobanteRepository, ComprobanteRepository>();
            services.AddScoped<IConfiguracionService, ConfiguracionService>();
            services.AddScoped<IConfiguracionRepository, ConfiguracionRepository>();
            services.AddScoped<IUsuarioService, UsuarioService>();
            services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            services.AddScoped<IEmailRepository, EmailRepository>();
            services.AddScoped<IRepositorioService, RepositorioService>();
            services.AddScoped<IRepositorioRepository, RepositorioRepository>();
            services.AddScoped<IHeaderService, HeaderService>();
            
            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Lambda function handler - This is called by AWS Lambda when the function is invoked
        /// EventBridge Schedule will trigger this function daily at 00:05 UTC
        /// </summary>
        /// <param name="input">Input from EventBridge (can be null for scheduled events)</param>
        /// <param name="context">Lambda execution context</param>
        /// <returns>Success message or throws exception on error</returns>
        public async Task<string> FunctionHandler(object input, ILambdaContext context)
        {
            context.Logger.LogLine($"ProveedorJobs Lambda function started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var proveedorService = scope.ServiceProvider.GetRequiredService<IProveedorService>();
                    var configuracionService = scope.ServiceProvider.GetRequiredService<IConfiguracionService>();
                    
                    context.Logger.LogLine("Getting configuration for PROOVEDORES...");
                    
                    // Get configuration for provider file location
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

                    context.Logger.LogLine($"Configuration loaded - Email: {correo}, File: {archivo}");
                    context.Logger.LogLine("Getting OneDrive access token...");

                    var accessToken = await GetAccessTokenAsync(configuracionService, context);

                    context.Logger.LogLine("Connecting to Microsoft Graph...");

                    var graphClient = new GraphServiceClient(
                        new DelegateAuthenticationProvider((requestMessage) =>
                        {
                            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                            return Task.CompletedTask;
                        })
                    );

                    context.Logger.LogLine("Downloading file from OneDrive...");

                    var driveItems = await graphClient.Users[correo].Drive.Root
                              .ItemWithPath(archivo)
                              .Request()
                              .GetAsync();

                    if (driveItems != null)
                    {
                        var fileId = driveItems.Id;
                        var downloadUrl = driveItems.AdditionalData["@microsoft.graph.downloadUrl"].ToString();

                        context.Logger.LogLine($"File found - ID: {fileId}");
                        context.Logger.LogLine("Downloading file content...");

                        using (var httpClient = new HttpClient())
                        {
                            var response = await httpClient.GetAsync(downloadUrl);
                            if (response.IsSuccessStatusCode)
                            {
                                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                                context.Logger.LogLine($"File downloaded - Size: {fileBytes.Length} bytes");

                                try
                                {
                                    context.Logger.LogLine("Processing Excel file...");

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
                                                    row["calidad"] = ConsultaUtil.retornaDouble(reader.GetValue(7).ToString(), false);
                                                    row["contrato"] = ConsultaUtil.retornaDouble(reader.GetValue(8).ToString(), false);
                                                    row["credito"] = ConsultaUtil.retornaDouble(reader.GetValue(9).ToString(), false);
                                                    row["on_time"] = ConsultaUtil.retornaDouble(reader.GetValue(10).ToString(), true);
                                                    row["in_full"] = ConsultaUtil.retornaDouble(reader.GetValue(11).ToString(), true);
                                                    row["calificacion"] = ConsultaUtil.retornaDouble(reader.GetValue(12).ToString(), false);
                                                    result.Add(row);
                                                }
                                                rowIndex++;
                                            }
                                            
                                            context.Logger.LogLine($"Processed {result.Count} rows from Excel");
                                            context.Logger.LogLine("Saving data to database...");

                                            proveedorService.registrarCalificacion(result);

                                            context.Logger.LogLine("Data saved successfully");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    context.Logger.LogLine($"Error processing Excel file: {ex.Message}");
                                    context.Logger.LogLine($"Stack trace: {ex.StackTrace}");
                                    throw;
                                }
                            }
                            else
                            {
                                context.Logger.LogLine($"Failed to download file - Status: {response.StatusCode}");
                                throw new Exception($"Failed to download file from OneDrive - Status: {response.StatusCode}");
                            }
                        }
                    }
                    else
                    {
                        context.Logger.LogLine("File not found in OneDrive");
                        throw new Exception("File not found in OneDrive");
                    }
                }
                
                context.Logger.LogLine($"ProveedorJobs Lambda function completed successfully at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                return "Job completed successfully";
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"ERROR in ProveedorJobs Lambda: {ex.Message}");
                context.Logger.LogLine($"Stack trace: {ex.StackTrace}");
                
                // Re-throw the exception so Lambda marks the execution as failed
                throw;
            }
        }
        
        /// <summary>
        /// Gets OneDrive access token using credentials from configuration
        /// </summary>
        private async Task<string> GetAccessTokenAsync(IConfiguracionService configuracionService, ILambdaContext context)
        {
            context.Logger.LogLine("Retrieving OneDrive credentials from configuration...");

            JObject jObject = new JObject();
            jObject["adm_cliente_id"] = Constantes.ADM_CLIENTE;
            jObject["tipo_constante"] = "ONEDRIVE";
            jObject["codigo_constante"] = "";

            Mensaje msj = configuracionService.listarConstantes(jObject);
            JObject jObjectList = JObject.Parse("{" + msj.data + "}");
            JArray jsonArray = (JArray)jObjectList["constantes"];

            string clientId = "";
            string clientSecret = "";
            string tenantId = "";

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

            context.Logger.LogLine("Building confidential client application...");

            IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithTenantId(tenantId)
                .WithClientSecret(clientSecret)
                .Build();

            context.Logger.LogLine("Acquiring access token...");

            string[] scopes = { "https://graph.microsoft.com/.default" };
            var authResult = await confidentialClientApplication.AcquireTokenForClient(scopes).ExecuteAsync();
            
            context.Logger.LogLine("Access token acquired successfully");
            
            return authResult.AccessToken;
        }
    }
}

