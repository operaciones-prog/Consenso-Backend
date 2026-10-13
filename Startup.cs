using esupplier.Config;
using esupplier.Job;
using esupplier.Repositorys;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services;
using esupplier.Services.IServices;
using System.Text;
using esupplier.Middleware;
using esupplier.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace esupplier
{
    public class Startup
    {
        readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
        public Startup(IConfiguration configuration)
        {
            // Necesario para que ExcelDataReader lea archivos .xls en .NET 8
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Orígenes permitidos por entorno (Cors:Origins). Si no se configuran, se usan los históricos.
            string[] origenes = Configuration.GetSection("Cors:Origins").Get<string[]>();
            if (origenes == null || origenes.Length == 0)
            {
                origenes = new[]
                {
                    "http://localhost:4200",
                    "https://consenso-ec-qas.netlify.app",
                    "https://concenso-ec-dev.netlify.app",
                    "https://consenso-web.vercel.app",
                    "https://consenso.esupplier.biz",
                    "https://consenso.esupplier.net"
                };
            }
            services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins,
                    builder => builder.WithOrigins(origenes)
                .AllowAnyHeader()
                .AllowAnyMethod());
            });
            services.AddHttpContextAccessor();
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

            // Cliente de Microsoft Graph compartido (credenciales y token cacheados)
            services.AddSingleton<IGraphClientProvider, GraphClientProvider>();

            // Cliente HTTP para SAP CPI con timeout menor al de API Gateway (30 s)
            services.AddHttpClient(Constantes.HTTP_CLIENT_SAP, client =>
            {
                int timeout = Configuration.GetValue<int?>("Http:TimeoutSeconds") ?? 25;
                client.Timeout = TimeSpan.FromSeconds(timeout);
                string autorizacion = Configuration["Http:Autorizacion"];
                if (!string.IsNullOrWhiteSpace(autorizacion))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", autorizacion);
                }
            });

            // ===== CONFIGURACIÓN DE ALMACENAMIENTO (OneDrive/S3) =====
            services.Configure<StorageSettings>(Configuration.GetSection("Storage"));

            // Registrar servicios de almacenamiento
            services.AddScoped<OneDriveStorageService>();
            services.AddScoped<S3StorageService>();

            // Registrar factory para selección dinámica
            services.AddScoped<FileStorageFactory>();

            // Registrar servicio por defecto según configuración
            var storageProvider = Configuration.GetSection("Storage:Provider").Value?.ToUpper() ?? "ONEDRIVE";
            if (storageProvider == "S3")
            {
                services.AddScoped<IFileStorageService>(provider => provider.GetRequiredService<S3StorageService>());
            }
            else
            {
                services.AddScoped<IFileStorageService>(provider => provider.GetRequiredService<OneDriveStorageService>());
            }
            // ===== FIN CONFIGURACIÓN DE ALMACENAMIENTO =====

            services.AddControllers();

            
            services.AddSwaggerGen(c =>
            {
                // Configuración básica del documento Swagger
                c.SwaggerDoc("v1", new OpenApiInfo 
                { 
                    Title = "Consenso API", 
                    Version = "v1",
                    Description = "API para el sistema de gestión de consenso",
                    Contact = new OpenApiContact
                    {
                        Name = "Consenso Team",
                        Email = "support@consenso.com"
                    }
                });

                // Incluir XML comments para documentación
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

                // Configurar respuestas detalladas para endpoints
                c.EnableAnnotations();
                
                // Configuración de schemas personalizados
                c.CustomSchemaIds(type => type.FullName);
                
                // Ignorar propiedades nulas en los schemas
                c.UseAllOfToExtendReferenceSchemas();
                c.SupportNonNullableReferenceTypes();
                
                // Configuración de seguridad para JWT (si aplica)
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
            // ===== AUTENTICACIÓN JWT =====
            // Auth:Enabled=false (por defecto) mantiene la API abierta mientras el frontend se adapta.
            // Con Auth:Enabled=true todos los endpoints exigen token, salvo los marcados [AllowAnonymous].
            bool authHabilitada = Configuration.GetValue<bool>("Auth:Enabled");
            string jwtKey = Configuration["Jwt:Key"];
            if (authHabilitada && (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32))
            {
                throw new InvalidOperationException("Auth:Enabled=true requiere Jwt:Key con al menos 32 caracteres.");
            }
            byte[] claveFirma = string.IsNullOrWhiteSpace(jwtKey)
                ? System.Security.Cryptography.RandomNumberGenerator.GetBytes(64) // sin clave configurada: ningún token externo es válido
                : Encoding.UTF8.GetBytes(jwtKey);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Configuration["Jwt:Issuer"] ?? "consenso-api",
                        ValidAudience = Configuration["Jwt:Audience"] ?? "consenso-web",
                        IssuerSigningKey = new SymmetricSecurityKey(claveFirma),
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });

            services.AddAuthorization(options =>
            {
                if (authHabilitada)
                {
                    options.FallbackPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                }
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<ExcepcionGlobalMiddleware>();

            app.UseHttpsRedirection();

            app.UseCors(MyAllowSpecificOrigins);

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            // Swagger solo en Development o si se habilita explícitamente (Swagger:Enabled=true)
            if (env.IsDevelopment() || Configuration.GetValue<bool>("Swagger:Enabled"))
            {
                app.UseSwagger();

                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "esupplier v1");
                });
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
