/* =====================================================================
   004_indices.sql
   ---------------------------------------------------------------------
   Objetivo : Indices de soporte para consultas frecuentes y unicidad.
   Motor    : SQL Server 2019
   Idempotente: SI (IF NOT EXISTS por nombre de indice).
   Nota     : En Standard Edition la creacion es OFFLINE (bloquea la tabla
              mientras se construye). Ejecutar en ventana de bajo uso.
              En Enterprise puede agregarse WITH (ONLINE = ON).
   Columnas verificadas contra columns.csv (export QAS): todas existen.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

/* ---------------------------------------------------------------------
   1) Busqueda de OC (sp_buscarOC operador 'B')
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_oc_cab_cli_soc_fecha' AND object_id = OBJECT_ID(N'dbo.tb_orden_compra_cab'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_oc_cab_cli_soc_fecha
        ON dbo.tb_orden_compra_cab (adm_cliente_id, sociedad, fecha_emision)
        INCLUDE (numero_oc, codigo_proveedor, tipo_oc, condicion_pago, estado_confirmacion, reg_estado);
    PRINT 'Creado IX_oc_cab_cli_soc_fecha.';
END
ELSE PRINT 'IX_oc_cab_cli_soc_fecha ya existe.';
GO

/* ---------------------------------------------------------------------
   2) Historicos (sp_buscarOC 'HC'/'HD' y MAX(reg_version) en sp_mantenerOC)
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_log_cab_oc' AND object_id = OBJECT_ID(N'dbo.log_orden_compra_cab'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_log_cab_oc
        ON dbo.log_orden_compra_cab (adm_cliente_id, numero_oc, sociedad, reg_version);
    PRINT 'Creado IX_log_cab_oc.';
END
ELSE PRINT 'IX_log_cab_oc ya existe.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_log_det_oc' AND object_id = OBJECT_ID(N'dbo.log_orden_compra_det'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_log_det_oc
        ON dbo.log_orden_compra_det (adm_cliente_id, numero_oc, reg_version);
    PRINT 'Creado IX_log_det_oc.';
END
ELSE PRINT 'IX_log_det_oc ya existe.';
GO

/* ---------------------------------------------------------------------
   3) Archivos por OC (sp_listarArchivoOC y subconsulta de sp_buscarOC)
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_oc_arc_oc_soc' AND object_id = OBJECT_ID(N'dbo.tb_orden_compra_arc'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_oc_arc_oc_soc
        ON dbo.tb_orden_compra_arc (numero_oc, sociedad)
        INCLUDE (reg_estado);
    PRINT 'Creado IX_oc_arc_oc_soc.';
END
ELSE PRINT 'IX_oc_arc_oc_soc ya existe.';
GO

/* ---------------------------------------------------------------------
   4) Indices UNICOS FILTRADOS
   IMPORTANTE: con un indice filtrado, TODO procedimiento que haga
   INSERT/UPDATE/DELETE sobre la tabla debe haber sido creado con
   QUOTED_IDENTIFIER ON y ANSI_NULLS ON; si no, fallara en tiempo de
   ejecucion (error 1934). Por eso, antes de crear cada indice se verifica
   que ningun modulo que referencia la tabla tenga esas opciones en OFF.
   Si hay alguno, NO se crea el indice y se listan los modulos: hay que
   recrearlos (script del objeto con SET QUOTED_IDENTIFIER ON / SET ANSI_NULLS ON)
   y volver a ejecutar este script.

   Pre-check manual de duplicados (ejecutar y revisar antes):

   -- tb_condicion_pago: codigos activos duplicados
   -- SELECT codigo, COUNT(*) AS cantidad, STRING_AGG(CAST(reg_id AS varchar(12)), ',') AS reg_ids
   -- FROM dbo.tb_condicion_pago
   -- WHERE reg_estado = '1'
   -- GROUP BY codigo
   -- HAVING COUNT(*) > 1;

   -- tb_usuario: logins duplicados por cliente + sociedad
   -- SELECT adm_cliente_id, codigo_sociedad, usuario, COUNT(*) AS cantidad,
   --        STRING_AGG(CAST(reg_id AS varchar(12)), ',') AS reg_ids
   -- FROM dbo.tb_usuario
   -- WHERE usuario <> ''
   -- GROUP BY adm_cliente_id, codigo_sociedad, usuario
   -- HAVING COUNT(*) > 1;
   --------------------------------------------------------------------- */

-- 4.a) UX_cond_pago_codigo
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_cond_pago_codigo' AND object_id = OBJECT_ID(N'dbo.tb_condicion_pago'))
BEGIN
    IF EXISTS (
        SELECT codigo FROM dbo.tb_condicion_pago
        WHERE reg_estado = '1'
        GROUP BY codigo HAVING COUNT(*) > 1
    )
        PRINT 'ADVERTENCIA: existen codigos duplicados activos en tb_condicion_pago. UX_cond_pago_codigo NO se crea. Depure con el pre-check y vuelva a ejecutar.';
    ELSE IF EXISTS (
        SELECT 1
        FROM sys.sql_expression_dependencies d
        JOIN sys.sql_modules m ON m.object_id = d.referencing_id
        WHERE d.referenced_id = OBJECT_ID(N'dbo.tb_condicion_pago')
          AND (m.uses_quoted_identifier = 0 OR m.uses_ansi_nulls = 0)
    )
    BEGIN
        PRINT 'ADVERTENCIA: hay modulos que referencian tb_condicion_pago creados con QUOTED_IDENTIFIER/ANSI_NULLS OFF. UX_cond_pago_codigo NO se crea. Modulos:';
        SELECT OBJECT_SCHEMA_NAME(m.object_id) + '.' + OBJECT_NAME(m.object_id) AS modulo,
               m.uses_quoted_identifier, m.uses_ansi_nulls
        FROM sys.sql_expression_dependencies d
        JOIN sys.sql_modules m ON m.object_id = d.referencing_id
        WHERE d.referenced_id = OBJECT_ID(N'dbo.tb_condicion_pago')
          AND (m.uses_quoted_identifier = 0 OR m.uses_ansi_nulls = 0);
    END
    ELSE
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX UX_cond_pago_codigo
            ON dbo.tb_condicion_pago (codigo)
            WHERE reg_estado = '1';
        PRINT 'Creado UX_cond_pago_codigo.';
    END
END
ELSE PRINT 'UX_cond_pago_codigo ya existe.';
GO

-- 4.b) UX_usuario_login
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_usuario_login' AND object_id = OBJECT_ID(N'dbo.tb_usuario'))
BEGIN
    IF EXISTS (
        SELECT 1 FROM dbo.tb_usuario
        WHERE usuario <> ''
        GROUP BY adm_cliente_id, codigo_sociedad, usuario HAVING COUNT(*) > 1
    )
        PRINT 'ADVERTENCIA: existen usuarios (login) duplicados en tb_usuario. UX_usuario_login NO se crea. Depure con el pre-check y vuelva a ejecutar.';
    ELSE IF EXISTS (
        SELECT 1
        FROM sys.sql_expression_dependencies d
        JOIN sys.sql_modules m ON m.object_id = d.referencing_id
        WHERE d.referenced_id = OBJECT_ID(N'dbo.tb_usuario')
          AND (m.uses_quoted_identifier = 0 OR m.uses_ansi_nulls = 0)
    )
    BEGIN
        PRINT 'ADVERTENCIA: hay modulos que referencian tb_usuario creados con QUOTED_IDENTIFIER/ANSI_NULLS OFF. UX_usuario_login NO se crea. Modulos:';
        SELECT OBJECT_SCHEMA_NAME(m.object_id) + '.' + OBJECT_NAME(m.object_id) AS modulo,
               m.uses_quoted_identifier, m.uses_ansi_nulls
        FROM sys.sql_expression_dependencies d
        JOIN sys.sql_modules m ON m.object_id = d.referencing_id
        WHERE d.referenced_id = OBJECT_ID(N'dbo.tb_usuario')
          AND (m.uses_quoted_identifier = 0 OR m.uses_ansi_nulls = 0);
    END
    ELSE
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX UX_usuario_login
            ON dbo.tb_usuario (adm_cliente_id, codigo_sociedad, usuario)
            WHERE usuario <> '';
        PRINT 'Creado UX_usuario_login.';
    END
END
ELSE PRINT 'UX_usuario_login ya existe.';
GO

PRINT '004_indices.sql aplicado.';
GO
