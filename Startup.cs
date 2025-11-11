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
            services.AddControllers();
            
            // Background job moved to separate Lambda function
            // services.AddHostedService<ProveedorJobs>(); // DISABLED for Lambda
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "esupplier", Version = "v1" });
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
