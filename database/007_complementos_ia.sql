/* =====================================================================
   007_complementos_ia.sql
   ---------------------------------------------------------------------
   Objetivo : Base de los complementos IA (add-ons activables).
              - tb_addon             catálogo de complementos
              - tb_addon_activacion  encendido/apagado por cliente + sociedad
              - tb_ia_credencial     conexión con el proveedor de IA por sociedad.
                                     La API key NO se guarda aquí: vive en AWS
                                     Secrets Manager; aquí solo la referencia
                                     (nombre del secreto) y los últimos 4 caracteres.
              - tb_ia_uso            consumo y auditoría (lo llenan los agentes).
   Motor    : SQL Server 2019
   Idempotente: SI.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   1) Tablas
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.tb_addon', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_addon (
        codigo_addon     varchar(40)   NOT NULL CONSTRAINT PK_addon PRIMARY KEY,
        nombre           nvarchar(100) NOT NULL,
        nombre_en        nvarchar(100) NOT NULL,
        descripcion      nvarchar(400) NOT NULL,
        descripcion_en   nvarchar(400) NOT NULL,
        datos_usa        nvarchar(200) NOT NULL,  -- qué datos envía al proveedor de IA
        datos_usa_en     nvarchar(200) NOT NULL,
        icono            varchar(40)   NOT NULL,  -- Material Symbols
        roles            varchar(50)   NOT NULL,  -- rol_id separados por coma
        disponible       bit           NOT NULL CONSTRAINT DF_addon_disponible DEFAULT (0), -- 0 = "Próximamente"
        orden            int           NOT NULL CONSTRAINT DF_addon_orden DEFAULT (0),
        reg_estado       char(1)       NOT NULL CONSTRAINT DF_addon_estado DEFAULT ('1')
    );
    PRINT 'Tabla tb_addon creada.';
END
GO

IF OBJECT_ID(N'dbo.tb_addon_activacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_addon_activacion (
        reg_id                   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_addon_activacion PRIMARY KEY,
        adm_cliente_id           varchar(12) NOT NULL,
        codigo_sociedad          varchar(18) NOT NULL,
        codigo_addon             varchar(40) NOT NULL CONSTRAINT FK_addon_activacion_addon REFERENCES dbo.tb_addon(codigo_addon),
        activo                   bit         NOT NULL,
        reg_fecha_creacion       datetime    NOT NULL CONSTRAINT DF_addon_act_fc DEFAULT (getdate()),
        reg_usuario_creacion     varchar(50) NOT NULL,
        reg_fecha_modificacion   datetime    NULL,
        reg_usuario_modificacion varchar(50) NULL,
        CONSTRAINT UX_addon_activacion UNIQUE (adm_cliente_id, codigo_sociedad, codigo_addon)
    );
    PRINT 'Tabla tb_addon_activacion creada.';
END
GO

IF OBJECT_ID(N'dbo.tb_ia_credencial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_credencial (
        reg_id                   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ia_credencial PRIMARY KEY,
        adm_cliente_id           varchar(12)   NOT NULL,
        codigo_sociedad          varchar(18)   NOT NULL,
        proveedor                varchar(20)   NOT NULL,  -- 'anthropic'
        modelo                   varchar(60)   NOT NULL,
        secreto_ref              varchar(256)  NOT NULL,  -- nombre del secreto en AWS Secrets Manager
        sufijo                   varchar(8)    NOT NULL,  -- últimos 4 caracteres, solo para mostrar
        estado                   varchar(12)   NOT NULL,  -- 'verificada' | 'error'
        fecha_verificacion       datetime      NULL,
        limite_mensual_usd       decimal(10,2) NULL,
        reg_estado               char(1)       NOT NULL CONSTRAINT DF_ia_cred_estado DEFAULT ('1'),
        reg_fecha_creacion       datetime      NOT NULL CONSTRAINT DF_ia_cred_fc DEFAULT (getdate()),
        reg_usuario_creacion     varchar(50)   NOT NULL,
        reg_fecha_modificacion   datetime      NULL,
        reg_usuario_modificacion varchar(50)   NULL
    );
    -- Una sola conexión vigente por cliente + sociedad
    CREATE UNIQUE INDEX UX_ia_credencial_vigente ON dbo.tb_ia_credencial (adm_cliente_id, codigo_sociedad) WHERE reg_estado = '1';
    PRINT 'Tabla tb_ia_credencial creada.';
END
GO

IF OBJECT_ID(N'dbo.tb_ia_uso', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_uso (
        reg_id           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ia_uso PRIMARY KEY,
        adm_cliente_id   varchar(12)    NOT NULL,
        codigo_sociedad  varchar(18)    NOT NULL,
        usuario          varchar(50)    NOT NULL,
        codigo_addon     varchar(40)    NOT NULL,
        modelo           varchar(60)    NOT NULL,
        tokens_entrada   int            NOT NULL,
        tokens_salida    int            NOT NULL,
        costo_usd        decimal(12,6)  NOT NULL,
        fecha            datetime       NOT NULL CONSTRAINT DF_ia_uso_fecha DEFAULT (getdate())
    );
    CREATE INDEX IX_ia_uso_cli_soc_fecha ON dbo.tb_ia_uso (adm_cliente_id, codigo_sociedad, fecha) INCLUDE (costo_usd, tokens_entrada, tokens_salida);
    PRINT 'Tabla tb_ia_uso creada.';
END
GO

/* ---------------------------------------------------------------------
   2) Catálogo inicial (MERGE: re-ejecutar actualiza textos, no toca activaciones)
   roles: 1,2 Administrador · 3 Contador · 4 Proveedor · 5 Proveedor ext.
   --------------------------------------------------------------------- */
MERGE dbo.tb_addon AS t
USING (VALUES
  ('asistente_proveedor', N'Asistente del proveedor', N'Supplier assistant',
   N'Responde en lenguaje natural sobre órdenes de compra, HES y estado de cuenta: "¿qué OC tengo pendientes?", "¿cuándo me pagan la factura 001-002?".',
   N'Answers questions in natural language about purchase orders, service entry sheets and account statement.',
   N'OC, HES y estado de cuenta del usuario (solo lectura)', N'The user''s POs, service entry sheets and account statement (read only)',
   'smart_toy', '1,2,3,4,5', 1, 10),
  ('validador_facturas', N'Validador de facturas', N'Invoice validator',
   N'Lee la factura (PDF o XML) y la compara con la OC: RUC, montos, ítems e IVA. Avisa de diferencias antes de enviarla.',
   N'Reads the invoice (PDF or XML) and checks it against the PO: tax ID, amounts, items and VAT, before it is sent.',
   N'Archivo de la factura y detalle de la OC', N'Invoice file and PO detail',
   'fact_check', '1,2,3,4', 1, 20),
  ('analista_desempeno', N'Analista de desempeño', N'Performance analyst',
   N'Explica en pocas líneas la calificación del periodo, qué la bajó y qué mejorar.',
   N'Explains the period rating in a few lines: what lowered it and what to improve.',
   N'Calificación, OTIF y calidad del proveedor', N'Supplier rating, OTIF and quality',
   'insights', '1,2,3,4', 1, 30),
  ('redactor_observaciones', N'Redactor de observaciones', N'Remarks writer',
   N'Propone el texto de rechazo de una OC según las diferencias detectadas. Una persona lo revisa y lo envía.',
   N'Drafts the rejection text for a PO from the detected differences. A person reviews and sends it.',
   N'OC y su historial', N'PO and its history',
   'edit_note', '1,2,3', 0, 40),
  ('comparador_versiones', N'Comparador de versiones', N'Version comparer',
   N'Resume qué cambió entre versiones de una OC ("subió el precio de la posición 3 en 4%").',
   N'Summarizes what changed between PO versions.',
   N'Historial de la OC', N'PO history',
   'difference', '1,2,3,4', 0, 50),
  ('clasificador_repositorio', N'Clasificador del repositorio', N'Repository classifier',
   N'Etiqueta el tipo de documento y detecta vencimientos de certificados y pólizas.',
   N'Tags document types and detects certificate and policy expirations.',
   N'Documentos del repositorio', N'Repository documents',
   'folder_special', '1,2,3', 0, 60),
  ('alertas_proyeccion', N'Alertas de proyección', N'Forecast alerts',
   N'Resume la demanda proyectada y avisa de picos o caídas.',
   N'Summarizes projected demand and flags peaks or drops.',
   N'Proyección de materiales', N'Material forecast',
   'trending_up', '1,2,3,4,5', 0, 70)
) AS s (codigo_addon, nombre, nombre_en, descripcion, descripcion_en, datos_usa, datos_usa_en, icono, roles, disponible, orden)
ON t.codigo_addon = s.codigo_addon
WHEN MATCHED THEN UPDATE SET
    nombre = s.nombre, nombre_en = s.nombre_en, descripcion = s.descripcion, descripcion_en = s.descripcion_en,
    datos_usa = s.datos_usa, datos_usa_en = s.datos_usa_en, icono = s.icono, roles = s.roles,
    disponible = s.disponible, orden = s.orden
WHEN NOT MATCHED THEN INSERT (codigo_addon, nombre, nombre_en, descripcion, descripcion_en, datos_usa, datos_usa_en, icono, roles, disponible, orden)
    VALUES (s.codigo_addon, s.nombre, s.nombre_en, s.descripcion, s.descripcion_en, s.datos_usa, s.datos_usa_en, s.icono, s.roles, s.disponible, s.orden);
GO

/* ---------------------------------------------------------------------
   3) Procedimientos
   --------------------------------------------------------------------- */

-- Catálogo con el estado de activación de la sociedad. @idioma: 'ES' | 'EN'
CREATE OR ALTER PROCEDURE dbo.sp_listarAddon
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @idioma          varchar(5) = 'ES'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.codigo_addon,
           CASE WHEN @idioma = 'EN' THEN a.nombre_en      ELSE a.nombre      END AS nombre,
           CASE WHEN @idioma = 'EN' THEN a.descripcion_en ELSE a.descripcion END AS descripcion,
           CASE WHEN @idioma = 'EN' THEN a.datos_usa_en   ELSE a.datos_usa   END AS datos_usa,
           a.icono, a.roles, a.disponible,
           CAST(CASE WHEN a.disponible = 1 AND ac.activo = 1 THEN 1 ELSE 0 END AS bit) AS activo,
           ac.reg_usuario_modificacion AS modificado_por,
           COALESCE(ac.reg_fecha_modificacion, ac.reg_fecha_creacion) AS fecha_modificacion
    FROM dbo.tb_addon a
    LEFT JOIN dbo.tb_addon_activacion ac
           ON ac.codigo_addon = a.codigo_addon
          AND ac.adm_cliente_id = @adm_cliente_id
          AND ac.codigo_sociedad = @codigo_sociedad
    WHERE a.reg_estado = '1'
    ORDER BY a.orden, a.codigo_addon;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_mantenerAddonActivacion
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @codigo_addon    varchar(40),
    @activo          bit,
    @usuario         varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tb_addon WHERE codigo_addon = @codigo_addon AND reg_estado = '1' AND disponible = 1)
        THROW 50701, 'El complemento no existe o todavía no está disponible.', 1;

    BEGIN TRAN;
    MERGE dbo.tb_addon_activacion WITH (HOLDLOCK) AS t
    USING (SELECT @adm_cliente_id AS adm_cliente_id, @codigo_sociedad AS codigo_sociedad, @codigo_addon AS codigo_addon) AS s
       ON t.adm_cliente_id = s.adm_cliente_id AND t.codigo_sociedad = s.codigo_sociedad AND t.codigo_addon = s.codigo_addon
    WHEN MATCHED THEN UPDATE SET activo = @activo, reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
    WHEN NOT MATCHED THEN INSERT (adm_cliente_id, codigo_sociedad, codigo_addon, activo, reg_usuario_creacion, reg_fecha_modificacion, reg_usuario_modificacion)
         VALUES (@adm_cliente_id, @codigo_sociedad, @codigo_addon, @activo, @usuario, getdate(), @usuario);
    COMMIT;
END
GO

-- Conexión vigente (sin la clave: solo referencia y sufijo) + consumo del mes actual
CREATE OR ALTER PROCEDURE dbo.sp_obtenerCredencialIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @inicio_mes datetime = DATEFROMPARTS(YEAR(getdate()), MONTH(getdate()), 1);

    SELECT c.proveedor, c.modelo, c.secreto_ref, c.sufijo, c.estado, c.fecha_verificacion, c.limite_mensual_usd,
           COALESCE(c.reg_usuario_modificacion, c.reg_usuario_creacion) AS modificado_por,
           COALESCE(c.reg_fecha_modificacion, c.reg_fecha_creacion) AS fecha_modificacion,
           (SELECT ISNULL(SUM(u.costo_usd), 0) FROM dbo.tb_ia_uso u
             WHERE u.adm_cliente_id = c.adm_cliente_id AND u.codigo_sociedad = c.codigo_sociedad AND u.fecha >= @inicio_mes) AS consumo_mes_usd,
           (SELECT COUNT(*) FROM dbo.tb_ia_uso u
             WHERE u.adm_cliente_id = c.adm_cliente_id AND u.codigo_sociedad = c.codigo_sociedad AND u.fecha >= @inicio_mes) AS consultas_mes
    FROM dbo.tb_ia_credencial c
    WHERE c.adm_cliente_id = @adm_cliente_id AND c.codigo_sociedad = @codigo_sociedad AND c.reg_estado = '1';
END
GO

-- Crea o reemplaza la conexión vigente de la sociedad
CREATE OR ALTER PROCEDURE dbo.sp_mantenerCredencialIA
    @adm_cliente_id     varchar(12),
    @codigo_sociedad    varchar(18),
    @proveedor          varchar(20),
    @modelo             varchar(60),
    @secreto_ref        varchar(256),
    @sufijo             varchar(8),
    @estado             varchar(12),
    @limite_mensual_usd decimal(10,2) = NULL,
    @usuario            varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    UPDATE dbo.tb_ia_credencial WITH (UPDLOCK, HOLDLOCK)
       SET proveedor = @proveedor, modelo = @modelo, secreto_ref = @secreto_ref, sufijo = @sufijo,
           estado = @estado, fecha_verificacion = getdate(), limite_mensual_usd = @limite_mensual_usd,
           reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
     WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad AND reg_estado = '1';

    IF @@ROWCOUNT = 0
        INSERT INTO dbo.tb_ia_credencial (adm_cliente_id, codigo_sociedad, proveedor, modelo, secreto_ref, sufijo, estado,
                                          fecha_verificacion, limite_mensual_usd, reg_usuario_creacion)
        VALUES (@adm_cliente_id, @codigo_sociedad, @proveedor, @modelo, @secreto_ref, @sufijo, @estado,
                getdate(), @limite_mensual_usd, @usuario);
    COMMIT;
END
GO

-- Resultado de "Probar conexión" sobre la clave ya guardada
CREATE OR ALTER PROCEDURE dbo.sp_actualizarEstadoCredencialIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @estado          varchar(12)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tb_ia_credencial
       SET estado = @estado, fecha_verificacion = getdate()
     WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad AND reg_estado = '1';
END
GO

-- Da de baja la conexión y devuelve la referencia del secreto para borrarlo
CREATE OR ALTER PROCEDURE dbo.sp_eliminarCredencialIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @usuario         varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.tb_ia_credencial
       SET reg_estado = '0', reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
    OUTPUT deleted.secreto_ref
     WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad AND reg_estado = '1';
END
GO

-- Modo sin JWT (Auth:Enabled=false): valida que quien llama sea un usuario activo
-- y devuelve su rol desde la BD (el rol nunca se toma de la petición).
CREATE OR ALTER PROCEDURE dbo.sp_obtenerUsuarioIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @reg_id          int,
    @usuario         varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.usuario, CAST(u.rol_id AS varchar(10)) AS rol_id, u.codigo_referencia
    FROM dbo.tb_usuario u
    WHERE u.adm_cliente_id = @adm_cliente_id
      AND u.codigo_sociedad = @codigo_sociedad
      AND u.reg_id = @reg_id
      AND u.usuario = @usuario
      AND u.reg_estado = '1';
END
GO

/* ---------------------------------------------------------------------
   4) Permisos: el usuario de la app necesita EXECUTE sobre los SP nuevos.
      Con el rol de 005 ya lo tiene (EXECUTE ON SCHEMA::dbo). Si el usuario
      de la app tiene permisos objeto por objeto (p. ej. qa_user en QAS):
        GRANT EXECUTE ON OBJECT::dbo.sp_listarAddon                  TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_mantenerAddonActivacion      TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_obtenerCredencialIA          TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_mantenerCredencialIA         TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_actualizarEstadoCredencialIA TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_eliminarCredencialIA         TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_obtenerUsuarioIA             TO <usuario_app>;
   --------------------------------------------------------------------- */
IF DATABASE_PRINCIPAL_ID(N'rol_app_consenso') IS NOT NULL
    GRANT EXECUTE ON SCHEMA::dbo TO rol_app_consenso;
GO

PRINT '007_complementos_ia.sql aplicado.';
GO

/* Verificación:
   SELECT OBJECT_ID('dbo.tb_addon') t1, OBJECT_ID('dbo.tb_addon_activacion') t2,
          OBJECT_ID('dbo.tb_ia_credencial') t3, OBJECT_ID('dbo.tb_ia_uso') t4,
          (SELECT COUNT(*) FROM dbo.tb_addon) addons;   -- esperado: ids no NULL, 7
   EXEC dbo.sp_listarAddon '1001', 'EC01', 'ES';

   Rollback:
   DROP PROCEDURE dbo.sp_listarAddon, dbo.sp_mantenerAddonActivacion, dbo.sp_obtenerCredencialIA, dbo.sp_obtenerUsuarioIA,
                  dbo.sp_mantenerCredencialIA, dbo.sp_actualizarEstadoCredencialIA, dbo.sp_eliminarCredencialIA;
   DROP TABLE dbo.tb_ia_uso, dbo.tb_ia_credencial, dbo.tb_addon_activacion, dbo.tb_addon;
   (Borrar antes los secretos "<IA:SecretPrefix>/..." en Secrets Manager.)
*/
