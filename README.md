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

