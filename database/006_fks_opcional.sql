/* =====================================================================
   006_fks_opcional.sql   (OPCIONAL)
   ---------------------------------------------------------------------
   Objetivo : Agregar integridad referencial (hoy la BD no tiene ninguna FK).
   Motor    : SQL Server 2019
   Idempotente: SI (IF NOT EXISTS por nombre de FK).
   Cada FK se crea WITH CHECK solo si NO hay huerfanos; si los hay se omite
   (PRINT) y se muestra la consulta para identificarlos.

   FKs:
     FK_usuario_rol           tb_usuario(rol_id)                      -> tb_rol(reg_id)
     FK_rol_permiso_rol       tb_rol_permiso(rol_id)                  -> tb_rol(reg_id)
     FK_rol_permiso_permiso   tb_rol_permiso(permiso_id)              -> tb_permiso(reg_id)
     FK_oc_det_cab            tb_orden_compra_det(adm_cliente_id, numero_oc)
                              -> tb_orden_compra_cab(adm_cliente_id, numero_oc)
                              (clave unica UQ existente en cabecera)

   Impacto funcional a considerar ANTES de aplicar:
     - FK_oc_det_cab: la cabecera debe existir antes de insertar detalle y el
       detalle debe borrarse antes que la cabecera. sp_eliminarOC (003) ya
       borra detalle -> cabecera. El flujo actual de OCRepository inserta
       cabecera (sp_mantenerOC) y luego detalle. Cualquier proceso externo
       (cargas manuales, ETL) que inserte detalle sin cabecera fallara.
     - FK_usuario_rol: rol_id debe existir en tb_rol (NULL permitido).
     - Borrar un rol/permiso en uso fallara (NO ACTION). Es el objetivo.
   Rollback: ALTER TABLE <tabla> DROP CONSTRAINT <FK>;
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   FK_usuario_rol
   Huerfanos:
   -- SELECT u.reg_id, u.usuario, u.rol_id FROM dbo.tb_usuario u
   -- WHERE u.rol_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.tb_rol r WHERE r.reg_id = u.rol_id);
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.FK_usuario_rol', N'F') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.tb_usuario u
               WHERE u.rol_id IS NOT NULL
                 AND NOT EXISTS (SELECT 1 FROM dbo.tb_rol r WHERE r.reg_id = u.rol_id))
        PRINT 'ADVERTENCIA: tb_usuario tiene rol_id huerfanos. FK_usuario_rol NO se crea.';
    ELSE
    BEGIN
        ALTER TABLE dbo.tb_usuario WITH CHECK
            ADD CONSTRAINT FK_usuario_rol FOREIGN KEY (rol_id) REFERENCES dbo.tb_rol (reg_id);
        PRINT 'Creada FK_usuario_rol.';
    END
END
ELSE PRINT 'FK_usuario_rol ya existe.';
GO

/* ---------------------------------------------------------------------
   FK_rol_permiso_rol
   Huerfanos:
   -- SELECT rp.* FROM dbo.tb_rol_permiso rp
   -- WHERE NOT EXISTS (SELECT 1 FROM dbo.tb_rol r WHERE r.reg_id = rp.rol_id);
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.FK_rol_permiso_rol', N'F') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.tb_rol_permiso rp
               WHERE NOT EXISTS (SELECT 1 FROM dbo.tb_rol r WHERE r.reg_id = rp.rol_id))
        PRINT 'ADVERTENCIA: tb_rol_permiso tiene rol_id huerfanos. FK_rol_permiso_rol NO se crea.';
    ELSE
    BEGIN
        ALTER TABLE dbo.tb_rol_permiso WITH CHECK
            ADD CONSTRAINT FK_rol_permiso_rol FOREIGN KEY (rol_id) REFERENCES dbo.tb_rol (reg_id);
        PRINT 'Creada FK_rol_permiso_rol.';
    END
END
ELSE PRINT 'FK_rol_permiso_rol ya existe.';
GO

/* ---------------------------------------------------------------------
   FK_rol_permiso_permiso
   Huerfanos:
   -- SELECT rp.* FROM dbo.tb_rol_permiso rp
   -- WHERE NOT EXISTS (SELECT 1 FROM dbo.tb_permiso p WHERE p.reg_id = rp.permiso_id);
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.FK_rol_permiso_permiso', N'F') IS NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.tb_rol_permiso rp
               WHERE NOT EXISTS (SELECT 1 FROM dbo.tb_permiso p WHERE p.reg_id = rp.permiso_id))
        PRINT 'ADVERTENCIA: tb_rol_permiso tiene permiso_id huerfanos. FK_rol_permiso_permiso NO se crea.';
    ELSE
    BEGIN
        ALTER TABLE dbo.tb_rol_permiso WITH CHECK
            ADD CONSTRAINT FK_rol_permiso_permiso FOREIGN KEY (permiso_id) REFERENCES dbo.tb_permiso (reg_id);
        PRINT 'Creada FK_rol_permiso_permiso.';
    END
END
ELSE PRINT 'FK_rol_permiso_permiso ya existe.';
GO

/* ---------------------------------------------------------------------
   FK_oc_det_cab
   Requiere una clave unica en tb_orden_compra_cab(adm_cliente_id, numero_oc)
   (existe en QAS: UQ__tb_orden__06966469C6CDBF70).
   Huerfanos:
   -- SELECT d.adm_cliente_id, d.numero_oc, COUNT(*) AS posiciones
   -- FROM dbo.tb_orden_compra_det d
   -- WHERE NOT EXISTS (SELECT 1 FROM dbo.tb_orden_compra_cab c
   --                   WHERE c.adm_cliente_id = d.adm_cliente_id AND c.numero_oc = d.numero_oc)
   -- GROUP BY d.adm_cliente_id, d.numero_oc;
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.FK_oc_det_cab', N'F') IS NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(N'dbo.tb_orden_compra_cab')
          AND i.is_unique = 1
          AND i.has_filter = 0
          AND (SELECT COUNT(*) FROM sys.index_columns ic
               WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0) = 2
          AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c
                        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = N'adm_cliente_id')
          AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c
                        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = N'numero_oc')
    )
        PRINT 'ADVERTENCIA: no existe clave unica (adm_cliente_id, numero_oc) en tb_orden_compra_cab. FK_oc_det_cab NO se crea.';
    ELSE IF EXISTS (SELECT 1 FROM dbo.tb_orden_compra_det d
                    WHERE NOT EXISTS (SELECT 1 FROM dbo.tb_orden_compra_cab c
                                      WHERE c.adm_cliente_id = d.adm_cliente_id AND c.numero_oc = d.numero_oc))
        PRINT 'ADVERTENCIA: tb_orden_compra_det tiene posiciones sin cabecera. FK_oc_det_cab NO se crea.';
    ELSE
    BEGIN
        ALTER TABLE dbo.tb_orden_compra_det WITH CHECK
            ADD CONSTRAINT FK_oc_det_cab FOREIGN KEY (adm_cliente_id, numero_oc)
                REFERENCES dbo.tb_orden_compra_cab (adm_cliente_id, numero_oc);
        PRINT 'Creada FK_oc_det_cab.';
    END
END
ELSE PRINT 'FK_oc_det_cab ya existe.';
GO

PRINT '006_fks_opcional.sql aplicado.';
GO
