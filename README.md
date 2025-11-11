# Consenso E-Supplier Backend

Backend API para el sistema E-Supplier de Consenso.

## Tecnologías

- .NET 8.0
- ASP.NET Core Web API
- Entity Framework Core

## Requisitos

- .NET SDK 8.0 o superior
- SQL Server

## Configuración

1. Clonar el repositorio
2. Restaurar paquetes NuGet:
   ```bash
   dotnet restore
   ```

3. Configurar las cadenas de conexión en `appsettings.json`

4. Compilar el proyecto:
   ```bash
   dotnet build
   ```

5. Ejecutar el proyecto:
   ```bash
   dotnet run
   ```

## Estructura del Proyecto

- **Job/** - Tareas programadas (Jobs)
- **Models/** - Modelos de datos
- **Repositorys/** - Capa de acceso a datos
- **RestControllers/** - Controladores de API
- **Services/** - Lógica de negocio
- **Utils/** - Utilidades y constantes

## Entornos

- **Development** - Desarrollo local
- **Release** - Ambiente de pruebas (QAS)
- **Production** - Ambiente de producción (PRD)

## 🚀 Deployment en AWS

### Opciones Disponibles

Este proyecto incluye documentación completa para desplegar en AWS con la opción más económica:

| Opción | Costo/mes | Setup | Recomendado |
|--------|-----------|-------|-------------|
| **AWS Lightsail** | $5 | 1-2 horas | ✅ Balance precio/simplicidad |
| **AWS Lambda** | $1-3 | 3-4 horas | ⭐ Máximo ahorro |
| **AWS App Runner** | $18-40 | 2-3 horas | 🐳 Docker + CI/CD |

**Ahorro vs Azure App Service:** ~$600/año

### Documentación Completa

Toda la documentación de deployment está en la carpeta `/deployment/`:

- **[AWS-DEPLOYMENT-ANALYSIS.md](./AWS-DEPLOYMENT-ANALYSIS.md)** - Análisis exhaustivo de 6 opciones con costos detallados
- **[deployment/COST-CALCULATOR.md](./deployment/COST-CALCULATOR.md)** - Calculadora de costos por escenario
- **[deployment/MIGRATION-GUIDE.md](./deployment/MIGRATION-GUIDE.md)** - Guía paso a paso para migración
- **[deployment/README.md](./deployment/README.md)** - Índice completo de deployment

### Quick Start - Lightsail (Recomendado)

```bash
# 1. Crear instancia Lightsail Ubuntu 22.04 ($5/mes)
# 2. Conectar vía SSH
ssh -i tu-key.pem ubuntu@TU_IP

# 3. Configurar servidor
bash deployment/lightsail/setup.sh

# 4. Compilar y subir aplicación
dotnet publish -c Release -o ./publish
scp -r ./publish/* ubuntu@TU_IP:/var/www/esupplier/

# 5. Iniciar servicio
sudo systemctl start esupplier
```

Ver [deployment/MIGRATION-GUIDE.md](./deployment/MIGRATION-GUIDE.md) para instrucciones detalladas.

