# Estimación de Refactorización a Lambda

## 📊 Análisis del Proyecto Actual

### Estructura Detectada
- **Controllers:** 7 controladores
- **Endpoints:** ~40 endpoints API
- **Líneas de código:** ~2,627 líneas (Controllers + Services + Jobs)
- **Background Job:** 1 tarea programada (ProveedorJobs)
- **Arquitectura:** ASP.NET Core tradicional con DI

### Dependencias Especiales
- ✅ Entity Framework (SQL Server externo)
- ✅ Microsoft Graph API (OneDrive)
- ✅ HttpClient para APIs externas (SAP HANA)
- ✅ ExcelDataReader
- ✅ Newtonsoft.Json

---

## ⏱️ ESTIMACIÓN DE TIEMPO TOTAL

### **3-4 horas para desarrollador con experiencia en Lambda**
### **5-6 horas para desarrollador nuevo en Lambda**

---

## 📋 DESGLOSE DETALLADO DE TAREAS

### 1️⃣ Instalación de Paquetes NuGet (5 minutos)

**Tarea:** Agregar paquetes Lambda a tu proyecto

```bash
dotnet add package Amazon.Lambda.AspNetCoreServer.Hosting
dotnet add package Amazon.Lambda.Core
dotnet add package Amazon.Lambda.Serialization.SystemTextJson
dotnet add package Amazon.Lambda.APIGatewayEvents
```

**Complejidad:** ⭐ Trivial  
**Tiempo:** 5 minutos  
**Riesgo:** Ninguno

---

### 2️⃣ Modificar Program.cs (10 minutos)

**Tarea:** Cambiar host builder para soportar Lambda

**Antes (actual):**
```csharp
namespace esupplier
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
```

**Después (Lambda compatible):**
```csharp
using Amazon.Lambda.AspNetCoreServer.Hosting;

namespace esupplier
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureServices(services =>
                {
                    services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
                });
    }
}
```

**Complejidad:** ⭐ Trivial  
**Tiempo:** 10 minutos  
**Riesgo:** Bajo  
**Cambios:** 1 archivo

---

### 3️⃣ Crear LambdaEntryPoint.cs (15 minutos)

**Tarea:** Crear punto de entrada para Lambda

**Archivo nuevo:** `LambdaEntryPoint.cs`

```csharp
using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Hosting;

namespace esupplier
{
    /// <summary>
    /// Lambda entry point for API Gateway HTTP API
    /// </summary>
    public class LambdaEntryPoint : APIGatewayHttpApiV2ProxyFunction
    {
        /// <summary>
        /// Initialize the ASP.NET Core application
        /// </summary>
        /// <param name="builder"></param>
        protected override void Init(IWebHostBuilder builder)
        {
            builder
                .UseStartup<Startup>();
        }

        /// <summary>
        /// Use this override to customize the services registered with the IHostBuilder. 
        /// </summary>
        /// <param name="builder"></param>
        protected override void Init(IHostBuilder builder)
        {
            // Configure services here if needed
        }
    }
}
```

**Complejidad:** ⭐⭐ Fácil  
**Tiempo:** 15 minutos  
**Riesgo:** Bajo  
**Cambios:** 1 archivo nuevo

---

### 4️⃣ Modificar Startup.cs (15 minutos)

**Tarea:** Asegurar compatibilidad con Lambda

**Cambios necesarios en Startup.cs:**

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCors(options => { /* ... tu código actual ... */ });
    services.AddHttpContextAccessor();
    
    // Tus servicios existentes
    services.AddScoped<IProveedorService, ProveedorService>();
    // ... resto de servicios ...
    
    services.AddControllers();
    
    // REMOVER ESTO para Lambda (no aplica background jobs aquí)
    // services.AddHostedService<ProveedorJobs>(); ❌ QUITAR
    
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "esupplier", Version = "v1" });
    });
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // Tu código actual está bien para Lambda
    // NO necesitas cambios aquí
}
```

**Complejidad:** ⭐ Trivial  
**Tiempo:** 15 minutos  
**Riesgo:** Bajo  
**Cambios:** 1 archivo (comentar 1 línea)

---

### 5️⃣ Separar Background Job a Lambda Independiente (30-45 minutos)

**Tarea:** Convertir `ProveedorJobs` a función Lambda separada

**Archivo nuevo:** `Job/ProveedorJobsLambda.cs`

```csharp
using Amazon.Lambda.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using esupplier.Services.IServices;
using esupplier.Repositorys.IRepositorys;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace esupplier.Job
{
    public class ProveedorJobsLambda
    {
        private readonly IServiceProvider _serviceProvider;

        public ProveedorJobsLambda()
        {
            // Setup DI container
            var services = new ServiceCollection();
            
            // Add configuration
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables();
            
            var configuration = configBuilder.Build();
            services.AddSingleton<IConfiguration>(configuration);
            
            // Register your services (copy from Startup.cs)
            services.AddScoped<IProveedorService, ProveedorService>();
            services.AddScoped<IProveedorRepository, ProveedorRepository>();
            services.AddScoped<IConfiguracionService, ConfiguracionService>();
            services.AddScoped<IConfiguracionRepository, ConfiguracionRepository>();
            services.AddScoped<IEmailRepository, EmailRepository>();
            
            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Lambda handler for scheduled job
        /// </summary>
        public async Task<string> FunctionHandler(ILambdaContext context)
        {
            context.Logger.LogLine("Starting Proveedor Job...");
            
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var proveedorService = scope.ServiceProvider.GetRequiredService<IProveedorService>();
                    var configuracionService = scope.ServiceProvider.GetRequiredService<IConfiguracionService>();
                    
                    // Copiar toda la lógica de DoWorkAsync() aquí
                    // Tu código actual de ProveedorJobs.DoWorkAsync()
                    // ...
                    
                    context.Logger.LogLine("Job completed successfully");
                    return "Success";
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"Error: {ex.Message}");
                throw;
            }
        }
    }
}
```

**Complejidad:** ⭐⭐⭐ Media  
**Tiempo:** 30-45 minutos  
**Riesgo:** Medio (requiere copiar lógica de DoWorkAsync)  
**Cambios:** 1 archivo nuevo

---

### 6️⃣ Crear template.yaml para SAM (20 minutos)

**Tarea:** Configurar infraestructura como código

**Archivo nuevo:** `template.yaml` (raíz del proyecto)

```yaml
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: E-Supplier Serverless API

Globals:
  Function:
    Timeout: 30
    MemorySize: 512
    Runtime: dotnet8
    Architectures:
      - x86_64
    Environment:
      Variables:
        ASPNETCORE_ENVIRONMENT: Production

Resources:
  # API Lambda Function
  ESupplierApi:
    Type: AWS::Serverless::Function
    Properties:
      Handler: esupplier::esupplier.LambdaEntryPoint::FunctionHandlerAsync
      CodeUri: ./
      Description: E-Supplier REST API
      MemorySize: 512
      Timeout: 30
      Events:
        ApiRoot:
          Type: HttpApi
          Properties:
            Path: /{proxy+}
            Method: ANY
      Policies:
        - AWSLambdaBasicExecutionRole

  # Background Job Lambda Function
  ProveedorJobFunction:
    Type: AWS::Serverless::Function
    Properties:
      Handler: esupplier::esupplier.Job.ProveedorJobsLambda::FunctionHandler
      CodeUri: ./
      Description: Proveedor scheduled job
      MemorySize: 1024
      Timeout: 900
      Events:
        DailySchedule:
          Type: Schedule
          Properties:
            Schedule: cron(5 0 * * ? *)
            Description: Daily job at 00:05 UTC
            Enabled: true
      Policies:
        - AWSLambdaBasicExecutionRole

Outputs:
  ApiUrl:
    Description: "API Gateway endpoint URL"
    Value: !Sub "https://${ServerlessHttpApi}.execute-api.${AWS::Region}.amazonaws.com/"
  
  ApiId:
    Description: "API Gateway ID"
    Value: !Ref ServerlessHttpApi
```

**Complejidad:** ⭐⭐ Fácil  
**Tiempo:** 20 minutos  
**Riesgo:** Bajo  
**Cambios:** 1 archivo nuevo

---

### 7️⃣ Ajustar esupplier.csproj (10 minutos)

**Tarea:** Asegurar que el proyecto compile para Lambda

**Agregar al .csproj:**

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
  <AWSProjectType>Lambda</AWSProjectType>
  <!-- Esto es importante para Lambda -->
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

**Complejidad:** ⭐ Trivial  
**Tiempo:** 10 minutos  
**Riesgo:** Bajo  
**Cambios:** 1 archivo

---

### 8️⃣ Testing Local con SAM (30 minutos)

**Tarea:** Probar Lambda localmente antes de deploy

```bash
# Instalar AWS SAM CLI (una sola vez)
brew install aws-sam-cli  # macOS
# o
choco install aws-sam-cli  # Windows

# Build del proyecto
sam build

# Test local de la API
sam local start-api

# Test en http://localhost:3000/swagger

# Test del job
sam local invoke ProveedorJobFunction
```

**Complejidad:** ⭐⭐ Fácil  
**Tiempo:** 30 minutos  
**Riesgo:** Medio (puede haber errores de configuración)  
**Cambios:** Ninguno (solo testing)

---

### 9️⃣ Deploy a AWS (30 minutos)

**Tarea:** Desplegar a producción

```bash
# Primera vez (wizard interactivo)
sam deploy --guided

# Responder preguntas:
# - Stack Name: esupplier-prod
# - AWS Region: us-east-1
# - Confirm changes: Y
# - Allow SAM CLI IAM role creation: Y
# - ESupplierApi may not have authorization: Y
# - Save arguments to config: Y

# Deploys subsecuentes (más rápido)
sam build && sam deploy
```

**Complejidad:** ⭐⭐ Fácil  
**Tiempo:** 30 minutos (primer deploy)  
**Riesgo:** Medio (permisos IAM, primera vez)  
**Cambios:** Deploy en AWS

---

### 🔟 Configurar Variables de Entorno (15 minutos)

**Tarea:** Configurar connection strings y secrets

**En AWS Console → Lambda → Configuration → Environment variables:**

```
ConnectionStrings__esupplier = Server=34.46.87.43;...
Http__Consultahesmigo = https://...
Http__Autorizacion = Basic ...
ExecutionTimesJob__0 = 00:05
```

**O mejor aún, usar AWS Secrets Manager:**

```bash
# Crear secret
aws secretsmanager create-secret \
    --name esupplier/prod/config \
    --secret-string file://secrets.json

# Actualizar template.yaml para usar el secret
```

**Complejidad:** ⭐⭐ Fácil  
**Tiempo:** 15 minutos  
**Riesgo:** Bajo  
**Cambios:** Configuración en AWS

---

## 📊 RESUMEN DE TIEMPO

| Tarea | Tiempo | Complejidad |
|-------|--------|-------------|
| 1. Instalar paquetes NuGet | 5 min | ⭐ |
| 2. Modificar Program.cs | 10 min | ⭐ |
| 3. Crear LambdaEntryPoint.cs | 15 min | ⭐⭐ |
| 4. Modificar Startup.cs | 15 min | ⭐ |
| 5. Separar Background Job | 30-45 min | ⭐⭐⭐ |
| 6. Crear template.yaml | 20 min | ⭐⭐ |
| 7. Ajustar .csproj | 10 min | ⭐ |
| 8. Testing local | 30 min | ⭐⭐ |
| 9. Deploy a AWS | 30 min | ⭐⭐ |
| 10. Configurar variables | 15 min | ⭐⭐ |
| **TOTAL** | **3-3.5 horas** | |

### Tiempo adicional para:
- **Debugging y ajustes:** +30-60 min
- **Documentación:** +15-30 min
- **Testing exhaustivo en AWS:** +30-60 min

### **TOTAL REALISTA: 3.5-5 horas**

---

## 🎯 FACTORES QUE AFECTAN EL TIEMPO

### ✅ Reducen el tiempo:
- ✅ Ya tienes experiencia con Lambda
- ✅ Ya usas AWS CLI y SAM
- ✅ Código bien organizado (tu caso)
- ✅ Tests unitarios existentes
- ✅ Arquitectura limpia con DI (tu caso)

### ⚠️ Aumentan el tiempo:
- ⚠️ Primera vez usando Lambda: +1-2 horas
- ⚠️ Problemas con IAM permissions: +30-60 min
- ⚠️ Dependencias incompatibles: +1-2 horas (poco probable)
- ⚠️ Cold start optimization necesaria: +1-2 horas
- ⚠️ Errores de runtime en producción: +1-3 horas

---

## 💡 COMPLEJIDAD POR ÁREA

### Muy Fácil (Total: 40 minutos)
- ✅ Instalar paquetes
- ✅ Modificar Program.cs
- ✅ Modificar Startup.cs
- ✅ Ajustar .csproj

### Fácil (Total: 1 hora)
- ✅ Crear LambdaEntryPoint.cs
- ✅ Crear template.yaml
- ✅ Configurar variables

### Media (Total: 1.5 horas)
- ⚠️ Separar background job
- ⚠️ Testing local
- ⚠️ Deploy inicial

---

## 🚧 RIESGOS Y MITIGACIONES

### Riesgo 1: Cold Start Lento
**Problema:** Primera request tarda 3-5 segundos  
**Mitigación:**
```yaml
# Provisioned Concurrency (evita cold starts)
ProvisionedConcurrencyConfig:
  ProvisionedConcurrentExecutions: 1  # +$12/mes
```

### Riesgo 2: Timeout en Requests Largas
**Problema:** Request > 30 segundos falla  
**Mitigación:**
- Optimizar queries SQL
- Aumentar timeout a 60 segundos (máximo en API Gateway)
- Para jobs largos, ya tienes 900 segundos

### Riesgo 3: Tamaño del Package
**Problema:** Lambda tiene límite de 250 MB unzipped  
**Mitigación:**
- Tu proyecto es pequeño (~50 MB estimado) ✅
- Eliminar dependencias no usadas
- Usar Lambda Layers para librerías comunes

### Riesgo 4: SQL Server Connections
**Problema:** Demasiadas conexiones abiertas  
**Mitigación:**
- Lambda reutiliza conexiones entre invocaciones ✅
- Usar connection pooling (ya lo tienes con EF) ✅
- Ajustar MaxPoolSize en connection string

---

## 💰 COSTO ADICIONAL VS BENEFICIO

### Inversión de Tiempo
- **Desarrollo:** 3.5-5 horas @ $50/hora = $175-250
- **Testing:** 1-2 horas @ $50/hora = $50-100
- **Total inversión:** $225-350

### Ahorro Mensual
- **Lightsail:** $5/mes
- **Lambda:** $1-3/mes
- **Ahorro:** $2-4/mes ($24-48/año)

### Break-even
- **Tiempo:** 5-7 años 😱

### ⚠️ CONCLUSIÓN: NO VALE LA PENA solo por ahorro
Refactorizar a Lambda **NO tiene sentido** si ya vas con Lightsail ($5/mes).

Lambda vale la pena si:
- ✅ Esperas crecimiento exponencial de tráfico
- ✅ Quieres aprender Lambda (valor educativo)
- ✅ Necesitas auto-scaling por requisito
- ✅ Tráfico muy variable (99% del tiempo sin uso)

---

## 📊 COMPARACIÓN FINAL

| Métrica | Lightsail | Lambda |
|---------|-----------|--------|
| **Costo/mes** | $5 | $1-3 |
| **Tiempo de setup** | 1-2h | 3.5-5h |
| **Refactorización** | No | Sí |
| **Mantenimiento** | Medio | Bajo |
| **Complejidad** | ⭐⭐ | ⭐⭐⭐⭐ |
| **Cold start** | No | Sí (2-5s) |
| **Debugging** | Fácil | Medio |
| **Escalabilidad** | Manual | Automática |

---

## 🎯 RECOMENDACIÓN FINAL

### Para tu caso específico:

**EMPIEZA CON LIGHTSAIL ($5/mes)**

**Razones:**
1. ✅ Ahorro de tiempo: 1-2h vs 3.5-5h
2. ✅ Sin refactorización necesaria
3. ✅ Debugging más simple
4. ✅ Diferencia de costo mínima ($2-4/mes)
5. ✅ Break-even de refactorización: 5-7 años

**Considera Lambda SOLO si:**
- 🎓 Quieres aprender Lambda (valor educativo)
- 📈 Tráfico muy variable (0 requests por horas, luego picos)
- 🚀 Necesitas escalar a millones de requests
- 💼 Proyecto personal/experimental

### Si decides ir con Lambda de todos modos:

**Plan de acción:**
1. **Día 1 (Sábado):** 
   - 09:00-12:00: Refactorización (tareas 1-7)
   - 14:00-16:00: Testing local (tarea 8)

2. **Día 2 (Domingo):**
   - 10:00-12:00: Deploy y configuración (tareas 9-10)
   - 14:00-16:00: Testing en producción y ajustes

**Total:** 1 fin de semana

---

## 📝 CHECKLIST DE REFACTORIZACIÓN

Si decides proceder con Lambda:

### Pre-refactorización
- [ ] Instalar AWS CLI
- [ ] Instalar SAM CLI
- [ ] Configurar credenciales AWS
- [ ] Crear branch Git: `feature/lambda-migration`
- [ ] Hacer backup del código actual

### Refactorización
- [ ] Instalar paquetes NuGet Lambda
- [ ] Modificar Program.cs
- [ ] Crear LambdaEntryPoint.cs
- [ ] Modificar Startup.cs (comentar HostedService)
- [ ] Crear ProveedorJobsLambda.cs
- [ ] Crear template.yaml
- [ ] Ajustar .csproj
- [ ] Compilar sin errores

### Testing
- [ ] `sam build` exitoso
- [ ] `sam local start-api` funciona
- [ ] Probar endpoints principales
- [ ] `sam local invoke ProveedorJobFunction` funciona
- [ ] Logs sin errores

### Deploy
- [ ] `sam deploy --guided`
- [ ] Configurar variables de entorno
- [ ] Configurar EventBridge para job
- [ ] Probar API en AWS
- [ ] Verificar CloudWatch logs
- [ ] Testing exhaustivo

### Post-deploy
- [ ] Monitorear costos (3-7 días)
- [ ] Verificar cold starts aceptables
- [ ] Documentar cambios
- [ ] Actualizar README
- [ ] Merge a main

---

## 📞 ¿Necesitas Ayuda?

Si decides refactorizar a Lambda y necesitas ayuda con algún paso específico, tengo la documentación lista para guiarte paso a paso.

**Archivos de referencia creados:**
- `/deployment/lambda/README.md` - Guía completa de Lambda
- Este documento - Estimación detallada

**Siguiente paso:** Decidir entre Lightsail (recomendado) o Lambda (experimental)

