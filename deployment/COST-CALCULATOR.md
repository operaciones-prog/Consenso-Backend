# Calculadora de Costos AWS - E-Supplier

## 📊 Variables de tu Aplicación

Ajusta estos valores según tu caso real:

- **Requests/mes:** 50,000 (bajo), 200,000 (medio), 1,000,000 (alto)
- **Duración promedio request:** 300-500ms
- **Tamaño response promedio:** 50 KB
- **Background job:** 1 vez/día, ~15 minutos
- **Horas de operación:** 24/7
- **Región:** us-east-1 (Virginia)

---

## 💰 Cálculo Detallado por Opción

### 1️⃣ AWS Lightsail

#### Planes Disponibles

| Plan | vCPU | RAM | Storage | Transfer | Precio | Ideal Para |
|------|------|-----|---------|----------|--------|------------|
| Nano | 0.5 | 512 MB | 20 GB | 500 GB | **$3.50** | Testing |
| Micro | 1 | 512 MB | 20 GB | 1 TB | **$3.50** | Muy bajo tráfico |
| Small | 1 | 1 GB | 40 GB | 2 TB | **$5.00** | ✅ **RECOMENDADO** |
| Medium | 1 | 2 GB | 60 GB | 3 TB | **$10.00** | Tráfico medio |
| Large | 2 | 4 GB | 80 GB | 4 TB | **$20.00** | Tráfico alto |

#### Cálculo para Plan Small ($5/mes)

```
Instancia Small:              $5.00/mes
Snapshot backup (20 GB):      $1.00/mes (opcional)
IP estática:                  $0.00 (incluida)
Firewall/monitoreo:           $0.00 (incluido)
-------------------------------------------
TOTAL:                        $5-6/mes
TOTAL ANUAL:                  $60-72/año
```

#### Sobrecargos Potenciales

- **Data Transfer:** Incluye 2 TB/mes
  - Sobrecarga: $0.09/GB después de 2 TB
  - Ejemplo: 50K requests × 50 KB = 2.5 GB/mes ✅ Dentro del límite

- **Snapshots:** $0.05/GB/mes
  - Backup semanal de 20 GB: $1/mes

**Costo Total Realista:** $5-7/mes

---

### 2️⃣ AWS Lambda + API Gateway

#### Escenario 1: Tráfico Muy Bajo (50K requests/mes)

**Lambda:**
```
Requests: 50,000
- Free tier: 1M requests/mes ✅ GRATIS

Compute time:
- 50,000 × 500ms × 512 MB = 12,500 GB-s
- Free tier: 400,000 GB-s ✅ GRATIS

Background Job (30 días):
- 30 × 900s × 1024 MB = 27,648 GB-s
- Después de free tier: 27,648 × $0.0000166667 = $0.46
```
Lambda Total: **$0.46/mes**

**API Gateway HTTP API:**
```
50,000 requests
- Free tier: 1M requests/mes ✅ GRATIS
```
API Gateway Total: **$0/mes**

**CloudWatch:**
```
Logs: ~1 GB = $0.50
Metrics: Incluidos en free tier
```
CloudWatch Total: **$0.50/mes**

**TOTAL Escenario 1:** ~$1/mes 🎉

---

#### Escenario 2: Tráfico Bajo (200K requests/mes)

**Lambda:**
```
Requests: 200,000 - 1M (free) = 0 (aún en free tier)
- Costo requests: $0

Compute time:
- 200,000 × 500ms × 512 MB = 50,000 GB-s
- Free tier: 400,000 GB-s ✅ Dentro de free tier
- Costo compute: $0

Background Job:
- 27,648 GB-s = $0.46
```
Lambda Total: **$0.46/mes**

**API Gateway:**
```
200,000 requests - 1M (free) = 0 (aún en free tier)
```
API Gateway Total: **$0/mes**

**CloudWatch:**
```
Logs: ~2 GB = $1.00
```
CloudWatch Total: **$1.00/mes**

**TOTAL Escenario 2:** ~$1.50/mes 🎉

---

#### Escenario 3: Tráfico Medio (1M requests/mes)

**Lambda:**
```
Requests: 1,000,000 - 1,000,000 (free) = 0
- Costo requests: $0

Compute time:
- 1M × 500ms × 512 MB = 250,000 GB-s
- Free tier: 400,000 GB-s ✅ Dentro de free tier
- Costo compute: $0

Background Job:
- 27,648 GB-s = $0.46
```
Lambda Total: **$0.46/mes**

**API Gateway:**
```
1,000,000 requests - 1,000,000 (free) = 0
```
API Gateway Total: **$0/mes**

**CloudWatch:**
```
Logs: ~5 GB = $2.50
```
CloudWatch Total: **$2.50/mes**

**TOTAL Escenario 3:** ~$3/mes 🎉

---

#### Escenario 4: Tráfico Alto (5M requests/mes)

**Lambda:**
```
Requests: 5,000,000 - 1,000,000 (free) = 4,000,000
- 4M × $0.20/1M = $0.80

Compute time:
- 5M × 500ms × 512 MB = 1,250,000 GB-s
- Free tier: 400,000 GB-s
- Billable: 850,000 GB-s
- 850,000 × $0.0000166667 = $14.17

Background Job: $0.46
```
Lambda Total: **$15.43/mes**

**API Gateway:**
```
5,000,000 - 1,000,000 (free) = 4,000,000
- 4M × $1.00/1M = $4.00
```
API Gateway Total: **$4.00/mes**

**CloudWatch:**
```
Logs: ~15 GB = $7.50
```
CloudWatch Total: **$7.50/mes**

**TOTAL Escenario 4:** ~$27/mes

---

### 3️⃣ AWS App Runner

#### Configuración Recomendada: 1 vCPU, 2 GB RAM

**Compute (vCPU):**
```
$0.007/vCPU-minute × 1 vCPU × 43,200 min/mes = $302.40/mes
```

**Espera... eso es caro! ¿Qué pasó?**

App Runner cobra por tiempo activo, no por 24/7 si usas auto-pause:

#### Con Auto-Pause (tráfico bajo)

Asumiendo:
- Activo 6 horas/día = 180 horas/mes
- Pausado resto del tiempo

**Compute:**
```
$0.007 × 1 vCPU × 10,800 min = $75.60/mes
```

**Memory:**
```
$0.001 × 2 GB × 10,800 min = $21.60/mes
```

**Requests:**
```
1M requests incluidos, después $0.10/1M
```

**Total con auto-pause:** ~$97/mes

#### Sin Auto-Pause (24/7 - tu caso)

**Compute:**
```
$0.007 × 1 vCPU × 43,200 min = $302.40/mes
```

**Memory:**
```
$0.001 × 2 GB × 43,200 min = $86.40/mes
```

**Build:**
```
~10 builds/mes × 5 min × $0.005 = $0.25
```

**Total 24/7:** ~$389/mes ❌ **DEMASIADO CARO**

---

**⚠️ CORRECCIÓN:** App Runner es económico SOLO si tu app puede pausarse.
Para apps 24/7, App Runner NO es económico.

**Alternativa para App Runner:** ECS Fargate es más barato para 24/7.

---

### 4️⃣ AWS ECS Fargate

#### Configuración: 0.5 vCPU, 1 GB RAM (24/7)

**Compute:**
```
$0.04048/vCPU-hour × 0.5 × 730 hours = $14.78/mes
```

**Memory:**
```
$0.004445/GB-hour × 1 GB × 730 hours = $3.24/mes
```

**Data Transfer:**
```
Salida a internet: 100 GB gratis
Después: $0.09/GB
50K requests × 50 KB = 2.5 GB ✅ Gratis
```

**Application Load Balancer (recomendado):**
```
Fixed cost: $16.20/mes
LCU cost: ~$5-10/mes (bajo tráfico)
```

**Total sin ALB:** $18/mes
**Total con ALB:** $40/mes

---

### 5️⃣ AWS EC2 t4g.micro

#### On-Demand (pay-as-you-go)

**Instance:**
```
t4g.micro: $0.0084/hour × 730 hours = $6.13/mes
```

**Storage (EBS gp3):**
```
20 GB × $0.08/GB = $1.60/mes
```

**Data Transfer:**
```
100 GB/mes incluidos, después $0.09/GB
```

**Total On-Demand:** $7.73/mes

---

#### Reserved Instance (1 año, no upfront)

**Instance:**
```
t4g.micro RI: $0.0052/hour × 730 hours = $3.80/mes
```

**Storage:**
```
$1.60/mes
```

**Total Reserved 1 año:** $5.40/mes

---

#### Reserved Instance (1 año, all upfront)

**Instance:**
```
Pago único: $25/año = $2.08/mes
```

**Storage:**
```
$1.60/mes
```

**Total Reserved upfront:** $3.68/mes 🎉

---

### 6️⃣ AWS Elastic Beanstalk

**EB Platform:** GRATIS (solo pagas recursos)

**EC2 t3.micro:**
```
$0.0104/hour × 730 hours = $7.59/mes
```

**EBS Storage:**
```
20 GB × $0.08/GB = $1.60/mes
```

**Application Load Balancer (recomendado):**
```
$16.20/mes + ~$5/mes LCU = $21.20/mes
```

**CloudWatch:**
```
$1-2/mes
```

**Total sin ALB:** $10/mes
**Total con ALB:** $31/mes

---

## 📊 TABLA COMPARATIVA FINAL

| Opción | Tráfico Bajo (50K) | Tráfico Medio (500K) | Tráfico Alto (2M) | 24/7 | Auto-scaling |
|--------|--------------------|----------------------|-------------------|------|--------------|
| **Lightsail Small** | **$5** | **$5** | **$5** | ✅ | ❌ |
| **Lambda + API GW** | **$1** | **$3** | **$10** | ✅ | ✅ |
| **EC2 t4g (Reserved)** | **$5** | **$5** | **$5** | ✅ | ❌ |
| **EC2 t4g (On-demand)** | $8 | $8 | $8 | ✅ | ❌ |
| **ECS Fargate** | $18 | $18 | $18 | ✅ | ✅ |
| **EB sin ALB** | $10 | $10 | $10 | ✅ | ❌ |
| **EB con ALB** | $31 | $31 | $31 | ✅ | ✅ |
| **App Runner** | ❌ | ❌ | ❌ | ✅ | ✅ |

*App Runner es caro para workloads 24/7 como el tuyo*

---

## 🏆 RECOMENDACIÓN FINAL POR ESCENARIO

### Escenario 1: Presupuesto Ultra Limitado (<$5/mes)
✅ **Lambda + API Gateway ($1-3/mes)**
- Ideal para tráfico bajo-medio
- Requiere refactorización
- Cold starts aceptables

### Escenario 2: Balance Simplicidad/Costo ($5/mes)
✅ **Lightsail Small ($5/mes)**
- Sin refactorización
- Rendimiento predecible
- Setup simple

### Escenario 3: Control Total + Compromiso
✅ **EC2 t4g.micro Reserved ($3.68-5/mes)**
- Mejor precio con compromiso 1 año
- Control completo
- Sin sorpresas

### Escenario 4: Producción Crítica + Auto-scaling
✅ **ECS Fargate sin ALB ($18/mes)**
- Contenedores serverless
- Alta disponibilidad
- Auto-scaling incluido

---

## 💡 CÁLCULO PARA TU CASO ESPECÍFICO

**Estimación tu aplicación:**
- Requests/mes: ~50,000 (bajo)
- Background job: 1 vez/día
- Disponibilidad: 24/7
- Presupuesto objetivo: <$10/mes

**TOP 3 para ti:**

1. **Lambda + API GW:** $1-3/mes ⭐⭐⭐⭐⭐
   - Más económico
   - Requiere adaptación código

2. **Lightsail Small:** $5/mes ⭐⭐⭐⭐⭐
   - Sin refactorización
   - Simple y predecible

3. **EC2 t4g Reserved:** $5/mes ⭐⭐⭐⭐
   - Control total
   - Requiere compromiso 1 año

---

## 📈 Proyección de Costos a 12 Meses

| Opción | Mes 1 | Mes 6 | Mes 12 | Año 1 | Ahorro vs Azure ($55/mes) |
|--------|-------|-------|--------|-------|---------------------------|
| Lambda | $1 | $2 | $3 | $24 | **$636** |
| Lightsail | $5 | $5 | $5 | $60 | **$600** |
| EC2 Reserved | $30* | $5 | $5 | $75 | **$585** |
| ECS Fargate | $18 | $18 | $18 | $216 | **$444** |

*EC2 Reserved: $25 upfront año 1, luego $5/mes

---

## 🎯 CONCLUSIÓN

Para tu aplicación específica:

**RECOMENDACIÓN #1:** AWS Lightsail Small (**$5/mes**)
- ✅ Simplicidad máxima
- ✅ Costo predecible
- ✅ Sin refactorización
- ✅ Suficiente para tu carga

**ALTERNATIVA #2:** Lambda + API Gateway (**$1-3/mes**)
- ✅ Más económico
- ✅ Escala automático
- ⚠️ Requiere adaptación código

**Ahorro anual:** ~$600 vs Azure App Service

