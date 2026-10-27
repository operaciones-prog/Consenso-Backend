/* =====================================================================
   013_planes_kushki_ia.sql
   ---------------------------------------------------------------------
   Objetivo : Planes mensuales de IA pagados con tarjeta (Kushki, Ecuador).
              El plan da un saldo de IA por mes que se usa ANTES que el
              saldo recargado con comprobante (que no vence y sigue igual).
              - tb_ia_config_plan : IVA y días de gracia (fila única id = 1).
              - tb_ia_plan        : Básico / Pro / Empresarial (precio SIN IVA
                                    y saldo de IA incluido por mes, en US$ de
                                    costo real, igual que tb_ia_saldo).
              - tb_ia_suscripcion : la suscripción de cada cliente + sociedad.
              - tb_ia_pago        : cada cobro de Kushki (pagado o fallido).
              - tb_ia_saldo.saldo_plan_usd: saldo del plan del periodo actual
                (se reemplaza en cada cobro; no se acumula).
   Cobro    : Kushki guarda la tarjeta y cobra solo cada mes (suscripción
              programada). El plan se activa con el PRIMER cobro confirmado
              por el webhook; la renovación también llega por webhook.
              Los datos de la tarjeta nunca pasan por el portal ni por la BD:
              solo el id de la suscripción de Kushki, la marca y los últimos 4.
   Vigencia : el plan sirve hasta fin_periodo; si Kushki sigue reintentando
              el cobro (suscripción viva), hasta fin_periodo + dias_gracia.
   Requiere : 008, 009 y 011.
   Motor    : SQL Server 2019
   Idempotente: SI.
   Fechas   : columnas datetime con getdate() (UTC en RDS, como el resto);
              periodos en date (día de Ecuador, 'SA Pacific Standard Time').
   Precios  : los de los planes son VALORES INICIALES para probar.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   1) Tablas
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.tb_ia_config_plan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_config_plan (
        id                       int          NOT NULL CONSTRAINT PK_ia_config_plan PRIMARY KEY
                                                       CONSTRAINT CK_ia_config_plan_unica CHECK (id = 1),
        iva_pct                  decimal(5,2) NOT NULL CONSTRAINT CK_ia_config_plan_iva CHECK (iva_pct >= 0 AND iva_pct < 50),
        dias_gracia              int          NOT NULL CONSTRAINT CK_ia_config_plan_gracia CHECK (dias_gracia BETWEEN 0 AND 30),
        reg_fecha_modificacion   datetime     NOT NULL CONSTRAINT DF_ia_config_plan_fm DEFAULT (getdate()),
        reg_usuario_modificacion varchar(50)  NULL
    );
    PRINT 'Tabla tb_ia_config_plan creada.';
END
GO
-- IVA de Ecuador 15 %. Gracia: Kushki reintenta el cobro 3 días; se deja margen.
IF NOT EXISTS (SELECT 1 FROM dbo.tb_ia_config_plan WHERE id = 1)
    INSERT INTO dbo.tb_ia_config_plan (id, iva_pct, dias_gracia, reg_usuario_modificacion) VALUES (1, 15.00, 5, 'migracion');
GO

IF OBJECT_ID(N'dbo.tb_ia_plan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_plan (
        codigo_plan              varchar(20)   NOT NULL CONSTRAINT PK_ia_plan PRIMARY KEY,
        nombre                   nvarchar(60)  NOT NULL,
        nombre_en                nvarchar(60)  NOT NULL,
        descripcion              nvarchar(300) NOT NULL,
        descripcion_en           nvarchar(300) NOT NULL,
        precio_mensual_usd       decimal(10,2) NOT NULL CONSTRAINT CK_ia_plan_precio CHECK (precio_mensual_usd > 0),  -- SIN IVA
        saldo_mes_usd            decimal(10,2) NOT NULL CONSTRAINT CK_ia_plan_saldo CHECK (saldo_mes_usd >= 0),
        recomendado              bit           NOT NULL CONSTRAINT DF_ia_plan_recomendado DEFAULT (0),
        activo                   bit           NOT NULL CONSTRAINT DF_ia_plan_activo DEFAULT (1),
        orden                    int           NOT NULL CONSTRAINT DF_ia_plan_orden DEFAULT (0),
        reg_fecha_modificacion   datetime      NOT NULL CONSTRAINT DF_ia_plan_fm DEFAULT (getdate()),
        reg_usuario_modificacion varchar(50)   NULL
    );
    PRINT 'Tabla tb_ia_plan creada.';
END
GO
-- Solo inserta: si ya se ajustaron precios, una reejecución no los pisa.
INSERT INTO dbo.tb_ia_plan (codigo_plan, nombre, nombre_en, descripcion, descripcion_en, precio_mensual_usd, saldo_mes_usd,
                            recomendado, orden, reg_usuario_modificacion)
SELECT s.codigo_plan, s.nombre, s.nombre_en, s.descripcion, s.descripcion_en, s.precio, s.saldo, s.recomendado, s.orden, 'migracion'
FROM (VALUES
    ('basico', N'Básico', N'Basic',
     N'Todos los complementos con US$ 15 de consumo de IA al mes. Ideal para empezar.',
     N'All add-ons with US$ 15 of AI usage per month. Ideal to get started.',
     49.00, 15.00, 0, 10),
    ('pro', N'Pro', N'Pro',
     N'Todos los complementos con US$ 60 de consumo de IA al mes para el uso diario del equipo.',
     N'All add-ons with US$ 60 of AI usage per month for the team''s daily work.',
     149.00, 60.00, 1, 20),
    ('empresarial', N'Empresarial', N'Business',
     N'Todos los complementos con US$ 200 de consumo de IA al mes para equipos grandes.',
     N'All add-ons with US$ 200 of AI usage per month for large teams.',
     399.00, 200.00, 0, 30)
) AS s (codigo_plan, nombre, nombre_en, descripcion, descripcion_en, precio, saldo, recomendado, orden)
WHERE NOT EXISTS (SELECT 1 FROM dbo.tb_ia_plan p WHERE p.codigo_plan = s.codigo_plan);
GO

IF OBJECT_ID(N'dbo.tb_ia_suscripcion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_suscripcion (
        adm_cliente_id           varchar(12)   NOT NULL,
        codigo_sociedad          varchar(18)   NOT NULL,
        codigo_plan              varchar(20)   NOT NULL CONSTRAINT FK_ia_suscripcion_plan REFERENCES dbo.tb_ia_plan (codigo_plan),
        codigo_plan_siguiente    varchar(20)   NULL     CONSTRAINT FK_ia_suscripcion_siguiente REFERENCES dbo.tb_ia_plan (codigo_plan),
        inicio_periodo           date          NULL,     -- NULL = esperando el primer cobro
        fin_periodo              date          NULL,     -- último día incluido (Ecuador)
        cancelar_al_final        bit           NOT NULL CONSTRAINT DF_ia_suscripcion_cancelar DEFAULT (0),
        proximo_cobro            date          NULL,     -- cuándo cobrará Kushki (NULL = no cobrará)
        kushki_suscripcion_id    varchar(40)   NULL,     -- NULL = Kushki ya no cobrará
        tarjeta_marca            varchar(30)   NULL,
        tarjeta_ultimos4         varchar(4)    NULL,
        email                    varchar(150)  NULL,
        nombres                  nvarchar(60)  NULL,
        apellidos                nvarchar(60)  NULL,
        tipo_documento           varchar(3)    NULL,     -- CI | RUC | PAS
        numero_documento         varchar(20)   NULL,
        telefono                 varchar(20)   NULL,     -- E.164 (+593...)
        intentos_fallidos        int           NOT NULL CONSTRAINT DF_ia_suscripcion_intentos DEFAULT (0),
        ultimo_error             nvarchar(300) NULL,
        reg_fecha_creacion       datetime      NOT NULL CONSTRAINT DF_ia_suscripcion_fc DEFAULT (getdate()),
        reg_usuario_creacion     varchar(50)   NOT NULL,
        reg_fecha_modificacion   datetime      NULL,
        reg_usuario_modificacion varchar(50)   NULL,
        CONSTRAINT PK_ia_suscripcion PRIMARY KEY (adm_cliente_id, codigo_sociedad)
    );
    CREATE UNIQUE INDEX UX_ia_suscripcion_kushki ON dbo.tb_ia_suscripcion (kushki_suscripcion_id) WHERE kushki_suscripcion_id IS NOT NULL;
    PRINT 'Tabla tb_ia_suscripcion creada.';
END
GO

IF OBJECT_ID(N'dbo.tb_ia_pago', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tb_ia_pago (
        reg_id                bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ia_pago PRIMARY KEY,
        adm_cliente_id        varchar(12)   NOT NULL,
        codigo_sociedad       varchar(18)   NOT NULL,
        tipo                  varchar(12)   NOT NULL,     -- suscripcion (primer cobro) | renovacion
        concepto              nvarchar(200) NOT NULL,
        codigo_plan           varchar(20)   NULL,
        subtotal_usd          decimal(10,2) NOT NULL,     -- sin IVA
        iva_usd               decimal(10,2) NOT NULL,
        total_usd             decimal(10,2) NOT NULL,     -- lo cobrado a la tarjeta
        estado                varchar(10)   NOT NULL,     -- pagado | fallido
        kushki_suscripcion_id varchar(40)   NULL,
        kushki_ticket         varchar(40)   NULL,
        kushki_evento_id      varchar(80)   NULL,         -- id del webhook (evita procesarlo dos veces)
        mensaje               nvarchar(300) NULL,         -- motivo del rechazo
        usuario               varchar(50)   NOT NULL,
        fecha                 datetime      NOT NULL CONSTRAINT DF_ia_pago_fecha DEFAULT (getdate()),
        CONSTRAINT CK_ia_pago_tipo CHECK (tipo IN ('suscripcion', 'renovacion')),
        CONSTRAINT CK_ia_pago_estado CHECK (estado IN ('pagado', 'fallido'))
    );
    CREATE UNIQUE INDEX UX_ia_pago_ticket ON dbo.tb_ia_pago (kushki_ticket) WHERE kushki_ticket IS NOT NULL;
    CREATE UNIQUE INDEX UX_ia_pago_evento ON dbo.tb_ia_pago (kushki_evento_id) WHERE kushki_evento_id IS NOT NULL;
    CREATE INDEX IX_ia_pago_empresa ON dbo.tb_ia_pago (adm_cliente_id, codigo_sociedad, fecha);
    PRINT 'Tabla tb_ia_pago creada.';
END
GO

IF COL_LENGTH('dbo.tb_ia_saldo', 'saldo_plan_usd') IS NULL
BEGIN
    ALTER TABLE dbo.tb_ia_saldo ADD saldo_plan_usd decimal(12,4) NOT NULL CONSTRAINT DF_ia_saldo_plan DEFAULT (0);
    PRINT 'Columna tb_ia_saldo.saldo_plan_usd agregada.';
END
GO

/* ---------------------------------------------------------------------
   2) ¿El plan de la sociedad está vigente hoy (día de Ecuador)?
   --------------------------------------------------------------------- */
CREATE OR ALTER FUNCTION dbo.fn_ia_planVigente (@adm_cliente_id varchar(12), @codigo_sociedad varchar(18))
RETURNS TABLE
AS
RETURN
    SELECT CAST(CASE WHEN EXISTS (
               SELECT 1
               FROM dbo.tb_ia_suscripcion s
               CROSS JOIN dbo.tb_ia_config_plan c
               WHERE c.id = 1
                 AND s.adm_cliente_id = @adm_cliente_id AND s.codigo_sociedad = @codigo_sociedad
                 AND s.inicio_periodo IS NOT NULL
                 -- Con la suscripción viva, Kushki sigue reintentando: se dan los días de gracia
                 AND CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SA Pacific Standard Time' AS date)
                     <= DATEADD(day, CASE WHEN s.kushki_suscripcion_id IS NOT NULL AND s.cancelar_al_final = 0
                                          THEN c.dias_gracia ELSE 0 END, s.fin_periodo)
           ) THEN 1 ELSE 0 END AS bit) AS vigente;
GO

/* ---------------------------------------------------------------------
   3) Saldo: el del plan (si está vigente) + el recargado
   --------------------------------------------------------------------- */
-- saldo_usd = total disponible (lo que ya usaban el motor y la pantalla).
CREATE OR ALTER PROCEDURE dbo.sp_obtenerSaldoIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @recarga decimal(12,4) = 0, @plan decimal(12,4) = 0;
    SELECT @recarga = s.saldo_usd,
           @plan = CASE WHEN v.vigente = 1 THEN s.saldo_plan_usd ELSE 0 END
    FROM dbo.tb_ia_saldo s
    CROSS APPLY dbo.fn_ia_planVigente(s.adm_cliente_id, s.codigo_sociedad) v
    WHERE s.adm_cliente_id = @adm_cliente_id AND s.codigo_sociedad = @codigo_sociedad;

    SELECT @recarga + @plan AS saldo_usd,
           @plan AS saldo_plan_usd,
           @recarga AS saldo_recarga_usd,
           (SELECT COUNT(*) FROM dbo.tb_ia_recarga
             WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad AND estado = 'pendiente') AS recargas_pendientes;
END
GO

-- Igual que 011, pero descuenta primero del saldo del plan vigente y el resto del recargado.
CREATE OR ALTER PROCEDURE dbo.sp_registrarUsoIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @usuario         varchar(50),
    @codigo_addon    varchar(40),
    @modelo          varchar(60),
    @tokens_entrada  int,
    @tokens_salida   int,
    @costo_usd       decimal(12,6),
    @con_saldo       bit,
    @pregunta        nvarchar(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    INSERT INTO dbo.tb_ia_uso (adm_cliente_id, codigo_sociedad, usuario, codigo_addon, modelo,
                               tokens_entrada, tokens_salida, costo_usd, con_saldo, pregunta)
    VALUES (@adm_cliente_id, @codigo_sociedad, @usuario, @codigo_addon, @modelo,
            @tokens_entrada, @tokens_salida, @costo_usd, @con_saldo, @pregunta);

    IF @con_saldo = 1
    BEGIN
        DECLARE @vigente bit = (SELECT vigente FROM dbo.fn_ia_planVigente(@adm_cliente_id, @codigo_sociedad));
        -- En el SET todas las expresiones leen los valores ANTERIORES de la fila.
        -- El recargado puede quedar levemente negativo por la última consulta; la siguiente se bloquea.
        UPDATE dbo.tb_ia_saldo WITH (UPDLOCK)
           SET saldo_plan_usd = CASE WHEN @vigente = 0 THEN 0
                                     WHEN saldo_plan_usd >= @costo_usd THEN saldo_plan_usd - @costo_usd
                                     ELSE 0 END,
               saldo_usd = saldo_usd - CASE WHEN @vigente = 0 THEN @costo_usd
                                            WHEN saldo_plan_usd >= @costo_usd THEN 0
                                            ELSE @costo_usd - saldo_plan_usd END,
               reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
         WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad;
    END
    COMMIT;
END
GO

/* ---------------------------------------------------------------------
   4) Planes y suscripción
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_obtenerConfigPlanIA
AS
BEGIN
    SET NOCOUNT ON;
    SELECT iva_pct, dias_gracia FROM dbo.tb_ia_config_plan WHERE id = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_listarPlanIA
    @solo_activos bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT codigo_plan, nombre, nombre_en, descripcion, descripcion_en, precio_mensual_usd, saldo_mes_usd,
           recomendado, activo, orden
    FROM dbo.tb_ia_plan
    WHERE @solo_activos = 0 OR activo = 1
    ORDER BY orden, codigo_plan;
END
GO

-- Por cliente + sociedad, o por el id de la suscripción de Kushki (webhook).
CREATE OR ALTER PROCEDURE dbo.sp_obtenerSuscripcionIA
    @adm_cliente_id        varchar(12) = NULL,
    @codigo_sociedad       varchar(18) = NULL,
    @kushki_suscripcion_id varchar(40) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.adm_cliente_id, s.codigo_sociedad, s.codigo_plan, s.codigo_plan_siguiente,
           s.inicio_periodo, s.fin_periodo, s.cancelar_al_final, s.proximo_cobro, s.kushki_suscripcion_id,
           s.tarjeta_marca, s.tarjeta_ultimos4, s.email, s.nombres, s.apellidos, s.tipo_documento,
           s.numero_documento, s.telefono, s.intentos_fallidos, s.ultimo_error,
           v.vigente
    FROM dbo.tb_ia_suscripcion s
    CROSS APPLY dbo.fn_ia_planVigente(s.adm_cliente_id, s.codigo_sociedad) v
    WHERE (@kushki_suscripcion_id IS NOT NULL AND s.kushki_suscripcion_id = @kushki_suscripcion_id)
       OR (@kushki_suscripcion_id IS NULL AND s.adm_cliente_id = @adm_cliente_id AND s.codigo_sociedad = @codigo_sociedad);
END
GO

-- Tarjeta registrada en Kushki (suscripción nueva). El periodo actual, si lo hay, se conserva.
CREATE OR ALTER PROCEDURE dbo.sp_registrarSuscripcionIA
    @adm_cliente_id        varchar(12),
    @codigo_sociedad       varchar(18),
    @codigo_plan           varchar(20),
    @kushki_suscripcion_id varchar(40),
    @proximo_cobro         date,
    @tarjeta_marca         varchar(30),
    @tarjeta_ultimos4      varchar(4),
    @email                 varchar(150),
    @nombres               nvarchar(60),
    @apellidos             nvarchar(60),
    @tipo_documento        varchar(3),
    @numero_documento      varchar(20),
    @telefono              varchar(20),
    @usuario               varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    -- Otra suscripción viva en Kushki (p. ej. doble clic): no se reemplaza, el backend anula la nueva.
    IF EXISTS (SELECT 1 FROM dbo.tb_ia_suscripcion WITH (UPDLOCK, HOLDLOCK)
               WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad
                 AND kushki_suscripcion_id IS NOT NULL)
        THROW 50901, 'La empresa ya tiene una suscripción activa.', 1;

    MERGE dbo.tb_ia_suscripcion WITH (HOLDLOCK) AS t
    USING (SELECT @adm_cliente_id AS adm_cliente_id, @codigo_sociedad AS codigo_sociedad) AS x
       ON t.adm_cliente_id = x.adm_cliente_id AND t.codigo_sociedad = x.codigo_sociedad
    WHEN MATCHED THEN UPDATE SET
        codigo_plan = @codigo_plan, codigo_plan_siguiente = NULL, cancelar_al_final = 0, proximo_cobro = @proximo_cobro,
        kushki_suscripcion_id = @kushki_suscripcion_id, tarjeta_marca = @tarjeta_marca, tarjeta_ultimos4 = @tarjeta_ultimos4,
        email = @email, nombres = @nombres, apellidos = @apellidos, tipo_documento = @tipo_documento,
        numero_documento = @numero_documento, telefono = @telefono, intentos_fallidos = 0, ultimo_error = NULL,
        -- Un periodo ya vencido no se arrastra: la sociedad espera el primer cobro de la suscripción nueva
        inicio_periodo = CASE WHEN t.fin_periodo >= CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SA Pacific Standard Time' AS date)
                              THEN t.inicio_periodo END,
        fin_periodo = CASE WHEN t.fin_periodo >= CAST(SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'SA Pacific Standard Time' AS date)
                           THEN t.fin_periodo END,
        reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
    WHEN NOT MATCHED THEN INSERT (adm_cliente_id, codigo_sociedad, codigo_plan, proximo_cobro, kushki_suscripcion_id, tarjeta_marca,
                                  tarjeta_ultimos4, email, nombres, apellidos, tipo_documento, numero_documento, telefono, reg_usuario_creacion)
        VALUES (@adm_cliente_id, @codigo_sociedad, @codigo_plan, @proximo_cobro, @kushki_suscripcion_id, @tarjeta_marca,
                @tarjeta_ultimos4, @email, @nombres, @apellidos, @tipo_documento, @numero_documento, @telefono, @usuario);
    COMMIT;
END
GO

/*  @accion:
      programarPlan   cambio de plan para la próxima renovación (@codigo_plan NULL = anularlo)
      cambiarPlan     cambio inmediato, solo mientras se espera el primer cobro
      cancelar        el cliente canceló: Kushki ya no cobrará; el plan sigue hasta fin_periodo
      baja            Kushki eliminó la suscripción (webhook subscriptionDelete)
      tarjeta         se cambió la tarjeta de la suscripción                                   */
CREATE OR ALTER PROCEDURE dbo.sp_actualizarSuscripcionIA
    @adm_cliente_id   varchar(12),
    @codigo_sociedad  varchar(18),
    @accion           varchar(15),
    @codigo_plan      varchar(20) = NULL,
    @tarjeta_marca    varchar(30) = NULL,
    @tarjeta_ultimos4 varchar(4)  = NULL,
    @usuario          varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    IF @accion NOT IN ('programarPlan', 'cambiarPlan', 'cancelar', 'baja', 'tarjeta')
        THROW 50902, 'Acción no válida.', 1;

    UPDATE dbo.tb_ia_suscripcion
       SET codigo_plan_siguiente = CASE WHEN @accion = 'programarPlan' THEN @codigo_plan
                                        WHEN @accion IN ('cambiarPlan', 'cancelar', 'baja') THEN NULL
                                        ELSE codigo_plan_siguiente END,
           codigo_plan = CASE WHEN @accion = 'cambiarPlan' THEN @codigo_plan ELSE codigo_plan END,
           cancelar_al_final = CASE WHEN @accion IN ('cancelar', 'baja') THEN 1 ELSE cancelar_al_final END,
           kushki_suscripcion_id = CASE WHEN @accion IN ('cancelar', 'baja') THEN NULL ELSE kushki_suscripcion_id END,
           proximo_cobro = CASE WHEN @accion IN ('cancelar', 'baja') THEN NULL ELSE proximo_cobro END,
           tarjeta_marca = CASE WHEN @accion = 'tarjeta' THEN @tarjeta_marca ELSE tarjeta_marca END,
           tarjeta_ultimos4 = CASE WHEN @accion = 'tarjeta' THEN @tarjeta_ultimos4 ELSE tarjeta_ultimos4 END,
           intentos_fallidos = CASE WHEN @accion = 'tarjeta' THEN 0 ELSE intentos_fallidos END,
           ultimo_error = CASE WHEN @accion = 'tarjeta' THEN NULL ELSE ultimo_error END,
           reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
     WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad
       AND (@accion <> 'cambiarPlan' OR inicio_periodo IS NULL);

    IF @@ROWCOUNT = 0
        THROW 50903, 'No hay una suscripción que actualizar.', 1;
END
GO

/* ---------------------------------------------------------------------
   5) Cobros (webhooks de Kushki)
   --------------------------------------------------------------------- */
-- Cobro aprobado: abre el periodo, aplica el plan programado y repone el saldo del plan.
-- Devuelve aplicado = 0 si el ticket o el evento ya se habían procesado.
CREATE OR ALTER PROCEDURE dbo.sp_acreditarCobroPlanIA
    @kushki_suscripcion_id varchar(40),
    @kushki_ticket         varchar(40),
    @kushki_evento_id      varchar(80),
    @fecha_cobro           date,
    @subtotal_usd          decimal(10,2),
    @iva_usd               decimal(10,2),
    @total_usd             decimal(10,2),
    @usuario               varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF EXISTS (SELECT 1 FROM dbo.tb_ia_pago WITH (UPDLOCK, HOLDLOCK)
               WHERE (@kushki_ticket IS NOT NULL AND kushki_ticket = @kushki_ticket)
                  OR (@kushki_evento_id IS NOT NULL AND kushki_evento_id = @kushki_evento_id))
    BEGIN
        COMMIT;
        SELECT CAST(0 AS bit) AS aplicado;
        RETURN;
    END

    DECLARE @adm varchar(12), @soc varchar(18), @plan varchar(20), @primero bit;
    SELECT @adm = adm_cliente_id, @soc = codigo_sociedad,
           @plan = ISNULL(codigo_plan_siguiente, codigo_plan),
           @primero = CASE WHEN inicio_periodo IS NULL THEN 1 ELSE 0 END
    FROM dbo.tb_ia_suscripcion WITH (UPDLOCK, HOLDLOCK)
    WHERE kushki_suscripcion_id = @kushki_suscripcion_id;

    IF @adm IS NULL
        THROW 50904, 'La suscripción de Kushki no está registrada.', 1;

    DECLARE @saldo_mes decimal(10,2), @nombre nvarchar(60);
    SELECT @saldo_mes = saldo_mes_usd, @nombre = nombre FROM dbo.tb_ia_plan WHERE codigo_plan = @plan;

    UPDATE dbo.tb_ia_suscripcion
       SET codigo_plan = @plan, codigo_plan_siguiente = NULL,
           inicio_periodo = @fecha_cobro, fin_periodo = DATEADD(day, -1, DATEADD(month, 1, @fecha_cobro)),
           proximo_cobro = DATEADD(month, 1, @fecha_cobro),
           intentos_fallidos = 0, ultimo_error = NULL,
           reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
     WHERE adm_cliente_id = @adm AND codigo_sociedad = @soc;

    -- El saldo del plan no se acumula: cada periodo empieza con el del plan.
    MERGE dbo.tb_ia_saldo WITH (HOLDLOCK) AS t
    USING (SELECT @adm AS adm_cliente_id, @soc AS codigo_sociedad) AS x
       ON t.adm_cliente_id = x.adm_cliente_id AND t.codigo_sociedad = x.codigo_sociedad
    WHEN MATCHED THEN UPDATE SET saldo_plan_usd = @saldo_mes, reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
    WHEN NOT MATCHED THEN INSERT (adm_cliente_id, codigo_sociedad, saldo_usd, saldo_plan_usd, reg_usuario_modificacion)
         VALUES (@adm, @soc, 0, @saldo_mes, @usuario);

    INSERT INTO dbo.tb_ia_pago (adm_cliente_id, codigo_sociedad, tipo, concepto, codigo_plan, subtotal_usd, iva_usd, total_usd,
                                estado, kushki_suscripcion_id, kushki_ticket, kushki_evento_id, usuario)
    VALUES (@adm, @soc, CASE WHEN @primero = 1 THEN 'suscripcion' ELSE 'renovacion' END,
            N'Plan ' + ISNULL(@nombre, @plan) + CASE WHEN @primero = 1 THEN N' - primer mes' ELSE N' - renovación' END,
            @plan, @subtotal_usd, @iva_usd, @total_usd, 'pagado', @kushki_suscripcion_id, @kushki_ticket, @kushki_evento_id, @usuario);
    COMMIT;
    SELECT CAST(1 AS bit) AS aplicado;
END
GO

-- Cobro rechazado. @ultimo = 1: Kushki agotó los reintentos y la suscripción se dio de baja.
CREATE OR ALTER PROCEDURE dbo.sp_registrarCobroFallidoPlanIA
    @kushki_suscripcion_id varchar(40),
    @kushki_evento_id      varchar(80),
    @subtotal_usd          decimal(10,2),
    @iva_usd               decimal(10,2),
    @total_usd             decimal(10,2),
    @mensaje               nvarchar(300),
    @ultimo                bit,
    @usuario               varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF @kushki_evento_id IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.tb_ia_pago WITH (UPDLOCK, HOLDLOCK) WHERE kushki_evento_id = @kushki_evento_id)
    BEGIN
        COMMIT;
        RETURN;
    END

    DECLARE @adm varchar(12), @soc varchar(18), @plan varchar(20), @primero bit;
    SELECT @adm = adm_cliente_id, @soc = codigo_sociedad, @plan = ISNULL(codigo_plan_siguiente, codigo_plan),
           @primero = CASE WHEN inicio_periodo IS NULL THEN 1 ELSE 0 END
    FROM dbo.tb_ia_suscripcion WITH (UPDLOCK, HOLDLOCK)
    WHERE kushki_suscripcion_id = @kushki_suscripcion_id;

    IF @adm IS NULL
        THROW 50904, 'La suscripción de Kushki no está registrada.', 1;

    UPDATE dbo.tb_ia_suscripcion
       SET intentos_fallidos = intentos_fallidos + 1, ultimo_error = @mensaje,
           kushki_suscripcion_id = CASE WHEN @ultimo = 1 THEN NULL ELSE kushki_suscripcion_id END,
           proximo_cobro = CASE WHEN @ultimo = 1 THEN NULL ELSE proximo_cobro END,
           codigo_plan_siguiente = CASE WHEN @ultimo = 1 THEN NULL ELSE codigo_plan_siguiente END,
           reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
     WHERE adm_cliente_id = @adm AND codigo_sociedad = @soc;

    INSERT INTO dbo.tb_ia_pago (adm_cliente_id, codigo_sociedad, tipo, concepto, codigo_plan, subtotal_usd, iva_usd, total_usd,
                                estado, kushki_suscripcion_id, kushki_evento_id, mensaje, usuario)
    SELECT @adm, @soc, CASE WHEN @primero = 1 THEN 'suscripcion' ELSE 'renovacion' END,
           N'Plan ' + ISNULL(p.nombre, @plan) + N' - cobro rechazado', @plan, @subtotal_usd, @iva_usd, @total_usd,
           'fallido', @kushki_suscripcion_id, @kushki_evento_id, @mensaje, @usuario
    FROM (SELECT @plan AS codigo_plan) x
    LEFT JOIN dbo.tb_ia_plan p ON p.codigo_plan = x.codigo_plan;
    COMMIT;
END
GO

-- @adm_cliente_id NULL = todas las empresas (plataforma).
CREATE OR ALTER PROCEDURE dbo.sp_listarPagoPlanIA
    @adm_cliente_id  varchar(12) = NULL,
    @codigo_sociedad varchar(18) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (200) reg_id, adm_cliente_id, codigo_sociedad, tipo, concepto, codigo_plan, subtotal_usd, iva_usd, total_usd,
           estado, kushki_ticket, mensaje,
           CONVERT(datetime, fecha AT TIME ZONE 'UTC' AT TIME ZONE 'SA Pacific Standard Time') AS fecha
    FROM dbo.tb_ia_pago
    WHERE (@adm_cliente_id IS NULL OR adm_cliente_id = @adm_cliente_id)
      AND (@codigo_sociedad IS NULL OR codigo_sociedad = @codigo_sociedad)
    ORDER BY reg_id DESC;
END
GO

/* ---------------------------------------------------------------------
   6) Permisos del usuario de la app (si tiene permisos objeto por objeto)
        GRANT EXECUTE ON OBJECT::dbo.sp_obtenerConfigPlanIA        TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_listarPlanIA               TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_obtenerSuscripcionIA       TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_registrarSuscripcionIA     TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_actualizarSuscripcionIA    TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_acreditarCobroPlanIA       TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_registrarCobroFallidoPlanIA TO <usuario_app>;
        GRANT EXECUTE ON OBJECT::dbo.sp_listarPagoPlanIA           TO <usuario_app>;
      fn_ia_planVigente solo se usa dentro de los SP (encadenamiento de propiedad): no necesita permisos.
   --------------------------------------------------------------------- */
IF DATABASE_PRINCIPAL_ID(N'rol_app_consenso') IS NOT NULL
    GRANT EXECUTE ON SCHEMA::dbo TO rol_app_consenso;
GO

PRINT '013_planes_kushki_ia.sql aplicado.';
GO

/* Verificación:
   SELECT * FROM dbo.tb_ia_config_plan;
   EXEC dbo.sp_listarPlanIA;
   EXEC dbo.sp_obtenerSaldoIA '1001', 'EC01';          -- saldo_usd = saldo_plan_usd + saldo_recarga_usd
   SELECT * FROM dbo.fn_ia_planVigente('1001', 'EC01');

   Rollback (sin cobros reales; exportar tb_ia_pago antes si los hubo):
   DROP PROCEDURE dbo.sp_obtenerConfigPlanIA, dbo.sp_listarPlanIA, dbo.sp_obtenerSuscripcionIA, dbo.sp_registrarSuscripcionIA,
                  dbo.sp_actualizarSuscripcionIA, dbo.sp_acreditarCobroPlanIA, dbo.sp_registrarCobroFallidoPlanIA,
                  dbo.sp_listarPagoPlanIA;
   -- Restaurar sp_obtenerSaldoIA (008) y sp_registrarUsoIA (011) ANTES de borrar la función y la columna.
   DROP FUNCTION dbo.fn_ia_planVigente;
   ALTER TABLE dbo.tb_ia_saldo DROP CONSTRAINT DF_ia_saldo_plan;
   ALTER TABLE dbo.tb_ia_saldo DROP COLUMN saldo_plan_usd;
   DROP TABLE dbo.tb_ia_pago, dbo.tb_ia_suscripcion, dbo.tb_ia_plan, dbo.tb_ia_config_plan;
*/
