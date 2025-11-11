# Análisis Exhaustivo: Despliegue Económico en AWS

## 📊 Características del Proyecto Detectadas

### Tecnologías y Recursos
- **Framework:** ASP.NET Core Web API (.NET 8.0)
- **Base de datos:** SQL Server (externa en GCP: 34.46.87.43)
- **Background Jobs:** Tarea programada diaria (00:05 AM)
- **Integraciones:**
  - Microsoft Graph API (OneDrive)
  - SAP HANA (APIs externas)
  - Procesamiento de Excel
  - Envío de emails
- **Tráfico:** CORS configurado para 5 dominios
- **Documentación:** Swagger UI

### Requerimientos Estimados
- **Memoria:** 512 MB - 1 GB (API + Background Job)
- **CPU:** Baja a media (procesa Excel periódicamente)
- **Almacenamiento:** Mínimo (archivos temporales en memoria)
- **Red:** Conexiones salientes a APIs externas y SQL Server
- **Disponibilidad:** Media (aplicación empresarial, no crítica 24/7)

---

## 💰 OPCIONES DE DESPLIEGUE EN AWS (Ordenadas por Costo)

### 🥇 OPCIÓN 1: AWS Lambda + API Gateway + EventBridge (SERVERLESS) 
**💵 Costo Estimado: $5-15/mes** ⭐ **MÁS ECONÓMICA**

#### Arquitectura
```
API Gateway → Lambda Function (.NET 8) → SQL Server (GCP)
EventBridge → Lambda Function (Job) → OneDrive/SAP
```

#### Costos Detallados
- **Lambda:**
  - 1M requests/mes gratuitas, luego $0.20 por 1M
  - 512 MB RAM: ~$0.0000008333 por 100ms
  - Estimado tráfico bajo: **$5-10/mes**
  
- **API Gateway:**
  - 1M requests/mes: $3.50
  - Estimado: **$3-5/mes**
  
- **EventBridge:**
  - Cron job (1 vez al día): **GRATIS**
  
- **CloudWatch Logs:**
  - 5 GB logs: $0.50
  - Estimado: **$1-2/mes**

#### Ventajas
✅ **Más económica** para tráfico bajo-medio  
✅ Sin gestión de servidores  
✅ Escala automáticamente  
✅ Pago por uso real  
✅ Alta disponibilidad incluida  
✅ Sin cargo por servidor inactivo  
✅ Ideal para APIs con picos esporádicos  

#### Desventajas
❌ Cold start ~2-5 segundos (primera petición)  
❌ Timeout máximo: 15 minutos  
❌ Requiere adaptación del código (refactorización ligera)  
❌ Debugging más complejo  

#### Cuándo es Ideal
- Tráfico bajo a medio (<100K requests/mes)
- No requieres respuestas sub-segundo garantizadas
- Presupuesto limitado

---

### 🥈 OPCIÓN 2: AWS Lightsail (VPS Simplificado)
**💵 Costo Estimado: $3.50-10/mes** ⭐ **SIMPLICIDAD + BAJO COSTO**

#### Planes Recomendados
| Plan | vCPU | RAM | Almacenamiento | Transferencia | Precio/mes |
|------|------|-----|----------------|---------------|------------|
| Micro | 1 | 512 MB | 20 GB SSD | 1 TB | $3.50 |
| Small | 1 | 1 GB | 40 GB SSD | 2 TB | $5.00 |
| Medium | 1 | 2 GB | 60 GB SSD | 3 TB | $10.00 |

#### Configuración Recomendada: **Small ($5/mes)**

#### Costos Detallados
- **Lightsail Instance:** $5/mes (1 vCPU, 1 GB RAM)
- **Snapshot backup (opcional):** $0.05/GB/mes (~$2/mes)
- **IP estática:** **INCLUIDA**
- **Total:** **$5-7/mes**

#### Ventajas
✅ **Precio fijo predecible**  
✅ Muy fácil de configurar (consola simplificada)  
✅ Servidor completo con acceso SSH  
✅ Sin cold starts, rendimiento constante  
✅ Ideal para migración desde Azure App Service  
✅ Incluye firewall y monitoreo básico  
✅ Snapshots fáciles para backups  

#### Desventajas
❌ Recursos limitados en planes económicos  
❌ Sin auto-scaling automático  
❌ Requiere gestión manual del servidor  
❌ Si excedes 1 TB transferencia, costos adicionales  

#### Cuándo es Ideal
- Quieres algo simple y predecible
- Presupuesto muy limitado
- Preferencia por VPS tradicional
- Tráfico estable y predecible

---

### 🥉 OPCIÓN 3: AWS App Runner (Contenedores Serverless)
**💵 Costo Estimado: $12-25/mes** ⭐ **BALANCE COSTO/FUNCIONALIDAD**

#### Arquitectura
```
App Runner (Container) → SQL Server (GCP)
Cron interno o EventBridge → Background Job
```

#### Costos Detallados
- **Compute:**
  - 1 vCPU, 2 GB RAM
  - $0.007/vCPU-min + $0.001/GB-min
  - 24/7: ~$12/mes
  
- **Memory:**
  - 2 GB: ~$3/mes
  
- **Requests:**
  - Primeros 1M requests: **GRATIS**
  - Después: $0.10 por 1M
  
- **Build (CI/CD incluido):** $0.005/min de build
  - ~10 builds/mes: $0.50
  
- **Total:** **$15-20/mes**

#### Ventajas
✅ Despliegue directo desde Docker/GitHub  
✅ Auto-scaling incluido  
✅ HTTPS automático  
✅ CI/CD integrado  
✅ Sin gestión de infraestructura  
✅ Logs y métricas incluidas  
✅ Buena opción para contenedores  

#### Desventajas
❌ Más caro que Lightsail para tráfico bajo  
❌ Menos control que EC2  
❌ Limitado a contenedores HTTP  
❌ Cold start en instancias pausadas  

#### Cuándo es Ideal
- Ya usas Docker
- Quieres despliegue automático desde Git
- Necesitas auto-scaling
- Tráfico variable

---

### 4️⃣ OPCIÓN 4: EC2 t4g.micro con Reserved Instance
**💵 Costo Estimado: $3-5/mes (1 año adelantado) o $7-10/mes (on-demand)**

#### Configuración
- **Instancia:** t4g.micro (ARM Graviton2)
- **Specs:** 2 vCPU, 1 GB RAM
- **Sistema:** Amazon Linux 2023 o Ubuntu

#### Costos Detallados
- **On-Demand:** $0.0084/hora = $6.13/mes
- **Reserved 1 año (upfront):** $25/año = $2.08/mes
- **Reserved 1 año (no upfront):** $0.0052/hora = $3.80/mes
- **EBS Storage (20 GB gp3):** $1.60/mes
- **Elastic IP:** $0/mes (si está asociada)
- **Data Transfer:** 100 GB/mes incluidos

- **Total On-Demand:** ~$8/mes
- **Total Reserved:** ~$5/mes (1 año)

#### Ventajas
✅ Control total del servidor  
✅ Compatible con cualquier .NET workload  
✅ Sin limitaciones de tiempo de ejecución  
✅ Free tier: 750 horas/mes primeros 12 meses  
✅ Mejor relación costo/rendimiento con Reserved  
✅ Networking avanzado disponible  

#### Desventajas
❌ Requiere gestión completa del SO  
❌ Updates, seguridad, monitoreo manual  
❌ Sin auto-scaling simple  
❌ Pago mensual fijo, uses o no el servicio  
❌ Reserved requiere compromiso de 1 año  

#### Cuándo es Ideal
- Necesitas control total
- Workloads 24/7 estables
- Puedes comprometerte a 1 año
- Free tier disponible (primeros 12 meses)

---

### 5️⃣ OPCIÓN 5: Elastic Beanstalk (PaaS)
**💵 Costo Estimado: $15-30/mes**

#### Arquitectura
```
Load Balancer (opcional) → EC2 Auto-Scaling → SQL Server
```

#### Costos Detallados
- **Elastic Beanstalk:** **GRATIS** (solo pagas recursos)
- **EC2 t3.micro:** $7.60/mes
- **Application Load Balancer (opcional):** $16.20/mes
- **EBS Storage:** $1.60/mes
- **CloudWatch:** $1-2/mes
- **Sin ALB:** ~$10/mes
- **Con ALB:** ~$25-30/mes

#### Ventajas
✅ Gestión simplificada de infraestructura  
✅ Auto-scaling y balanceo incluidos (con ALB)  
✅ Múltiples ambientes (dev/staging/prod)  
✅ Despliegue desde Visual Studio  
✅ Monitoreo integrado  

#### Desventajas
❌ Load Balancer aumenta costos significativamente  
❌ Sobrecarga para aplicaciones pequeñas  
❌ Más complejo que Lightsail  
❌ Costos ocultos pueden acumularse  

#### Cuándo es Ideal
- Aplicaciones medianas/grandes
- Necesitas múltiples ambientes
- Presupuesto medio ($20-50/mes)
- Equipo familiarizado con AWS

---

### 6️⃣ OPCIÓN 6: ECS Fargate (Contenedores Serverless)
**💵 Costo Estimado: $15-35/mes**

#### Configuración Mínima
- 0.5 vCPU, 1 GB RAM

#### Costos Detallados
- **Compute (vCPU):** $0.04048/vCPU/hora × 0.5 = $14.57/mes
- **Memory (GB):** $0.004445/GB/hora × 1 = $3.20/mes
- **Application Load Balancer (opcional):** $16.20/mes
- **Sin ALB:** ~$18/mes
- **Con ALB:** ~$35/mes

#### Ventajas
✅ Contenedores sin gestión de servidores  
✅ Escala a cero (con App Runner mejor)  
✅ Integración con ECR, CodePipeline  
✅ Buen rendimiento y aislamiento  

#### Desventajas
❌ Más caro que otras opciones serverless  
❌ Configuración más compleja  
❌ Requiere conocimientos de Docker/ECS  
❌ ALB casi obligatorio, aumenta costos  

#### Cuándo es Ideal
- Arquitecturas de microservicios
- Ya usas Docker extensivamente
- Presupuesto medio-alto

---

## 📊 COMPARACIÓN DIRECTA

| Opción | Costo/mes | Complejidad | Auto-scaling | Cold Start | Gestión | Ideal para |
|--------|-----------|-------------|--------------|------------|---------|------------|
| **Lambda + API Gateway** | $5-15 | Media | ✅ Automático | 2-5s | Mínima | Tráfico bajo, presupuesto limitado |
| **Lightsail** | $3.50-10 | Baja | ❌ Manual | No | Media | Simplicidad, costo predecible |
| **App Runner** | $12-25 | Baja | ✅ Automático | Posible | Mínima | Docker, CI/CD, balance |
| **EC2 t4g.micro** | $5-10 | Alta | ❌ Manual | No | Alta | Control total, Reserved |
| **Elastic Beanstalk** | $15-30 | Media | ✅ Con ALB | No | Media | Aplicaciones tradicionales |
| **ECS Fargate** | $18-35 | Alta | ✅ Manual | No | Media | Microservicios, contenedores |

---

## 🏆 RECOMENDACIONES FINALES

### 🥇 MEJOR OPCIÓN ECONÓMICA GENERAL: **AWS Lightsail Small ($5/mes)**

#### ¿Por qué?
✅ **Costo fijo y predecible:** $5/mes sin sorpresas  
✅ **Simplicidad extrema:** Setup en 10 minutos  
✅ **Sin refactorización:** Deploy directo de tu código  
✅ **Rendimiento constante:** Sin cold starts  
✅ **Suficiente para tu workload:** 1 vCPU + 1 GB RAM  
✅ **Backups fáciles:** Snapshots con un clic  
✅ **Monitoreo incluido:** Métricas básicas gratis  

#### Configuración Recomendada
- Plan: **Small ($5/mes)**
- OS: Ubuntu 22.04 LTS
- Runtime: .NET 8.0
- Reverse Proxy: Nginx
- Process Manager: systemd
- SSL: Let's Encrypt (gratis)
- Backup: Snapshots semanales (~$2/mes)

#### ⚠️ Limitaciones
- Máximo 2 TB transferencia/mes (más que suficiente)
- 1 GB RAM (ajustado pero funcional)
- Sin auto-scaling (no lo necesitas con este tráfico)

---

### 🥈 MEJOR OPCIÓN SI PREFIERES SERVERLESS: **AWS Lambda + API Gateway ($5-15/mes)**

#### ¿Por qué?
✅ **Pago por uso real:** Pagas solo cuando hay requests  
✅ **Escala automáticamente:** De 0 a miles de requests  
✅ **Sin gestión de servidor:** AWS maneja todo  
✅ **Alta disponibilidad:** Multi-AZ por defecto  
✅ **Ideal para tu caso:** Tráfico bajo-medio con picos ocasionales  

#### Consideraciones
- Requiere adaptar el código para Lambda (usando AWS Lambda .NET Annotations)
- Background Job separado con EventBridge
- Cold start ~2-5 segundos aceptable para API empresarial

#### Configuración Recomendada
- Runtime: .NET 8 (Amazon.Lambda.RuntimeSupport)
- Memoria: 512 MB
- Timeout: 30 segundos (API), 900 segundos (Job)
- Concurrencia: 5 ejecuciones simultáneas
- EventBridge: Cron (0 5 * * ? *) para job diario

---

### 🥉 MEJOR OPCIÓN PARA BALANCE: **AWS App Runner ($12-20/mes)**

#### ¿Por qué?
✅ **Despliegue súper simple:** Desde GitHub o ECR  
✅ **Auto-scaling incluido:** Sin configuración  
✅ **HTTPS automático:** Sin configuración de SSL  
✅ **CI/CD integrado:** Push to deploy  
✅ **Buen rendimiento:** Sin cold starts significativos  

#### Cuándo elegir
- Si ya usas Docker
- Si quieres CI/CD automatizado
- Si el presupuesto permite $15-20/mes

---

## 💡 RECOMENDACIÓN ESPECÍFICA PARA TU PROYECTO

### Escenario Actual
- Actualmente en Azure App Service
- SQL Server en GCP (externa)
- Tráfico empresarial bajo-medio
- Background job diario
- Presupuesto objetivo: lo más económico posible

### ✅ MI RECOMENDACIÓN: **AWS Lightsail Small ($5/mes)**

#### Razones Específicas:
1. **Migración directa:** Similar a Azure App Service, sin refactorización
2. **Costo predecible:** $5/mes fijo, sin sorpresas
3. **Fácil setup:** Menos de 1 hora para configurar
4. **Compatible con tu stack:** .NET 8 funciona perfectamente
5. **Background jobs nativos:** Cron en Linux para ProveedorJobs
6. **Suficiente para tu carga:** 1 GB RAM + 1 vCPU maneja tu API cómodamente
7. **Conexión a GCP SQL:** Sin problemas de networking

#### Plan de Migración:
1. Crear instancia Lightsail Ubuntu 22.04
2. Instalar .NET 8 Runtime
3. Configurar Nginx como reverse proxy
4. Configurar systemd para auto-start
5. Configurar cron para background job
6. Configurar Let's Encrypt para HTTPS
7. Testing y switch de DNS

#### Escalamiento Futuro:
- Si crece el tráfico → Upgrade a plan $10/mes (2 GB RAM)
- Si necesitas redundancia → Lightsail Load Balancer ($18/mes)
- Si necesitas auto-scaling → Migrar a App Runner

---

## 📝 NOTAS ADICIONALES

### Base de Datos
Tu SQL Server está en GCP (34.46.87.43). Opciones:

1. **Mantener en GCP (RECOMENDADO):**
   - Sin costos de migración
   - Sin interrupción del servicio
   - Costo actual ya asumido
   - Latencia aceptable desde AWS

2. **Migrar a AWS RDS SQL Server:**
   - db.t3.micro: ~$30/mes (mínimo)
   - db.t4g.micro: No disponible para SQL Server
   - **NO RECOMENDADO:** Aumenta costos significativamente

3. **Migrar a AWS RDS PostgreSQL:**
   - db.t4g.micro: $13/mes
   - Requiere migración de esquema y datos
   - **Solo si:** Planeas cambiar de SQL Server

### Almacenamiento Temporal
Archivos Excel procesados en memoria (MemoryStream):
- ✅ No requiere S3
- ✅ Sin costos adicionales de storage
- ✅ Implementación actual es eficiente

### Monitoreo y Logs
- **Lightsail:** Métricas básicas incluidas, logs en servidor
- **Lambda:** CloudWatch Logs (~$1-2/mes)
- **App Runner:** CloudWatch incluido

### Backups
- **Lightsail:** Snapshots manuales ($0.05/GB/mes)
  - Snapshot semanal de 20 GB: $1/mes
- **Lambda:** No aplica (stateless)
- **App Runner:** Source code en Git = backup natural

---

## 🚀 PRÓXIMOS PASOS RECOMENDADOS

1. **Probar con Free Tier:**
   - EC2 t2.micro: 750 horas/mes gratis (12 meses)
   - Lambda: 1M requests/mes gratis (siempre)
   - Lightsail: 1 mes gratis (nuevo account)

2. **Setup Inicial Recomendado:**
   - Empezar con **Lightsail Small ($5/mes)**
   - Monitorear uso por 1-2 meses
   - Ajustar según necesidades reales

3. **Plan de Contingencia:**
   - Si Lightsail se queda corto → Upgrade a $10/mes
   - Si necesitas auto-scaling → Migrar a Lambda/App Runner
   - Documentar todo para fácil migración

---

## 📊 ESTIMACIÓN DE COSTOS ANUALES

| Opción | Mes | Año | Ahorro vs Azure |
|--------|-----|-----|-----------------|
| Lambda + API Gateway | $10 | $120 | ~$1,000 |
| Lightsail Small | $5 | $60 | ~$1,060 |
| Lightsail Medium | $10 | $120 | ~$1,000 |
| App Runner | $15 | $180 | ~$940 |
| EC2 Reserved | $5 | $60 | ~$1,060 |
| Elastic Beanstalk | $25 | $300 | ~$820 |

*Asumiendo Azure App Service Basic B1 (~$55/mes)*

---

## ✅ CONCLUSIÓN

Para tu proyecto específico, **AWS Lightsail Small ($5/mes)** es la opción más económica y práctica:

- ✅ **Ahorro anual:** ~$600 vs Azure App Service
- ✅ **Simplicidad:** Migración directa sin refactorización
- ✅ **Confiabilidad:** Rendimiento predecible sin cold starts
- ✅ **Escalabilidad:** Upgrade fácil si crece el tráfico
- ✅ **Backup/Restore:** Snapshots simples y económicos

**Alternativa serverless:** Si prefieres pago por uso y no te importa refactorizar ligeramente, **Lambda + API Gateway** ($5-15/mes) es igualmente económica y más escalable.

**¿Necesitas ayuda con la migración?** Puedo crear:
- Scripts de deployment para Lightsail
- Configuración de Nginx
- Setup de systemd para background jobs
- Guía paso a paso de migración

