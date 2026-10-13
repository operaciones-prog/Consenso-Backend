using Amazon.Lambda.Core;
using esupplier.Models.Response;
using esupplier.Repositorys;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services;
using esupplier.Services.IServices;
using esupplier.Utils;
using ExcelDataReader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;

// This assembly attribute defines the Lambda serializer to use
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace esupplier.Job
{
    /// <summary>
    /// Lambda programada (EventBridge) que carga la calificación de proveedores desde un Excel en OneDrive.
    /// Reemplaza al antiguo HostedService ProveedorJobs.
    /// </summary>
    public class ProveedorJobsLambda
    {
        // Columnas leídas del Excel (0..12); la 6 no se usa.
        private const int UltimaColumna = 12;

        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance.
        /// </summary>
        public ProveedorJobsLambda()
        {
            // Necesario para que ExcelDataReader lea archivos .xls en .NET 8 (Startup no se ejecuta aquí).
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var services = new ServiceCollection();

            // Mismo criterio que la API: el entorno se toma de ASPNETCORE_ENVIRONMENT (QAS, Production...).
            string entorno = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{entorno}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(builder => builder.AddConsole());
            services.AddHttpContextAccessor();

            // Mismo cliente SAP que en Startup (los repositorios registrados lo requieren).
            services.AddHttpClient(Constantes.HTTP_CLIENT_SAP, client =>
            {
                int timeout = configuration.GetValue<int?>("Http:TimeoutSeconds") ?? 25;
                client.Timeout = TimeSpan.FromSeconds(timeout);
                string autorizacion = configuration["Http:Autorizacion"];
                if (!string.IsNullOrWhiteSpace(autorizacion))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", autorizacion);
                }
            });

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
            services.AddSingleton<IGraphClientProvider, GraphClientProvider>();

            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Lambda function handler - EventBridge Schedule lo invoca diariamente.
        /// Lanza excepción ante cualquier fallo para que Lambda marque la ejecución como fallida.
        /// </summary>
        public async Task<string> FunctionHandler(object input, ILambdaContext context)
        {
            context.Logger.LogLine($"ProveedorJobs Lambda function started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var proveedorService = scope.ServiceProvider.GetRequiredService<IProveedorService>();
                    var configuracionService = scope.ServiceProvider.GetRequiredService<IConfiguracionService>();
                    var graphProvider = scope.ServiceProvider.GetRequiredService<IGraphClientProvider>();

                    context.Logger.LogLine("Getting configuration for PROOVEDORES...");

                    JObject jObject = new JObject
                    {
                        ["adm_cliente_id"] = Constantes.ADM_CLIENTE,
                        ["tipo_constante"] = "PROOVEDORES",
                        ["codigo_constante"] = ""
                    };

                    Mensaje msj = configuracionService.listarConstantes(jObject);
                    if (msj == null || msj.tipo != "S" || string.IsNullOrEmpty(msj.data))
                    {
                        throw new InvalidOperationException("No se pudo leer la configuración PROOVEDORES: " + msj?.mensaje);
                    }

                    JArray jsonArray = JObject.Parse("{" + msj.data + "}")["constantes"] as JArray ?? new JArray();

                    string correo = "";
                    string archivo = "";
                    foreach (JToken item in jsonArray)
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

                    if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(archivo))
                    {
                        throw new InvalidOperationException("Faltan las constantes PROOVEDORES/CORREO o PROOVEDORES/ARCHIVO.");
                    }

                    context.Logger.LogLine($"Configuration loaded - Email: {correo}, File: {archivo}");
                    context.Logger.LogLine("Downloading file from OneDrive...");

                    var graphClient = graphProvider.Cliente;
                    var driveItem = await graphClient.Users[correo].Drive.Root
                              .ItemWithPath(archivo)
                              .Request()
                              .GetAsync();

                    if (driveItem == null)
                    {
                        throw new InvalidOperationException("File not found in OneDrive");
                    }

                    context.Logger.LogLine($"File found - ID: {driveItem.Id}");

                    byte[] fileBytes;
                    using (var contenido = await graphClient.Users[correo].Drive.Items[driveItem.Id].Content.Request().GetAsync())
                    using (var memoria = new MemoryStream())
                    {
                        await contenido.CopyToAsync(memoria);
                        fileBytes = memoria.ToArray();
                    }
                    context.Logger.LogLine($"File downloaded - Size: {fileBytes.Length} bytes");

                    List<Dictionary<string, object>> result = LeerCalificaciones(fileBytes);
                    context.Logger.LogLine($"Processed {result.Count} rows from Excel");

                    if (result.Count == 0)
                    {
                        // Evita borrar la calificación vigente con un archivo vacío.
                        throw new InvalidOperationException("El archivo de calificación no contiene filas; no se actualizó la base de datos.");
                    }

                    context.Logger.LogLine("Saving data to database...");
                    Mensaje resultado = proveedorService.registrarCalificacion(result);
                    if (resultado == null || resultado.tipo != "S")
                    {
                        throw new InvalidOperationException("No se pudo registrar la calificación de proveedores: " + resultado?.mensaje);
                    }

                    context.Logger.LogLine("Data saved successfully");
                }

                context.Logger.LogLine($"ProveedorJobs Lambda function completed successfully at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                return "Job completed successfully";
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"ERROR in ProveedorJobs Lambda: {ex}");

                // Re-throw the exception so Lambda marks the execution as failed
                throw;
            }
        }

        /// <summary>Lee el Excel de calificación (la primera fila es encabezado; las filas vacías se omiten).</summary>
        private static List<Dictionary<string, object>> LeerCalificaciones(byte[] fileBytes)
        {
            var result = new List<Dictionary<string, object>>();

            using (MemoryStream memoryStream = new MemoryStream(fileBytes))
            using (var reader = ExcelReaderFactory.CreateReader(memoryStream))
            {
                int rowIndex = 0;
                while (reader.Read())
                {
                    if (rowIndex++ == 0)
                    {
                        continue;
                    }

                    var celdas = new string[UltimaColumna + 1];
                    bool vacia = true;
                    for (int i = 0; i <= UltimaColumna; i++)
                    {
                        celdas[i] = Celda(reader, i);
                        if (celdas[i].Length > 0) vacia = false;
                    }
                    if (vacia)
                    {
                        continue;
                    }

                    var row = new Dictionary<string, object>
                    {
                        ["origen"] = celdas[0],
                        ["nombre"] = celdas[1],
                        ["codigo"] = celdas[2],
                        ["annio"] = celdas[3],
                        ["mes"] = celdas[4],
                        ["set_calificacion"] = celdas[5],
                        ["calidad"] = ConsultaUtil.retornaDouble(celdas[7], false),
                        ["contrato"] = ConsultaUtil.retornaDouble(celdas[8], false),
                        ["credito"] = ConsultaUtil.retornaDouble(celdas[9], false),
                        ["on_time"] = ConsultaUtil.retornaDouble(celdas[10], true),
                        ["in_full"] = ConsultaUtil.retornaDouble(celdas[11], true),
                        ["calificacion"] = ConsultaUtil.retornaDouble(celdas[12], false)
                    };
                    result.Add(row);
                }
            }

            return result;
        }

        /// <summary>Valor de la celda como texto (cultura invariante para números); "" si no existe o es nula.</summary>
        private static string Celda(IExcelDataReader reader, int indice)
        {
            if (indice >= reader.FieldCount) return "";
            object valor = reader.GetValue(indice);
            return (Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "").Trim();
        }
    }
}
