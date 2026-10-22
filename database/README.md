# Scripts de base de datos – Consenso / eSupplier

Scripts versionados para SQL Server 2019 que corrigen las observaciones de la revisión
(rama `fix/observaciones-revision`). Se generaron a partir de las definiciones exportadas
de QAS (37 SP, 2 funciones, 1 vista, columnas e índices). **No se ejecutaron contra ninguna BD.**

## Prerrequisitos

- SQL Server 2019 (o Azure SQL), nivel de compatibilidad **>= 130** (OPENJSON, `CREATE OR ALTER`, `STRING_AGG` en los pre-checks requiere 140).
  Verificar: `SELECT name, compatibility_level FROM sys.databases WHERE name = DB_NAME();`
- Ejecutar conectado a la **base de datos de la aplicación** (los scripts no contienen `USE`).
- Usuario con permisos `db_owner` (o `ALTER` sobre el esquema dbo + `CREATE ROLE` para 005).
- **Respaldo previo** de la BD (o al menos script de los SP afectados: ver “Rollback”).
- Ejecutar con SSMS / sqlcmd (los scripts usan separadores `GO`). Ejemplo:
  `sqlcmd -S <servidor> -d <bd> -E -b -i 001_usuario_clave_hash.sql`

## Orden de despliegue

1. **Ejecutar los scripts 001 → 005** (006 es opcional) en este orden.
2. **Después** desplegar el backend nuevo (Lambda).

Los scripts son compatibles con el backend actual (mismas firmas y columnas, salvo lo indicado),
por lo que pueden aplicarse antes del despliegue sin cortar el servicio. Excepciones a tener en cuenta:

- `sp_listarUsuario` deja de devolver `clave` (el backend nuevo no la usa).
- El backend nuevo envía el **hash** de la clave; `sp_mantenerUsuario` conserva la clave
  existente cuando `@clave` llega `NULL` o vacía.
- `sp_mantenerOC` (UPDATE/MODIFY) ahora **lanza error** si la OC no existe (antes no hacía nada).
- `sp_mantenerConstante` ahora **lanza error** si la constante no existe.
- `sp_mantenerOCDet`: un UPDATE de una posición inexistente ahora la **inserta** (antes se perdía en silencio). Otros `@action_code` se siguen ignorando como antes.

Todos los scripts son **idempotentes** (se pueden re-ejecutar).

## Scripts

| # | Archivo | Qué hace |
|---|---------|----------|
| 001 | `001_usuario_clave_hash.sql` | Amplía `tb_usuario.clave` a `varchar(256) NULL`. `sp_validarUsuario` (@clave 256). `sp_mantenerUsuario` (@clave 256, @codigo_sociedad 18, conserva clave si viene vacía, transacción). Nuevos `sp_obtenerCredencialUsuario` y `sp_actualizarClaveUsuario`. `sp_listarUsuario` sin columna `clave`. |
| 002 | `002_sp_mantenerOC_cabecera.sql` | Crea `sp_mantenerOC_cabecera(@p_json nvarchar(max), @p_sociedad varchar(18) = NULL)`: inserta en lote, desde JSON, las cabeceras de OC de SAP que aún no existen (clave `adm_cliente_id + numero_oc`). |
| 003 | `003_fix_bugs_sp.sql` | Corrige `sp_insertarCondicionPago`, `sp_mantenerOC`, `sp_eliminarOC`, `sp_eliminarCalificacionProveedor`, `sp_mantenerOCDet`, `sp_mantenerConstante`, `sp_validarOCPendiente`, `sp_buscarOC`, `sp_buscarProveedor`, `sp_listarUsuario`. |
| 004 | `004_indices.sql` | Índices de soporte y dos índices únicos filtrados (con verificación previa de duplicados y de opciones SET de los módulos). |
| 005 | `005_permisos_usuario_app.sql` | Rol `rol_app_consenso` con `EXECUTE` sobre dbo + `INSERT` sobre `tb_orden_compra_det` (único SQL ad-hoc de la app). Plantilla comentada de login/usuario. |
| 006 | `006_fks_opcional.sql` | **OPCIONAL.** FKs `tb_usuario→tb_rol`, `tb_rol_permiso→tb_rol/tb_permiso`, `tb_orden_compra_det→tb_orden_compra_cab`, solo si no hay huérfanos. |
| 007 | `007_complementos_ia.sql` | Complementos IA: tablas `tb_addon` (catálogo, 7 complementos; 3 disponibles), `tb_addon_activacion`, `tb_ia_credencial` (solo referencia al secreto de AWS Secrets Manager + últimos 4 caracteres; **la API key no se guarda en la BD**) y `tb_ia_uso`. SP `sp_listarAddon`, `sp_mantenerAddonActivacion` (THROW 50701), `sp_obtenerCredencialIA`, `sp_mantenerCredencialIA`, `sp_actualizarEstadoCredencialIA`, `sp_eliminarCredencialIA`. Verificación y rollback al final del script. |
| 008 | `008_plataforma_pagos_ia.sql` | Rol 6 "Administrador de plataforma" (proveedor del sistema) con sus permisos de menú `plataforma/pagos` y `plataforma/clave-ia`; tablas `tb_ia_saldo` y `tb_ia_recarga` (comprobantes, índice único por banco + referencia); SP de saldo, registro, listado, aprobación (acredita saldo en la misma transacción) y rechazo de recargas (THROW 50811-50813), y `sp_esUsuarioPlataforma`. Al final: plantilla para crear el primer administrador de plataforma. |
| 009 | `009_agentes_ia.sql` | Agentes IA en funcionamiento: todos los complementos disponibles más "Monitor de entregas" y "Previsión de cobros"; `tb_ia_uso.con_saldo`; `sp_registrarUsoIA` (registra cada consulta y descuenta el saldo si usó la clave de la plataforma); `sp_obtenerCredencialIA` separa el consumo con clave propia del consumo con saldo. |

### Detalle por procedimiento

**001**
- `sp_validarUsuario`: solo cambia `@clave varchar(50)` → `varchar(256)`. Mismo result set y outputs.
- `sp_mantenerUsuario`: `@clave varchar(256)`, `@codigo_sociedad varchar(18)` (igual que la columna). En UPDATE: `clave = CASE WHEN @clave IS NULL OR @clave = '' THEN clave ELSE @clave END`. Los dos UPDATE (usuario + idioma del proveedor) en una transacción.
- `sp_obtenerCredencialUsuario(@adm_cliente_id varchar(12), @codigo_sociedad varchar(18), @usuario varchar(50))` → `reg_id, clave` del usuario activo (`reg_estado='1'`).
- `sp_actualizarClaveUsuario(@reg_id int, @clave varchar(256))` → actualiza `clave`, `reg_fecha_modificacion = getdate()`, `reg_usuario_modificacion = '2'` (usuario sistema). Si el `reg_id` no existe no hace nada.
- `sp_listarUsuario`: se quita `clave` de ambos result sets (resto de columnas igual) y filtros LIKE tolerantes a NULL.

**002** – `sp_mantenerOC_cabecera`: ver el encabezado del script para el mapeo completo JSON → columnas.
Valores fijos iguales a `sp_mantenerOC` rama CREATE: `estado_confirmacion='Pendiente'`, `reg_estado='2'`, `reg_usuario_creacion='2'`.
**Importante:** el item JSON actual (`OCRepository.MapearCabeceras`) **no trae `sociedad`** (columna NOT NULL).
El SP toma `$.sociedad` o, si falta, `@p_sociedad`; los items sin sociedad se omiten. El backend debe agregar
`sociedad` al item o pasar `@p_sociedad`.

**003**
- `sp_insertarCondicionPago`: el UPDATE escribía `getdate()` en `reg_usuario_modificacion`; ahora setea `reg_fecha_modificacion = getdate()` y `reg_usuario_modificacion = ISNULL(@p_usuario,'2')`. Nuevo parámetro **opcional** `@p_usuario varchar(50) = NULL` (compatible con el C# actual).
- `sp_mantenerOC`: UPDATE/MODIFY y APPROVE/REFUSED en transacción; lectura de cabecera y de `MAX(reg_version)` con `UPDLOCK, HOLDLOCK`; `THROW 50301/50302` si el UPDATE de cabecera no afecta filas. CREATE/HISTORY sin cambios.
- `sp_eliminarOC`: borra detalle y cabecera en una transacción (detalle primero, compatible con la FK opcional).
- `sp_eliminarCalificacionProveedor`: una sola sentencia; se agrega `SET NOCOUNT/XACT_ABORT`.
- `sp_mantenerOCDet`: UPDATE sin filas → INSERT de la posición; sin errores nuevos.
- `sp_mantenerConstante`: `@descripcion varchar(100)` (largo real de la columna); aplica `@reg_estado` solo si es `'0'`/`'1'` (otro valor conserva el actual); `THROW 50320` si no existe.
- `sp_validarOCPendiente`: `MERGE ... WITH (HOLDLOCK)` dentro de transacción; excluye `codigo_proveedor IS NULL`; SELECT final filtrado por `adm_cliente_id`; `TRY_CONVERT` del intervalo.
- `sp_buscarOC`: filtros `(@x = '' OR @x IS NULL OR col LIKE '%'+@x+'%')`; join a `tb_condicion_pago` con `co.reg_estado = '1'` (evita duplicados); `reg_estado = '1'` en archivos (antes entero → conversión implícita).
- `sp_buscarProveedor`, `sp_listarUsuario`: mismos filtros LIKE tolerantes a NULL.
- `sp_listarConstantes`: sin cambios (el enmascarado de secretos se hace en C#).

Códigos de error propios: 50201 (002), 50301–50302 (sp_mantenerOC), 50320 (sp_mantenerConstante).

## Verificación (una consulta por script)

```sql
-- 001: columna ampliada y SP nuevos
SELECT c.max_length, c.is_nullable,
       OBJECT_ID('dbo.sp_obtenerCredencialUsuario') AS sp_cred,
       OBJECT_ID('dbo.sp_actualizarClaveUsuario')   AS sp_clave
FROM sys.columns c
WHERE c.object_id = OBJECT_ID('dbo.tb_usuario') AND c.name = 'clave';
-- Esperado: 256, 1, ids no NULL

-- 002: SP creado y parámetros
SELECT p.name, TYPE_NAME(p.user_type_id) AS tipo, p.max_length
FROM sys.parameters p WHERE p.object_id = OBJECT_ID('dbo.sp_mantenerOC_cabecera');
-- Esperado: @p_json nvarchar -1, @p_sociedad varchar 18

-- 003: SP modificados hoy y con SET NOCOUNT ON
SELECT o.name, o.modify_date,
       CASE WHEN m.definition LIKE '%SET NOCOUNT ON%' THEN 1 ELSE 0 END AS nocount
FROM sys.objects o JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.name IN ('sp_insertarCondicionPago','sp_mantenerOC','sp_eliminarOC','sp_eliminarCalificacionProveedor',
                 'sp_mantenerOCDet','sp_mantenerConstante','sp_validarOCPendiente','sp_buscarOC',
                 'sp_buscarProveedor','sp_listarUsuario')
ORDER BY o.name;

-- 004: índices
SELECT OBJECT_NAME(object_id) AS tabla, name, is_unique, has_filter, filter_definition
FROM sys.indexes
WHERE name IN ('IX_oc_cab_cli_soc_fecha','IX_log_cab_oc','IX_log_det_oc','IX_oc_arc_oc_soc',
               'UX_cond_pago_codigo','UX_usuario_login');
-- Si faltan UX_*: revisar los PRINT de advertencia (duplicados o módulos con QUOTED_IDENTIFIER OFF)

-- 005: permisos del rol
SELECT dp.permission_name, dp.state_desc, dp.class_desc,
       COALESCE(OBJECT_NAME(dp.major_id), SCHEMA_NAME(dp.major_id)) AS objeto
FROM sys.database_permissions dp
WHERE dp.grantee_principal_id = DATABASE_PRINCIPAL_ID('rol_app_consenso');
-- Esperado: EXECUTE/SCHEMA/dbo e INSERT/OBJECT/tb_orden_compra_det

-- 006 (opcional): FKs
SELECT name, OBJECT_NAME(parent_object_id) AS tabla, is_not_trusted, is_disabled
FROM sys.foreign_keys
WHERE name IN ('FK_usuario_rol','FK_rol_permiso_rol','FK_rol_permiso_permiso','FK_oc_det_cab');
-- Esperado: is_not_trusted = 0
```

## Notas importantes

- **Índices filtrados (004):** una vez creados, todo SP que modifique `tb_usuario` o `tb_condicion_pago`
  debe estar compilado con `QUOTED_IDENTIFIER ON` y `ANSI_NULLS ON` (si no, error 1934 en ejecución).
  El script lo verifica y **no crea** el índice si encuentra módulos con esas opciones en OFF
  (p.ej. `sp_generarUsuario`, `sp_mantenerProveedor`, `sp_eliminarCondicionPago`, `sp_obtenerRutaRepositorio`
  no se recrean en estos scripts). En ese caso recrear esos módulos con
  `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;` (misma definición) y re-ejecutar 004.
- **Duplicados (004):** si hay duplicados el índice único no se crea; usar las consultas de pre-check
  comentadas en el script, depurar y re-ejecutar.
- Creación de índices en Standard Edition es OFFLINE: ejecutar en ventana de bajo uso.
- **Claves existentes en texto plano:** el ALTER de la columna no las transforma. La migración a hash
  la hace el backend (p.ej. al iniciar sesión, vía `sp_obtenerCredencialUsuario` + `sp_actualizarClaveUsuario`).

## Rollback

Antes de ejecutar, generar el script de los objetos actuales (SSMS → Tasks → Generate Scripts, o
`SELECT definition FROM sys.sql_modules WHERE object_id = OBJECT_ID('dbo.<sp>')`) y guardarlo.

| Script | Rollback |
|--------|----------|
| 001 | Restaurar definiciones previas de `sp_validarUsuario`, `sp_mantenerUsuario`, `sp_listarUsuario`. `DROP PROCEDURE dbo.sp_obtenerCredencialUsuario, dbo.sp_actualizarClaveUsuario;`. **No** volver la columna a `varchar(50)` si ya hay hashes guardados (se truncarían / fallaría el ALTER). |
| 002 | `DROP PROCEDURE dbo.sp_mantenerOC_cabecera;` (las OC insertadas son datos válidos; no se revierten). |
| 003 | Restaurar las definiciones previas guardadas de cada SP. |
| 004 | `DROP INDEX <nombre> ON dbo.<tabla>;` para cada índice creado. |
| 005 | `ALTER ROLE rol_app_consenso DROP MEMBER <usuario>;` y luego `DROP ROLE rol_app_consenso;` |
| 006 | `ALTER TABLE dbo.<tabla> DROP CONSTRAINT <FK>;` |

Si se revierte la BD, el backend nuevo también debe revertirse (depende de 001 y 002).
