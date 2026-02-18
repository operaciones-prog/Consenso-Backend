# Deployment Lambda + API Gateway

## Opción Serverless para E-Supplier

Esta configuración permite desplegar la aplicación como funciones Lambda detrás de API Gateway.

### Ventajas
- ✅ Costo ultra bajo ($5-15/mes)
- ✅ Escala automáticamente
- ✅ Sin gestión de servidores
- ✅ Alta disponibilidad por defecto

### Desventajas
- ❌ Cold start ~2-5 segundos
- ❌ Requiere refactorización del código
- ❌ Timeout máximo: 15 minutos

## Refactorización Necesaria

### 1. Instalar Paquetes NuGet

```bash
dotnet add package Amazon.Lambda.AspNetCoreServer.Hosting
dotnet add package Amazon.Lambda.Core
dotnet add package Amazon.Lambda.Serialization.SystemTextJson
```

### 2. Modificar Program.cs

```csharp
using Amazon.Lambda.AspNetCoreServer.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add AWS Lambda support
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// Add services to the container (tu código existente)
// ...

var app = builder.Build();

// Configure the HTTP request pipeline (tu código existente)
// ...

app.Run();
```

### 3. Crear LambdaEntryPoint.cs

```csharp
namespace esupplier;

public class LambdaEntryPoint : Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction
{
    protected override void Init(IWebHostBuilder builder)
    {
        builder.UseStartup<Startup>();
    }
}
```

### 4. Separar Background Job

El background job debe ejecutarse como una función Lambda separada:

**ProveedorJobsLambda.cs:**
```csharp
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace esupplier.Job;

public class ProveedorJobsLambda
{
    public async Task<string> FunctionHandler(ILambdaContext context)
    {
        context.Logger.LogLine("Starting Proveedor Job");
        
        // Tu lógica de DoWorkAsync aquí
        // ...
        
        return "Job completed successfully";
    }
}
```

### 5. Configurar EventBridge

Crear regla en EventBridge:
- Schedule: `cron(5 0 * * ? *)`  (00:05 UTC diario)
- Target: Lambda function (ProveedorJobsLambda)

## Deployment con AWS SAM

### 1. Instalar AWS SAM CLI

```bash
# macOS
brew install aws-sam-cli

# Verificar
sam --version
```

### 2. Crear template.yaml

```yaml
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: E-Supplier Serverless API

Globals:
  Function:
    Timeout: 30
    MemorySize: 512
    Runtime: dotnet8
    Environment:
      Variables:
        ASPNETCORE_ENVIRONMENT: Production

Resources:
  ESupplierApi:
    Type: AWS::Serverless::Function
    Properties:
      Handler: esupplier::esupplier.LambdaEntryPoint::FunctionHandlerAsync
      CodeUri: ./
      Events:
        ApiRoot:
          Type: HttpApi
          Properties:
            Path: /{proxy+}
            Method: ANY
      Policies:
        - AWSLambdaBasicExecutionRole

  ProveedorJob:
    Type: AWS::Serverless::Function
    Properties:
      Handler: esupplier::esupplier.Job.ProveedorJobsLambda::FunctionHandler
      Timeout: 900
      MemorySize: 1024
      Events:
        DailySchedule:
          Type: Schedule
          Properties:
            Schedule: cron(5 0 * * ? *)
      Policies:
        - AWSLambdaBasicExecutionRole

Outputs:
  ApiUrl:
    Description: "API Gateway endpoint URL"
    Value: !Sub "https://${ServerlessHttpApi}.execute-api.${AWS::Region}.amazonaws.com/"
```

### 3. Build y Deploy

```bash
# Build
sam build

# Deploy (primera vez)
sam deploy --guided

# Deploy subsecuentes
sam deploy
```

## Costos Estimados

### Cálculo para 50,000 requests/mes

**Lambda:**
- Requests: 50,000 × $0.20/1M = $0.01
- Compute (512 MB, 500ms avg):
  - 50,000 × 0.5s × 512MB = 12,500 GB-seconds
  - 12,500 × $0.0000166667 = $0.21
- Job diario: 30 × 900s × 1024MB = 27,648 GB-seconds = $0.46
- **Subtotal Lambda:** $0.68

**API Gateway:**
- 50,000 × $3.50/1M = $0.18
- **Subtotal API Gateway:** $0.18

**CloudWatch Logs:**
- ~2 GB logs: $1.00
- **Subtotal Logs:** $1.00

**TOTAL:** ~$2/mes (tráfico muy bajo)
**TOTAL (100K requests):** ~$5/mes
**TOTAL (500K requests):** ~$15/mes

## Limitaciones

1. **Cold Start:** Primera request tarda 2-5 segundos
2. **Timeout:** Máximo 15 minutos (900 segundos)
3. **Payload:** Máximo 6 MB request/response
4. **Concurrent Executions:** Límite de cuenta (default 1000)

## Cuándo Usar Lambda

✅ **Usar Lambda si:**
- Tráfico bajo e impredecible
- Presupuesto muy limitado
- No importan cold starts ocasionales
- Quieres cero mantenimiento

❌ **NO usar Lambda si:**
- Necesitas latencia sub-segundo consistente
- Requests de larga duración (>15 min)
- Procesas archivos muy grandes
- Prefieres arquitectura tradicional

## Alternativa: AWS App Runner (más simple)

Si Lambda parece muy complejo, considera **App Runner**:
- Sin refactorización necesaria
- Deploy directo desde Docker/GitHub
- Auto-scaling incluido
- Costo: $12-20/mes
- Ver `/deployment/docker/` para configuración

