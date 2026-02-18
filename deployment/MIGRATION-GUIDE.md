# Guía de Migración a AWS - Paso a Paso

## 🎯 Objetivo
Migrar E-Supplier Backend desde Azure App Service a AWS con el menor costo posible.

---

## 📋 Pre-requisitos

- [ ] Cuenta AWS activa
- [ ] AWS CLI instalado
- [ ] Acceso a tu código fuente
- [ ] Credenciales SQL Server (GCP)
- [ ] Credenciales OneDrive API
- [ ] Dominio configurado (opcional)

---

## 🚀 OPCIÓN A: Migración a Lightsail (RECOMENDADA)

**Tiempo estimado:** 1-2 horas  
**Costo:** $5/mes  
**Dificultad:** ⭐⭐ Fácil

### Paso 1: Crear Instancia Lightsail (10 min)

1. **Acceder a Lightsail:**
   - https://lightsail.aws.amazon.com/
   - Click "Create instance"

2. **Seleccionar configuración:**
   - Region: `us-east-1` (Virginia) o la más cercana
   - Platform: `Linux/Unix`
   - Blueprint: `OS Only` → `Ubuntu 22.04 LTS`
   - Plan: **$5 USD** (1 GB RAM, 1 vCPU, 40 GB SSD)
   - Instance name: `esupplier-prod`

3. **Crear instancia:**
   - Click "Create instance"
   - Esperar ~2 minutos

4. **Configurar IP estática:**
   - En el dashboard de la instancia
   - Tab "Networking"
   - "Create static IP"
   - Asignar a tu instancia

### Paso 2: Configurar Servidor (30 min)

1. **Conectar vía SSH:**
   ```bash
   # Descargar key SSH desde Lightsail console
   chmod 400 LightsailDefaultKey-us-east-1.pem
   
   ssh -i LightsailDefaultKey-us-east-1.pem ubuntu@TU_IP_ESTATICA
   ```

2. **Ejecutar script de setup:**
   ```bash
   # Descargar script
   wget https://raw.githubusercontent.com/[tu-repo]/deployment/lightsail/setup.sh
   
   # Dar permisos
   chmod +x setup.sh
   
   # Ejecutar
   sudo ./setup.sh
   ```

   O copiar el contenido de `/deployment/lightsail/setup.sh` y ejecutar manualmente.

3. **Verificar instalación:**
   ```bash
   dotnet --version  # Debe mostrar 8.0.x
   nginx -v          # Debe mostrar nginx instalado
   sudo systemctl status esupplier  # Debe estar cargado (no activo aún)
   ```

### Paso 3: Compilar y Subir Aplicación (20 min)

1. **En tu máquina local, compilar:**
   ```bash
   cd /path/to/Consenso-Backend
   dotnet publish -c Release -o ./publish
   ```

2. **Subir archivos al servidor:**
   ```bash
   # Comprimir
   tar -czf esupplier.tar.gz -C ./publish .
   
   # Subir
   scp -i LightsailDefaultKey-us-east-1.pem \
       esupplier.tar.gz \
       ubuntu@TU_IP:/tmp/
   ```

3. **En el servidor, descomprimir:**
   ```bash
   sudo mkdir -p /var/www/esupplier
   sudo tar -xzf /tmp/esupplier.tar.gz -C /var/www/esupplier
   sudo chown -R esupplier:esupplier /var/www/esupplier
   ```

4. **Configurar appsettings:**
   ```bash
   sudo nano /var/www/esupplier/appsettings.Production.json
   ```
   
   Verificar:
   - ConnectionStrings apuntan a tu SQL Server en GCP
   - URLs de APIs SAP HANA correctas
   - Configuración de OneDrive

### Paso 4: Iniciar Servicio (10 min)

1. **Iniciar aplicación:**
   ```bash
   sudo systemctl start esupplier
   sudo systemctl enable esupplier
   ```

2. **Verificar estado:**
   ```bash
   sudo systemctl status esupplier
   ```
   
   Debe mostrar "active (running)"

3. **Ver logs:**
   ```bash
   sudo journalctl -u esupplier -f
   ```
   
   Debe mostrar:
   ```
   Now listening on: http://localhost:5000
   Application started. Press Ctrl+C to shut down.
   ```

4. **Probar API:**
   ```bash
   curl http://localhost:5000/swagger
   curl http://TU_IP/swagger  # Desde fuera
   ```

### Paso 5: Configurar SSL (15 min) [OPCIONAL]

1. **Apuntar dominio a tu IP:**
   - En tu proveedor DNS (Namecheap, GoDaddy, etc.)
   - Crear registro A: `api.tudominio.com` → `TU_IP_LIGHTSAIL`

2. **Instalar certificado SSL:**
   ```bash
   sudo certbot --nginx -d api.tudominio.com
   ```
   
   Seguir las instrucciones en pantalla.

3. **Verificar auto-renovación:**
   ```bash
   sudo certbot renew --dry-run
   ```

### Paso 6: Verificación Final (10 min)

- [ ] API responde en http://TU_IP/swagger
- [ ] API responde en https://api.tudominio.com (si configuraste SSL)
- [ ] Logs muestran aplicación corriendo
- [ ] Background job configurado: `sudo crontab -e -u esupplier`
  ```
  5 0 * * * /usr/bin/dotnet /var/www/esupplier/esupplier.dll --run-job
  ```
- [ ] Firewall configurado: `sudo ufw status`
- [ ] Backup configurado: Snapshot manual en Lightsail console

### Paso 7: Migración de Tráfico

1. **Testing paralelo:**
   - Probar API con tu IP Lightsail
   - Verificar todas las funcionalidades

2. **Switch de DNS:**
   - Cambiar DNS de Azure a Lightsail
   - TTL bajo (300s) para rollback rápido

3. **Monitoreo post-migración:**
   - Ver logs: `sudo tail -f /var/log/esupplier/stdout.log`
   - Ver métricas en Lightsail console

---

## 🚀 OPCIÓN B: Migración a Lambda (MÁS ECONÓMICA)

**Tiempo estimado:** 3-4 horas  
**Costo:** $1-3/mes  
**Dificultad:** ⭐⭐⭐⭐ Media-Alta

### Paso 1: Preparar Código (90 min)

1. **Instalar paquetes NuGet:**
   ```bash
   dotnet add package Amazon.Lambda.AspNetCoreServer.Hosting
   dotnet add package Amazon.Lambda.Core
   dotnet add package Amazon.Lambda.Serialization.SystemTextJson
   ```

2. **Modificar Program.cs:**
   Ver `/deployment/lambda/README.md` para detalles completos.

3. **Crear LambdaEntryPoint.cs**

4. **Separar Background Job** en función Lambda independiente

5. **Testing local:**
   ```bash
   dotnet lambda invoke-function ESupplierApi --payload "{}"
   ```

### Paso 2: Instalar AWS SAM (15 min)

```bash
# macOS
brew tap aws/tap
brew install aws-sam-cli

# Verificar
sam --version
```

### Paso 3: Crear template.yaml (30 min)

Ver `/deployment/lambda/README.md` para template completo.

### Paso 4: Build y Deploy (20 min)

```bash
# Build
sam build

# Deploy con wizard
sam deploy --guided

# Responder preguntas:
# - Stack Name: esupplier-prod
# - Region: us-east-1
# - Confirm changes: Y
# - Allow SAM CLI IAM role creation: Y
# - Save arguments to config: Y
```

### Paso 5: Configurar EventBridge para Job (10 min)

En AWS Console:
1. EventBridge → Rules → Create rule
2. Schedule: `cron(5 0 * * ? *)`
3. Target: Lambda function `ProveedorJob`

### Paso 6: Testing y Verificación (15 min)

```bash
# Obtener API URL del output de SAM
curl https://XXXXX.execute-api.us-east-1.amazonaws.com/swagger

# Test desde tu app
curl -X GET https://XXXXX.execute-api.us-east-1.amazonaws.com/api/proveedor
```

---

## 🚀 OPCIÓN C: Migración con Docker (App Runner/ECS)

**Tiempo estimado:** 2-3 horas  
**Costo:** $18-40/mes  
**Dificultad:** ⭐⭐⭐ Media

### Paso 1: Crear Dockerfile (15 min)

Ya está en `/deployment/docker/Dockerfile`

### Paso 2: Build y Test Local (20 min)

```bash
# Build
docker build -t esupplier:latest -f deployment/docker/Dockerfile .

# Test local
docker run -p 5000:5000 esupplier:latest

# Verificar
curl http://localhost:5000/swagger
```

### Paso 3: Push a ECR (20 min)

```bash
# Crear repositorio ECR
aws ecr create-repository --repository-name esupplier --region us-east-1

# Login
aws ecr get-login-password --region us-east-1 | \
    docker login --username AWS --password-stdin \
    ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com

# Tag
docker tag esupplier:latest \
    ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/esupplier:latest

# Push
docker push ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/esupplier:latest
```

### Paso 4A: Deploy a App Runner (30 min)

1. **AWS Console → App Runner → Create service**
2. Source: Container registry → Amazon ECR
3. Browse → Select your image
4. Deployment settings:
   - vCPU: 1
   - Memory: 2 GB
   - Port: 5000
5. Health check: `/swagger`
6. Environment variables:
   - ASPNETCORE_ENVIRONMENT=Production
7. Create service

### Paso 4B: Deploy a ECS Fargate (45 min)

Más complejo, requiere:
- Cluster ECS
- Task Definition
- Service
- (Opcional) Load Balancer

Recomiendo usar AWS Console wizard "Create service" en ECS.

---

## 📊 Comparación de Opciones

| Opción | Tiempo | Costo/mes | Dificultad | Refactorización |
|--------|--------|-----------|------------|-----------------|
| **Lightsail** | 1-2h | $5 | ⭐⭐ | No |
| **Lambda** | 3-4h | $1-3 | ⭐⭐⭐⭐ | Sí |
| **App Runner** | 2-3h | $18-40 | ⭐⭐⭐ | No (con Docker) |
| **ECS Fargate** | 3-4h | $18-40 | ⭐⭐⭐⭐ | No (con Docker) |

---

## 🎯 Recomendación para Migración Rápida

### PLAN RECOMENDADO: Lightsail

**Día 1 (Sábado):**
- 09:00-10:00: Crear y configurar instancia Lightsail
- 10:00-11:00: Compilar y subir aplicación
- 11:00-12:00: Testing y verificación
- 12:00-13:00: Configurar SSL (opcional)

**Día 1 (Tarde):**
- 14:00-15:00: Testing paralelo exhaustivo
- 15:00-16:00: Documentar y preparar rollback

**Día 2 (Domingo):**
- 10:00: Cambiar DNS a Lightsail
- 10:30: Monitoreo intensivo
- 12:00: Verificar background job
- 14:00: Migración completa ✅

**Rollback Plan:**
- Revertir DNS a Azure (5 minutos)
- Lightsail sigue corriendo para debugging

---

## ✅ Checklist Post-Migración

### Inmediato (Día 1)
- [ ] API responde correctamente
- [ ] Swagger accesible
- [ ] Logs sin errores críticos
- [ ] Conexión a SQL Server funciona
- [ ] Endpoints principales probados

### Día 2-7
- [ ] Background job ejecuta correctamente
- [ ] Integración OneDrive funciona
- [ ] Integración SAP HANA funciona
- [ ] Sin errores 500 en logs
- [ ] Performance aceptable (< 1s response time)

### Semana 2
- [ ] Crear snapshot de backup
- [ ] Documentar configuración
- [ ] Configurar alarmas CloudWatch (opcional)
- [ ] Plan de escalamiento si crece tráfico

### Mes 1
- [ ] Revisar costos reales en AWS Billing
- [ ] Ajustar recursos si necesario
- [ ] Optimizar logs si storage es alto
- [ ] Considerar Reserved Instance si EC2

---

## 🆘 Troubleshooting Común

### Problema 1: API no responde
```bash
# Ver logs
sudo journalctl -u esupplier -n 100

# Verificar proceso
sudo systemctl status esupplier

# Reiniciar
sudo systemctl restart esupplier
```

### Problema 2: Error de conexión a SQL Server
```bash
# Verificar configuración
cat /var/www/esupplier/appsettings.Production.json

# Test conexión desde servidor
telnet 34.46.87.43 1433

# Verificar Security Groups (si aplica)
```

### Problema 3: Cold start muy lento (Lambda)
```bash
# Aumentar memoria (más memoria = más CPU)
aws lambda update-function-configuration \
    --function-name ESupplierApi \
    --memory-size 1024
```

### Problema 4: Nginx 502 Bad Gateway
```bash
# Verificar que la app está corriendo
curl http://localhost:5000/swagger

# Ver logs de Nginx
sudo tail -f /var/log/nginx/error.log

# Verificar configuración
sudo nginx -t
```

---

## 💰 Estimación de Costos Primer Mes

### Lightsail
```
Instancia $5/mes:        $5.00
Setup (tiempo):          $0.00
Dominio (ya tienes):     $0.00
SSL (Let's Encrypt):     $0.00
Snapshot backup:         $1.00
-----------------------------------
TOTAL:                   $6.00
```

### Lambda
```
Free tier (primeros 12 meses): $0.00
Después de free tier:          $1-3.00
Setup (tiempo):                $0.00
-----------------------------------
TOTAL:                         $0-3.00
```

---

## 📞 Soporte

¿Necesitas ayuda? Revisa:

1. **Documentación AWS:**
   - Lightsail: https://lightsail.aws.amazon.com/ls/docs
   - Lambda: https://docs.aws.amazon.com/lambda/

2. **Logs de la aplicación:**
   ```bash
   sudo tail -f /var/log/esupplier/stdout.log
   ```

3. **AWS Support:**
   - Basic (gratis): Documentación y foros
   - Developer ($29/mes): Soporte técnico

---

## ✅ Conclusión

**Para migración rápida y económica:**
→ Elige **Lightsail** ($5/mes, 1-2 horas setup)

**Para máximo ahorro:**
→ Elige **Lambda** ($1-3/mes, 3-4 horas setup)

**Para simplicidad con Docker:**
→ Elige **App Runner** ($18-40/mes, 2-3 horas setup)

**Mi recomendación:** Empieza con Lightsail. Si después necesitas más escalabilidad, migra a Lambda o ECS.

