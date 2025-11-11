# 📦 Instrucciones de Deployment a Lambda - Consola AWS

## ✅ Estado del Código

**Todo el código ya está listo y compila correctamente:**
- ✅ Paquetes Lambda instalados
- ✅ Program.cs modificado
- ✅ Startup.cs modificado
- ✅ LambdaEntryPoint.cs creado
- ✅ ProveedorJobsLambda.cs creado
- ✅ Compilación exitosa
- ✅ Tamaño: 30 MB descomprimido (~12-15 MB comprimido en ZIP)
- ✅ **Dentro del límite de Lambda** (250 MB descomprimido)

---

## 📋 RESUMEN DE LO QUE VAS A CREAR

Crearás **2 funciones Lambda**:

1. **esupplier-api** - Tu API REST completa (todos los endpoints)
2. **esupplier-job** - Background job que ejecuta diariamente a las 00:05

---

## 🚀 PASO 1: PREPARAR EL PACKAGE

### 1.1 Compilar el proyecto

```bash
cd /Users/wilbert/Documents/Freelance/Consenso/Consenso-Backend

# Compilar para producción
dotnet publish -c Release -o publish
```

### 1.2 Crear archivo ZIP

**En Mac:**
```bash
cd publish
zip -r ../esupplier-lambda.zip .
cd ..
```

**Resultado:** Tendrás un archivo `esupplier-lambda.zip` (~12-15 MB)

---

## 🌐 PASO 2: CREAR LAMBDA PARA LA API

### 2.1 Ir a AWS Lambda Console

1. Abre https://console.aws.amazon.com/lambda/
2. Asegúrate de estar en la región **us-east-1** (Virginia)
3. Click en **"Create function"**

### 2.2 Configuración básica

- **Function name:** `esupplier-api`
- **Runtime:** **.NET 8 (C#/PowerShell)**
- **Architecture:** **x86_64**
- **Execution role:** 
  - **Create a new role with basic Lambda permissions** (primera vez)
  - O selecciona un rol existente que tenga permisos básicos

- Click **"Create function"**

### 2.3 Subir el código

1. En la sección **"Code source"**
2. Click en **"Upload from"** → **".zip file"**
3. Selecciona tu archivo `esupplier-lambda.zip`
4. Click **"Save"**
5. Espera a que termine de subir (~1-2 minutos)

### 2.4 Configurar el Handler

1. Scroll down a **"Runtime settings"**
2. Click **"Edit"**
3. **Handler:** `esupplier::esupplier.LambdaEntryPoint::FunctionHandlerAsync`
4. Click **"Save"**

### 2.5 Configurar Memoria y Timeout

1. Tab **"Configuration"** → **"General configuration"**
2. Click **"Edit"**
3. **Memory:** 512 MB
4. **Timeout:** 30 seconds
5. Click **"Save"**

### 2.6 Configurar Variables de Entorno

1. Tab **"Configuration"** → **"Environment variables"**
2. Click **"Edit"** → **"Add environment variable"**

Agrega estas variables (usa tus valores de `appsettings.Production.json`):

```
Key: ConnectionStrings__esupplier
Value: Server=34.46.87.43;DataBase=DBESUPPLIER;User Id=sqlserver;Password=XlJYG$+JJzr_6>oy;Trusted_Connection=False;TrustServerCertificate=True;MultipleActiveResultSets=True

Key: Http__Consultahesmigo
Value: https://l250890-iflmap.hcisbp.us3.hana.ondemand.com/http/prd/hes/consultahesmigo

Key: Http__Consultarcomprobantes
Value: https://l250890-iflmap.hcisbp.us3.hana.ondemand.com/http/prd/comprobantes/consultacomprobantes

Key: Http__consultarCreditoProveedores
Value: https://l250890-iflmap.hcisbp.us3.hana.ondemand.com/http/prd/proveedor/consultarCreditoProveedores

Key: Http__consultarProyeccionMateriales
Value: https://l250890-iflmap.hcisbp.us3.hana.ondemand.com/http/prd/proveedor/consultarProyeccionMateriales

Key: Http__ConsultarOC
Value: https://l250890-iflmap.hcisbp.us3.hana.ondemand.com/http/prd/oc/consultarOC

Key: Http__ConsultarOCHistorico
Value: https://l250890-iflmap.hcisbp.us3.hana.ondemand.com/http/prd/oc/consultarOCHistorico

Key: Http__Autorizacion
Value: Basic UzAwMjU3ODc1MzY6RTFzdXQ1cy5kZXMk

Key: Web__url
Value: https://consenso.esupplier.biz

Key: ASPNETCORE_ENVIRONMENT
Value: Production
```

3. Click **"Save"**

---

## 🔌 PASO 3: CREAR API GATEWAY

### 3.1 Crear HTTP API

1. Abre https://console.aws.amazon.com/apigateway/
2. Click **"Create API"**
3. En **"HTTP API"**, click **"Build"**

### 3.2 Configuración

1. **Integrations:** 
   - Click **"Add integration"**
   - Select **"Lambda"**
   - Select region: **us-east-1**
   - Lambda function: **esupplier-api**

2. **API name:** `esupplier-api-gateway`

3. Click **"Next"**

### 3.3 Configurar Rutas

1. **Method:** ANY
2. **Resource path:** `/{proxy+}`
3. Click **"Next"**

### 3.4 Configurar Stages

1. **Stage name:** `$default` (auto-deploying)
2. Click **"Next"**

### 3.5 Review y Create

1. Review la configuración
2. Click **"Create"**

### 3.6 Obtener URL

1. En la página de tu API, copia la **"Invoke URL"**
2. Se verá algo así: `https://abc123xyz.execute-api.us-east-1.amazonaws.com`

**Pruébala:**
```bash
curl https://TU_URL/swagger
```

---

## ⏰ PASO 4: CREAR LAMBDA PARA EL JOB

### 4.1 Crear segunda Lambda

1. Vuelve a https://console.aws.amazon.com/lambda/
2. Click **"Create function"**

- **Function name:** `esupplier-job`
- **Runtime:** **.NET 8 (C#/PowerShell)**
- **Architecture:** **x86_64**
- Click **"Create function"**

### 4.2 Subir el MISMO archivo ZIP

1. **"Upload from"** → **".zip file"**
2. Selecciona `esupplier-lambda.zip` (el mismo archivo)
3. Click **"Save"**

### 4.3 Configurar Handler (DIFERENTE)

1. **"Runtime settings"** → **"Edit"**
2. **Handler:** `esupplier::esupplier.Job.ProveedorJobsLambda::FunctionHandler`
3. Click **"Save"**

### 4.4 Configurar Memoria y Timeout

1. **"General configuration"** → **"Edit"**
2. **Memory:** 1024 MB (el job procesa Excel)
3. **Timeout:** 15 minutes (900 seconds)
4. Click **"Save"**

### 4.5 Configurar MISMAS Variables de Entorno

Repite el Paso 2.6 con las mismas variables de entorno.

---

## ⏰ PASO 5: PROGRAMAR EL JOB CON EVENTBRIDGE

### 5.1 Agregar Trigger

1. En la función `esupplier-job`
2. Click **"Add trigger"**
3. Select trigger: **EventBridge (CloudWatch Events)**

### 5.2 Configurar Schedule

1. **Rule:** Create a new rule
2. **Rule name:** `esupplier-daily-job`
3. **Rule type:** **Schedule expression**
4. **Schedule expression:** `cron(5 0 * * ? *)`
   - Esto es: **00:05 UTC diario**
   - Equivale a: 19:05 ECT (Ecuador) si estás en UTC-5
5. Click **"Add"**

---

## ✅ PASO 6: TESTING

### 6.1 Probar la API

```bash
# Reemplaza con tu URL de API Gateway
API_URL="https://abc123xyz.execute-api.us-east-1.amazonaws.com"

# Probar Swagger
curl $API_URL/swagger

# Debería devolver HTML de Swagger UI
```

### 6.2 Probar el Job manualmente

1. Ve a la función `esupplier-job` en Lambda console
2. Tab **"Test"**
3. **Event name:** `test-event`
4. **Template:** Hello World (dejar JSON vacío está OK)
5. Click **"Save"**
6. Click **"Test"**
7. Espera a que termine (puede tardar 1-2 minutos)
8. Revisa los logs - deberías ver:
   - "Getting configuration..."
   - "Downloading file from OneDrive..."
   - "Processing Excel file..."
   - "Job completed successfully"

### 6.3 Ver Logs en CloudWatch

**Para la API:**
1. https://console.aws.amazon.com/cloudwatch/
2. **Logs** → **Log groups**
3. Busca `/aws/lambda/esupplier-api`
4. Click en el log stream más reciente
5. Verás todas las requests

**Para el Job:**
1. Log group: `/aws/lambda/esupplier-job`
2. Verás la ejecución completa con todos los pasos

---

## 🔍 PASO 7: VERIFICACIÓN FINAL

### Checklist

- [ ] Lambda `esupplier-api` creada y funcionando
- [ ] API Gateway configurado y devuelve `/swagger`
- [ ] Lambda `esupplier-job` creada
- [ ] EventBridge programado para 00:05 UTC
- [ ] Job ejecutado manualmente exitosamente
- [ ] Variables de entorno configuradas en ambas Lambdas
- [ ] Logs visibles en CloudWatch

---

## 📊 MONITOREO DE COSTOS

### Estimación Mensual

**Lambda API (esupplier-api):**
- 50,000 requests/mes × 500ms avg = 12,500 GB-s
- Costo compute: ~$0.21
- Costo requests: $0 (dentro de free tier 1M)

**Lambda Job (esupplier-job):**
- 30 ejecuciones/mes × 2 min × 1 GB = 60 GB-minutes = 3,600 GB-s
- Costo: ~$0.60

**API Gateway:**
- 50,000 requests/mes = $0 (dentro de free tier 1M)

**CloudWatch Logs:**
- ~2 GB logs/mes = $1.00

**TOTAL ESTIMADO: $1.80/mes** 🎉

---

## 🚨 TROUBLESHOOTING

### Problema: Lambda devuelve error 502

**Causa:** Probablemente el Handler está mal configurado.

**Solución:**
- Para API: `esupplier::esupplier.LambdaEntryPoint::FunctionHandlerAsync`
- Para Job: `esupplier::esupplier.Job.ProveedorJobsLambda::FunctionHandler`

### Problema: Timeout después de 30 segundos

**Solución:**
- Aumenta el timeout en Configuration → General configuration
- API: 30-60 segundos
- Job: 900 segundos (15 minutos)

### Problema: Out of Memory

**Solución:**
- Aumenta la memoria en Configuration → General configuration
- API: 512 MB → 1024 MB
- Job: 1024 MB → 2048 MB

### Problema: No conecta a SQL Server

**Causa:** Variables de entorno mal configuradas o VPC.

**Solución:**
1. Verifica que `ConnectionStrings__esupplier` esté correcta
2. Lambda debe poder acceder a 34.46.87.43:1433
3. Si el SQL Server está en VPC, configura VPC settings en Lambda

### Problema: Job no se ejecuta automáticamente

**Solución:**
1. Ve a EventBridge Console
2. Verifica que la regla `esupplier-daily-job` esté **Enabled**
3. Verifica el cron expression: `cron(5 0 * * ? *)`

---

## 📝 PRÓXIMOS PASOS OPCIONALES

### Configurar Custom Domain

1. **Route 53 o tu DNS provider:**
   - Crea un CNAME apuntando a tu API Gateway URL
   - Ejemplo: `api.consenso.esupplier.biz` → `abc123xyz.execute-api.us-east-1.amazonaws.com`

2. **API Gateway Custom Domain:**
   - Tab **"Custom domain names"**
   - **Domain name:** `api.consenso.esupplier.biz`
   - **Certificate:** Solicita uno en ACM o usa existente
   - **API mapping:** Mapea a tu API

### Configurar Alarmas

**CloudWatch Alarms:**

1. **Para la API:**
   - Métrica: Errors > 10 en 5 minutos
   - Acción: Email notification

2. **Para el Job:**
   - Métrica: Failed executions > 0
   - Acción: Email notification

---

## 💰 COMPARACIÓN FINAL

| Concepto | Azure App Service | Lambda + API Gateway |
|----------|-------------------|----------------------|
| **Costo/mes** | ~$55 | ~$2 |
| **Setup time** | 30 min | 1 hora |
| **Escalabilidad** | Manual | Automática infinita |
| **Mantenimiento** | Updates, patches | Cero |
| **Cold start** | No | 2-5 segundos |
| **Disponibilidad** | 99.95% | 99.99% |

**Ahorro anual: $636** 🎉

---

## ✅ LISTO

Una vez completados todos los pasos:

1. ✅ Tu API estará disponible en: `https://xxx.execute-api.us-east-1.amazonaws.com`
2. ✅ Tu job se ejecutará automáticamente a las 00:05 UTC diario
3. ✅ Pagarás ~$2/mes en vez de $55/mes
4. ✅ Todo estará monitoreado en CloudWatch

**¿Necesitas ayuda con algún paso?** Los logs de CloudWatch te dirán exactamente qué está pasando.

