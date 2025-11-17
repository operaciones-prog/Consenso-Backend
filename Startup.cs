using esupplier.Config;
using esupplier.Job;
using esupplier.Repositorys;
using esupplier.Repositorys.IRepositorys;
using esupplier.Services;
using esupplier.Services.IServices;
using Microsoft.OpenApi.Models;

namespace esupplier
{
    public class Startup
    {
        readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins,
                    builder => builder.WithOrigins("http://localhost:4200",
                    "https://consenso-ec-qas.netlify.app",
                    "https://concenso-ec-dev.netlify.app",
                    "https://consenso-web.vercel.app",
                    "https://consenso.esupplier.biz")
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

            // Background job moved to separate Lambda function
            // services.AddHostedService<ProveedorJobs>(); // DISABLED for Lambda
            
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
           /* string secretKey = Configuration["Jwt:Key"];

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = Configuration["Jwt:Issuer"],
                ValidAudience = Configuration["Jwt:Issuer"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = tokenValidationParameters;
                });

            services.AddAuthorization();*/
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseCors(MyAllowSpecificOrigins);

            app.UseRouting();

            //app.UseAuthentication();

            app.UseAuthorization();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "esupplier v1");
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
