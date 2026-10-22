/* =====================================================================
   008_plataforma_pagos_ia.sql
   ---------------------------------------------------------------------
   Objetivo : Administración de plataforma (el proveedor del sistema) y
              recargas de saldo IA validadas a partir de un comprobante.
              - Rol 6 "Administrador de plataforma" (tipo_rol 'plataforma'):
                no es un rol de la empresa cliente; solo ve /plataforma.
              - tb_ia_saldo    saldo prepagado por cliente + sociedad (US$)
              - tb_ia_recarga  comprobantes subidos por el cliente y su validación
              La clave de IA de la plataforma usa tb_ia_credencial con
              adm_cliente_id = codigo_sociedad = 'PLATAFORMA'.
   Requiere : 007_complementos_ia.sql
   Motor    : SQL Server 2019
   Idempotente: SI.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   1) Rol, permisos de menú y su asignación
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.tb_rol WHERE reg_id = 6)
BEGIN
    SET IDENTITY_INSERT dbo.tb_rol ON;
    INSERT INTO dbo.tb_rol (reg_id, adm_cliente_id, codigo_sociedad, codigo_referencia, codigo_referencia_aux,
                            nombre_rol, nombre_rolEN, tipo_rol, reg_estado, reg_fecha_creacion, reg_usuario_creacion)
    VALUES (6, '1001', '0', '0', '0', 'Administrador de plataforma', 'Platform administrator', 'plataforma', '1', getdate(), '2');
    SET IDENTITY_INSERT dbo.tb_rol OFF;
    PRINT 'Rol 6 (Administrador de plataforma) creado.';
END
ELSE IF NOT EXISTS (SELECT 1 FROM dbo.tb_rol WHERE reg_id = 6 AND tipo_rol = 'plataforma')
    THROW 50801, 'El rol 6 ya existe y no es el de plataforma. Revisar antes de continuar.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.tb_permiso WHERE adm_cliente_id = '1001' AND uri_permiso = 'plataforma/pagos')
    INSERT INTO dbo.tb_permiso (adm_cliente_id, nombre_permiso, uri_permiso, reg_estado, reg_fecha_creacion, reg_usuario_creacion)
    VALUES ('1001', 'menu.pagos_ia', 'plataforma/pagos', '1', getdate(), '2');
IF NOT EXISTS (SELECT 1 FROM dbo.tb_permiso WHERE adm_cliente_id = '1001' AND uri_permiso = 'plataforma/clave-ia')
    INSERT INTO dbo.tb_permiso (adm_cliente_id, nombre_permiso, uri_permiso, reg_estado, reg_fecha_creacion, reg_usuario_creacion)
    VALUES ('1001', 'menu.clave_plataforma', 'plataforma/clave-ia', '1', getdate(), '2');
GO

INSERT INTO dbo.tb_rol_permiso (adm_cliente_id, rol_id, permiso_id, reg_estado, reg_fecha_creacion, reg_usuario_creacion)
SELECT '1001', 6, p.reg_id, '1', getdate(), '2'
FROM dbo.tb_permiso p
WHERE p.adm_cliente_id = '1001' AND p.uri_permiso IN ('plataforma/pagos', 'plataforma/clave-ia')
  AND NOT EXISTS (SELECT 1 FROM dbo.tb_rol_permiso rp WHERE rp.rol_id = 6 AND rp.permiso_id = p.reg_id);
GO

/* ---------------------------------------------------------------------
   2) Tablas
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.tb_ia_saldo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_saldo (
        adm_cliente_id           varchar(12)   NOT NULL,
        codigo_sociedad          varchar(18)   NOT NULL,
        saldo_usd                decimal(12,4) NOT NULL CONSTRAINT DF_ia_saldo DEFAULT (0),
        reg_fecha_modificacion   datetime      NOT NULL CONSTRAINT DF_ia_saldo_fm DEFAULT (getdate()),
        reg_usuario_modificacion varchar(50)   NULL,
        CONSTRAINT PK_ia_saldo PRIMARY KEY (adm_cliente_id, codigo_sociedad)
    );
    PRINT 'Tabla tb_ia_saldo creada.';
END
GO

IF OBJECT_ID(N'dbo.tb_ia_recarga', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_recarga (
        reg_id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ia_recarga PRIMARY KEY,
        adm_cliente_id    varchar(12)    NOT NULL,
        codigo_sociedad   varchar(18)    NOT NULL,
        monto_usd         decimal(10,2)  NOT NULL,           -- lo que declara el cliente
        monto_aprobado    decimal(10,2)  NULL,               -- lo que acredita la plataforma
        banco             nvarchar(60)   NOT NULL,
        referencia        varchar(60)    NOT NULL,           -- n.º de transferencia o factura
        fecha_pago        date           NOT NULL,
        archivo_ruta      varchar(400)   NOT NULL,
        archivo_nombre    nvarchar(200)  NOT NULL,
        estado            varchar(12)    NOT NULL CONSTRAINT DF_ia_recarga_estado DEFAULT ('pendiente'), -- pendiente | aprobada | rechazada
        motivo_rechazo    nvarchar(300)  NULL,
        usuario_solicita  varchar(50)    NOT NULL,
        fecha_solicitud   datetime       NOT NULL CONSTRAINT DF_ia_recarga_fs DEFAULT (getdate()),
        usuario_valida    varchar(50)    NULL,
        fecha_validacion  datetime       NULL,
        CONSTRAINT CK_ia_recarga_estado CHECK (estado IN ('pendiente', 'aprobada', 'rechazada')),
        CONSTRAINT CK_ia_recarga_monto CHECK (monto_usd > 0)
    );
    -- Un mismo comprobante no se puede usar dos veces (salvo que se haya rechazado)
    CREATE UNIQUE INDEX UX_ia_recarga_referencia ON dbo.tb_ia_recarga (banco, referencia) WHERE estado <> 'rechazada';
    CREATE INDEX IX_ia_recarga_estado ON dbo.tb_ia_recarga (estado, fecha_solicitud);
    PRINT 'Tabla tb_ia_recarga creada.';
END
GO

/* ---------------------------------------------------------------------
   3) Procedimientos
   --------------------------------------------------------------------- */

-- ¿El usuario es de plataforma? (para que un administrador de empresa no lo edite)
CREATE OR ALTER PROCEDURE dbo.sp_esUsuarioPlataforma
    @reg_id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.tb_usuario WHERE reg_id = @reg_id AND rol_id = 6) THEN 1 ELSE 0 END AS bit) AS es_plataforma;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_obtenerSaldoIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL((SELECT saldo_usd FROM dbo.tb_ia_saldo
                   WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad), 0) AS saldo_usd,
           (SELECT COUNT(*) FROM dbo.tb_ia_recarga
             WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad AND estado = 'pendiente') AS recargas_pendientes;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_registrarRecargaIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @monto_usd       decimal(10,2),
    @banco           nvarchar(60),
    @referencia      varchar(60),
    @fecha_pago      date,
    @archivo_ruta    varchar(400),
    @archivo_nombre  nvarchar(200),
    @usuario         varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.tb_ia_recarga WHERE banco = @banco AND referencia = @referencia AND estado <> 'rechazada')
        THROW 50811, 'Ese comprobante ya fue registrado.', 1;

    INSERT INTO dbo.tb_ia_recarga (adm_cliente_id, codigo_sociedad, monto_usd, banco, referencia, fecha_pago,
                                   archivo_ruta, archivo_nombre, usuario_solicita)
    VALUES (@adm_cliente_id, @codigo_sociedad, @monto_usd, @banco, @referencia, @fecha_pago,
            @archivo_ruta, @archivo_nombre, @usuario);
    SELECT CAST(SCOPE_IDENTITY() AS int) AS reg_id;
END
GO

-- @codigo_sociedad NULL = todas (plataforma). @estado NULL o '' = todos.
CREATE OR ALTER PROCEDURE dbo.sp_listarRecargaIA
    @adm_cliente_id  varchar(12) = NULL,
    @codigo_sociedad varchar(18) = NULL,
    @estado          varchar(12) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (500) r.reg_id, r.adm_cliente_id, r.codigo_sociedad, s.razon_social, r.monto_usd, r.monto_aprobado,
           r.banco, r.referencia, r.fecha_pago, r.archivo_nombre, r.estado, r.motivo_rechazo,
           r.usuario_solicita, r.fecha_solicitud, r.usuario_valida, r.fecha_validacion
    FROM dbo.tb_ia_recarga r
    OUTER APPLY (SELECT TOP 1 so.denominacion_sociedad AS razon_social
                 FROM dbo.tb_sociedad so
                 WHERE so.adm_cliente_id = r.adm_cliente_id AND so.codigo_sociedad = r.codigo_sociedad) s
    WHERE (@adm_cliente_id IS NULL OR r.adm_cliente_id = @adm_cliente_id)
      AND (@codigo_sociedad IS NULL OR r.codigo_sociedad = @codigo_sociedad)
      AND (@estado IS NULL OR @estado = '' OR r.estado = @estado)
    ORDER BY CASE WHEN r.estado = 'pendiente' THEN 0 ELSE 1 END, r.fecha_solicitud DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_obtenerRecargaIA
    @reg_id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT reg_id, adm_cliente_id, codigo_sociedad, monto_usd, estado, archivo_ruta, archivo_nombre
    FROM dbo.tb_ia_recarga WHERE reg_id = @reg_id;
END
GO

-- Aprueba una recarga pendiente y acredita el saldo en la misma transacción
CREATE OR ALTER PROCEDURE dbo.sp_aprobarRecargaIA
    @reg_id         int,
    @monto_aprobado decimal(10,2),
    @usuario        varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @monto_aprobado IS NULL OR @monto_aprobado <= 0
        THROW 50813, 'El monto aprobado debe ser mayor a 0.', 1;

    BEGIN TRAN;
    DECLARE @adm varchar(12), @soc varchar(18);
    UPDATE dbo.tb_ia_recarga WITH (UPDLOCK, HOLDLOCK)
       SET estado = 'aprobada', monto_aprobado = @monto_aprobado, usuario_valida = @usuario, fecha_validacion = getdate(),
           @adm = adm_cliente_id, @soc = codigo_sociedad
     WHERE reg_id = @reg_id AND estado = 'pendiente';

    IF @@ROWCOUNT = 0
        THROW 50812, 'La recarga no existe o ya fue validada.', 1;

    MERGE dbo.tb_ia_saldo WITH (HOLDLOCK) AS t
    USING (SELECT @adm AS adm_cliente_id, @soc AS codigo_sociedad) AS s
       ON t.adm_cliente_id = s.adm_cliente_id AND t.codigo_sociedad = s.codigo_sociedad
    WHEN MATCHED THEN UPDATE SET saldo_usd = t.saldo_usd + @monto_aprobado, reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
    WHEN NOT MATCHED THEN INSERT (adm_cliente_id, codigo_sociedad, saldo_usd, reg_usuario_modificacion)
         VALUES (@adm, @soc, @monto_aprobado, @usuario);
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_rechazarRecargaIA
    @reg_id  int,
    @motivo  nvarchar(300),
    @usuario varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tb_ia_recarga
       SET estado = 'rechazada', motivo_rechazo = @motivo, usuario_valida = @usuario, fecha_validacion = getdate()
     WHERE reg_id = @reg_id AND estado = 'pendiente';

    IF @@ROWCOUNT = 0
        THROW 50812, 'La recarga no existe o ya fue validada.', 1;
END
GO

/* ---------------------------------------------------------------------
   4) Permisos del usuario de la app (si tiene permisos objeto por objeto)
        GRANT EXECUTE ON OBJECT::dbo.sp_esUsuarioPlataforma TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_obtenerSaldoIA      TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_registrarRecargaIA  TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_listarRecargaIA     TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_obtenerRecargaIA    TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_aprobarRecargaIA    TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_rechazarRecargaIA   TO <usuario_app>;
   --------------------------------------------------------------------- */
IF DATABASE_PRINCIPAL_ID(N'rol_app_consenso') IS NOT NULL
    GRANT EXECUTE ON SCHEMA::dbo TO rol_app_consenso;
GO

PRINT '008_plataforma_pagos_ia.sql aplicado.';
GO

/* ---------------------------------------------------------------------
   Crear el primer administrador de plataforma (no se puede desde la
   pantalla de Usuarios: el backend rechaza el rol 6 ahí).
   codigo_sociedad: la sociedad que elegirá en el login (el rol le da
   acceso a todas). La clave en texto plano se convierte a hash en el
   primer inicio de sesión; cámbiela apenas ingrese.

   INSERT INTO dbo.tb_usuario (adm_cliente_id, codigo_sociedad, codigo_referencia, codigo_referencia_aux,
                               usuario, clave, rol_id, denominacion_usuario, correo_referencia,
                               reg_estado, reg_fecha_creacion, reg_usuario_creacion)
   VALUES ('1001', 'EC01', '0', '0', '<usuario>', '<clave_temporal>', 6, '<nombre>', '<correo>', '1', getdate(), '2');

   Verificación:
   SELECT * FROM dbo.tb_rol WHERE reg_id = 6;
   SELECT p.uri_permiso FROM dbo.tb_rol_permiso rp JOIN dbo.tb_permiso p ON p.reg_id = rp.permiso_id WHERE rp.rol_id = 6;
   SELECT OBJECT_ID('dbo.tb_ia_saldo') t1, OBJECT_ID('dbo.tb_ia_recarga') t2;

   Rollback (sin recargas aprobadas):
   DROP PROCEDURE dbo.sp_esUsuarioPlataforma, dbo.sp_obtenerSaldoIA, dbo.sp_registrarRecargaIA, dbo.sp_listarRecargaIA,
                  dbo.sp_obtenerRecargaIA, dbo.sp_aprobarRecargaIA, dbo.sp_rechazarRecargaIA;
   DROP TABLE dbo.tb_ia_recarga, dbo.tb_ia_saldo;
   DELETE dbo.tb_rol_permiso WHERE rol_id = 6;
   DELETE dbo.tb_permiso WHERE uri_permiso IN ('plataforma/pagos', 'plataforma/clave-ia');
   DELETE dbo.tb_rol WHERE reg_id = 6;   -- solo si no hay usuarios con rol 6
*/
