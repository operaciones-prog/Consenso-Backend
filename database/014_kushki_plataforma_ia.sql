/* =====================================================================
   014_kushki_plataforma_ia.sql
   ---------------------------------------------------------------------
   Objetivo : Las llaves de Kushki (pago con tarjeta de los planes de IA)
              las configura el administrador de plataforma (rol 6) desde
              Plataforma > Pago con tarjeta, como la clave de IA.
              - tb_ia_config_kushki: fila única (id = 1) con el modo
                (prueba/producción), la llave pública y la REFERENCIA al
                secreto de AWS Secrets Manager que guarda la llave privada
                y el id de firma de webhooks. Las llaves secretas nunca se
                guardan en la BD: solo sus últimos 4 caracteres.
              - Menú: permiso plataforma/pago-tarjeta para el rol 6.
              Sin fila, el backend usa Kushki:* de la configuración (respaldo).
   Requiere : 008 y 013.
   Motor    : SQL Server 2019
   Idempotente: SI.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   1) Menú del administrador de plataforma
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.tb_permiso WHERE adm_cliente_id = '1001' AND uri_permiso = 'plataforma/pago-tarjeta')
    INSERT INTO dbo.tb_permiso (adm_cliente_id, nombre_permiso, uri_permiso, reg_estado, reg_fecha_creacion, reg_usuario_creacion)
    VALUES ('1001', 'menu.pago_tarjeta', 'plataforma/pago-tarjeta', '1', getdate(), '2');
GO

INSERT INTO dbo.tb_rol_permiso (adm_cliente_id, rol_id, permiso_id, reg_estado, reg_fecha_creacion, reg_usuario_creacion)
SELECT '1001', 6, p.reg_id, '1', getdate(), '2'
FROM dbo.tb_permiso p
WHERE p.adm_cliente_id = '1001' AND p.uri_permiso = 'plataforma/pago-tarjeta'
  AND NOT EXISTS (SELECT 1 FROM dbo.tb_rol_permiso rp WHERE rp.rol_id = 6 AND rp.permiso_id = p.reg_id);
GO

/* ---------------------------------------------------------------------
   2) Tabla
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.tb_ia_config_kushki', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_config_kushki (
        id                       int          NOT NULL CONSTRAINT PK_ia_config_kushki PRIMARY KEY
                                                       CONSTRAINT CK_ia_config_kushki_unica CHECK (id = 1),
        prueba                   bit          NOT NULL,           -- 1 = ambiente de pruebas (api-uat)
        llave_publica            varchar(100) NOT NULL,           -- Public-Merchant-Id (no es secreta)
        secreto_ref              varchar(300) NOT NULL,           -- nombre del secreto en AWS Secrets Manager
        sufijo_privada           varchar(4)   NOT NULL,
        sufijo_webhook           varchar(4)   NULL,               -- NULL = aún sin id de firma de webhooks
        estado                   varchar(15)  NOT NULL CONSTRAINT CK_ia_config_kushki_estado CHECK (estado IN ('verificada', 'error')),
        fecha_verificacion       datetime     NULL,
        reg_fecha_modificacion   datetime     NOT NULL CONSTRAINT DF_ia_config_kushki_fm DEFAULT (getdate()),
        reg_usuario_modificacion varchar(50)  NULL
    );
    PRINT 'Tabla tb_ia_config_kushki creada.';
END
GO

/* ---------------------------------------------------------------------
   3) Procedimientos
   --------------------------------------------------------------------- */
-- La configuración (sin fila si no hay) y cuántas suscripciones siguen vivas en Kushki:
-- con suscripciones vivas no se puede cambiar de modo ni eliminar (sus cobros dejarían de llegar).
CREATE OR ALTER PROCEDURE dbo.sp_obtenerConfigKushkiIA
AS
BEGIN
    SET NOCOUNT ON;
    SELECT prueba, llave_publica, secreto_ref, sufijo_privada, sufijo_webhook, estado, fecha_verificacion,
           reg_fecha_modificacion, reg_usuario_modificacion
    FROM dbo.tb_ia_config_kushki WHERE id = 1;

    SELECT COUNT(*) AS suscripciones_vivas FROM dbo.tb_ia_suscripcion WHERE kushki_suscripcion_id IS NOT NULL;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_guardarConfigKushkiIA
    @prueba         bit,
    @llave_publica  varchar(100),
    @secreto_ref    varchar(300),
    @sufijo_privada varchar(4),
    @sufijo_webhook varchar(4) = NULL,
    @usuario        varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    UPDATE dbo.tb_ia_config_kushki WITH (UPDLOCK, HOLDLOCK)
       SET prueba = @prueba, llave_publica = @llave_publica, secreto_ref = @secreto_ref,
           sufijo_privada = @sufijo_privada, sufijo_webhook = @sufijo_webhook,
           estado = 'verificada', fecha_verificacion = getdate(),
           reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
     WHERE id = 1;
    IF @@ROWCOUNT = 0
        INSERT INTO dbo.tb_ia_config_kushki (id, prueba, llave_publica, secreto_ref, sufijo_privada, sufijo_webhook,
                                             estado, fecha_verificacion, reg_usuario_modificacion)
        VALUES (1, @prueba, @llave_publica, @secreto_ref, @sufijo_privada, @sufijo_webhook, 'verificada', getdate(), @usuario);
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_actualizarEstadoConfigKushkiIA
    @estado varchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tb_ia_config_kushki SET estado = @estado, fecha_verificacion = getdate() WHERE id = 1;
END
GO

-- Devuelve secreto_ref para borrar el secreto (NULL si no había configuración).
CREATE OR ALTER PROCEDURE dbo.sp_eliminarConfigKushkiIA
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.tb_ia_config_kushki OUTPUT deleted.secreto_ref WHERE id = 1;
END
GO

/* ---------------------------------------------------------------------
   4) Permisos (igual que 013)
   --------------------------------------------------------------------- */
IF DATABASE_PRINCIPAL_ID(N'rol_app_consenso') IS NOT NULL
    GRANT EXECUTE ON SCHEMA::dbo TO rol_app_consenso;
GO

PRINT '014_kushki_plataforma_ia.sql aplicado.';
GO

/* Verificación:
   EXEC dbo.sp_obtenerConfigKushkiIA;

   Rollback (borrar antes el secreto de AWS indicado en secreto_ref):
   DROP PROCEDURE dbo.sp_obtenerConfigKushkiIA, dbo.sp_guardarConfigKushkiIA, dbo.sp_actualizarEstadoConfigKushkiIA,
                  dbo.sp_eliminarConfigKushkiIA;
   DROP TABLE dbo.tb_ia_config_kushki;
   DELETE rp FROM dbo.tb_rol_permiso rp JOIN dbo.tb_permiso p ON p.reg_id = rp.permiso_id WHERE p.uri_permiso = 'plataforma/pago-tarjeta';
   DELETE dbo.tb_permiso WHERE uri_permiso = 'plataforma/pago-tarjeta';
*/
