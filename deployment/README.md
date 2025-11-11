# 🚀 Deployment Documentation - E-Supplier Backend

Documentación completa para desplegar E-Supplier en AWS con la opción más económica.

## 📚 Documentos Disponibles

### 1. [AWS-DEPLOYMENT-ANALYSIS.md](../AWS-DEPLOYMENT-ANALYSIS.md)
**Análisis exhaustivo de todas las opciones de AWS**

Incluye:
- ✅ Comparación detallada de 6 opciones de deployment
- ✅ Costos estimados por opción
- ✅ Ventajas y desventajas de cada una
- ✅ Recomendaciones específicas para tu proyecto

**Recomendación principal:** AWS Lightsail Small ($5/mes)

---

### 2. [COST-CALCULATOR.md](./COST-CALCULATOR.md)
**Calculadora detallada de costos**

Incluye:
- ✅ Desglose de costos por componente
- ✅ Cálculos para diferentes niveles de tráfico
- ✅ Comparación de costos anuales
- ✅ Proyecciones y ahorro vs Azure

---

### 3. [MIGRATION-GUIDE.md](./MIGRATION-GUIDE.md)
**Guía paso a paso para migración**

Incluye:
- ✅ Plan de migración detallado
- ✅ Comandos exactos a ejecutar
- ✅ Checklist de verificación
- ✅ Troubleshooting común

---

## 🎯 Inicio Rápido

### Opción Recomendada: Lightsail ($5/mes)

**1. Crear instancia:**
```bash
# Accede a https://lightsail.aws.amazon.com/
# Crea Ubuntu 22.04, Plan $5/mes
```

**2. Configurar servidor:**
```bash
# Conectar vía SSH
ssh -i tu-key.pem ubuntu@TU_IP

# Ejecutar script de setup
bash <(curl -s https://raw.githubusercontent.com/.../setup.sh)
# O usa: /deployment/lightsail/setup.sh
```

**3. Compilar y subir:**
```bash
# En tu máquina local
dotnet publish -c Release -o ./publish
scp -r ./publish/* ubuntu@TU_IP:/var/www/esupplier/
```

**4. Iniciar servicio:**
```bash
# En el servidor
sudo systemctl start esupplier
sudo systemctl enable esupplier
```

**5. Verificar:**
```bash
curl http://TU_IP/swagger
```

---

## 📁 Estructura de Carpetas

```
deployment/
├── README.md                      # Este archivo
├── COST-CALCULATOR.md             # Calculadora de costos
├── MIGRATION-GUIDE.md             # Guía de migración paso a paso
│
├── lightsail/                     # Opción #1: Lightsail ($5/mes)
│   ├── setup.sh                   # Script de configuración del servidor
│   └── deploy.sh                  # Script de deployment automático
│
├── lambda/                        # Opción #2: Lambda ($1-3/mes)
│   └── README.md                  # Guía para Lambda + API Gateway
│
└── docker/                        # Opción #3: Docker (App Runner/ECS)
    ├── Dockerfile                 # Configuración Docker
    ├── .dockerignore              # Archivos a ignorar
    └── docker-compose.yml         # Para testing local
```

---

## 💰 Comparación Rápida de Costos

| Opción | Costo/mes | Setup | Dificultad | Recomendado Para |
|--------|-----------|-------|------------|------------------|
| **Lightsail** | **$5** | 1-2h | ⭐⭐ | **Balance precio/simplicidad** ✅ |
| Lambda | $1-3 | 3-4h | ⭐⭐⭐⭐ | Máximo ahorro |
| App Runner | $18-40 | 2-3h | ⭐⭐⭐ | Docker + CI/CD |
| ECS Fargate | $18-40 | 3-4h | ⭐⭐⭐⭐ | Microservicios |
| EC2 Reserved | $5 | 2-3h | ⭐⭐⭐ | Control total |
| Elastic Beanstalk | $10-30 | 2-3h | ⭐⭐⭐ | PaaS tradicional |

---

## 🏆 Recomendaciones por Escenario

### Caso 1: Quieres algo simple y económico
→ **Lightsail Small ($5/mes)**
- Sin refactorización de código
- Setup en 1-2 horas
- Rendimiento predecible

### Caso 2: Quieres el máximo ahorro posible
→ **Lambda + API Gateway ($1-3/mes)**
- Requiere adaptar código
- Setup en 3-4 horas
- Pago por uso real

### Caso 3: Ya usas Docker
→ **App Runner ($18-40/mes)** o **ECS Fargate ($18/mes)**
- Deploy desde Docker
- CI/CD incluido
- Auto-scaling

### Caso 4: Necesitas control total
→ **EC2 t4g.micro Reserved ($5/mes)**
- Acceso root completo
- Sin limitaciones
- Requiere gestión de OS

---

## 📊 Ahorro Estimado vs Azure

**Azure App Service Basic B1:** ~$55/mes  
**Lightsail Small:** $5/mes  
**Ahorro anual:** ~$600

---

## 🚀 Próximos Pasos

### 1. Leer la documentación
- [ ] Revisar [AWS-DEPLOYMENT-ANALYSIS.md](../AWS-DEPLOYMENT-ANALYSIS.md)
- [ ] Revisar [COST-CALCULATOR.md](./COST-CALCULATOR.md)

### 2. Decidir opción
- [ ] Lightsail (recomendado)
- [ ] Lambda (más económico)
- [ ] Docker (App Runner/ECS)

### 3. Seguir guía de migración
- [ ] Abrir [MIGRATION-GUIDE.md](./MIGRATION-GUIDE.md)
- [ ] Seguir pasos específicos de tu opción elegida

### 4. Testing
- [ ] Probar API
- [ ] Verificar background job
- [ ] Validar integraciones

### 5. Go Live
- [ ] Cambiar DNS
- [ ] Monitorear aplicación
- [ ] Verificar costos reales

---

## 🛠️ Scripts Disponibles

### Lightsail

**`lightsail/setup.sh`** - Configura servidor Ubuntu
```bash
# Instala .NET 8, Nginx, configura firewall, crea servicios
sudo bash setup.sh
```

**`lightsail/deploy.sh`** - Deployment automático
```bash
# Compila, sube archivos, reinicia servicio
./deploy.sh
```

### Docker

**`docker/Dockerfile`** - Build para producción
```bash
docker build -t esupplier:latest -f deployment/docker/Dockerfile .
```

**`docker/docker-compose.yml`** - Testing local
```bash
docker-compose -f deployment/docker/docker-compose.yml up
```

---

## 📝 Configuración Requerida

Antes de desplegar, asegúrate de tener:

- [ ] **SQL Server:** Conexión string válida (actualmente en GCP)
- [ ] **OneDrive API:** Client ID, Secret, Tenant ID
- [ ] **SAP HANA APIs:** URLs y credenciales de autenticación
- [ ] **SMTP:** Configuración para envío de emails
- [ ] **Dominio:** (opcional) Para SSL/HTTPS

---

## ⚠️ Consideraciones Importantes

### Base de Datos
Tu SQL Server está en GCP (34.46.87.43):
- ✅ Mantenerlo ahí es la opción más económica
- ✅ No hay costos de migración
- ✅ Latencia aceptable desde AWS
- ⚠️ Si migras a RDS SQL Server: +$30/mes mínimo

### Background Jobs
Tarea programada (00:05 diario):
- **Lightsail:** Usar cron de Linux ✅
- **Lambda:** Usar EventBridge ✅
- **Docker:** Cron interno en container ✅

### Almacenamiento
Archivos Excel procesados en memoria:
- ✅ No requiere S3
- ✅ Sin costos adicionales
- ✅ Implementación actual es óptima

---

## 📈 Monitoreo y Logs

### Lightsail
```bash
# Ver logs de aplicación
sudo journalctl -u esupplier -f

# Ver logs de Nginx
sudo tail -f /var/log/nginx/error.log

# Métricas en Lightsail Console
https://lightsail.aws.amazon.com/
```

### Lambda
```bash
# Ver logs en CloudWatch
aws logs tail /aws/lambda/ESupplierApi --follow

# Métricas en Lambda Console
https://console.aws.amazon.com/lambda/
```

### Docker (App Runner/ECS)
```bash
# Ver logs en CloudWatch
aws logs tail /aws/apprunner/esupplier --follow
```

---

## 🔐 Seguridad

### SSL/HTTPS
- **Lightsail:** Let's Encrypt (gratis)
- **Lambda:** API Gateway (incluido)
- **App Runner:** Automático (incluido)

### Firewall
- **Lightsail:** UFW + Lightsail firewall
- **Lambda:** Security Groups
- **App Runner:** Configuración en servicio

### Secrets
- Usar AWS Secrets Manager ($0.40/secret/mes)
- O variables de entorno encriptadas
- O appsettings.json con permisos restringidos

---

## 💡 Tips y Best Practices

### 1. Empieza con Lightsail
- Más simple de configurar
- Migración sin refactorización
- Puedes mover a Lambda después si quieres

### 2. Usa Free Tier
- EC2: 750 horas/mes gratis (12 meses)
- Lambda: 1M requests/mes gratis (siempre)
- Lightsail: 1 mes gratis (nuevo account)

### 3. Backups Regulares
- Lightsail: Snapshots semanales ($1/mes)
- Lambda: Code en Git = backup natural
- RDS: Automated backups (si aplica)

### 4. Monitoreo de Costos
- Configurar Budget Alert en $10/mes
- Revisar AWS Cost Explorer mensualmente
- Activar cost allocation tags

---

## 🆘 Soporte y Recursos

### Documentación AWS
- Lightsail: https://lightsail.aws.amazon.com/ls/docs
- Lambda: https://docs.aws.amazon.com/lambda/
- App Runner: https://docs.aws.amazon.com/apprunner/

### Calculadora de Costos AWS
https://calculator.aws/

### AWS Free Tier
https://aws.amazon.com/free/

### Community Support
- Stack Overflow: `[aws-lightsail]`, `[aws-lambda]`
- AWS Forums: https://forums.aws.amazon.com/

---

## ✅ Checklist de Migración

### Pre-migración
- [ ] Cuenta AWS creada y verificada
- [ ] AWS CLI instalado y configurado
- [ ] Código fuente accesible
- [ ] Credenciales de servicios externos disponibles
- [ ] Plan de rollback definido

### Durante migración
- [ ] Instancia/servicio creado en AWS
- [ ] Aplicación desplegada y corriendo
- [ ] Testing exhaustivo completado
- [ ] SSL/HTTPS configurado (si aplica)
- [ ] Background jobs configurados

### Post-migración
- [ ] DNS apuntando a AWS
- [ ] Aplicación funcionando en producción
- [ ] Monitoreo activo (logs, métricas)
- [ ] Backups configurados
- [ ] Documentación actualizada
- [ ] Equipo capacitado en nueva infraestructura

---

## 📞 Necesitas Ayuda?

Si necesitas asistencia con:
- Scripts personalizados de deployment
- Configuración específica de tu entorno
- Troubleshooting de problemas
- Optimización de costos

Revisa primero:
1. [MIGRATION-GUIDE.md](./MIGRATION-GUIDE.md) - Troubleshooting section
2. Logs de tu aplicación
3. AWS Documentation

---

## 🎉 ¡Listo para empezar!

1. **Lee:** [AWS-DEPLOYMENT-ANALYSIS.md](../AWS-DEPLOYMENT-ANALYSIS.md)
2. **Calcula:** [COST-CALCULATOR.md](./COST-CALCULATOR.md)
3. **Migra:** [MIGRATION-GUIDE.md](./MIGRATION-GUIDE.md)
4. **Deploy:** Usa scripts en `/lightsail/` o `/docker/`

**Recomendación:** Empieza con Lightsail Small ($5/mes) para una migración rápida y sin complicaciones.

**Ahorro esperado:** ~$50/mes ($600/año) vs Azure App Service

¡Buena suerte con tu migración! 🚀

