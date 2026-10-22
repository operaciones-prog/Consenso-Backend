/* =====================================================================
   009_agentes_ia.sql
   ---------------------------------------------------------------------
   Objetivo : Agentes IA en funcionamiento.
              - Todos los complementos quedan disponibles (sin "Próximamente")
                y se agregan "Monitor de entregas" y "Previsión de cobros".
              - tb_ia_uso.con_saldo: si la consulta usó la clave de la
                plataforma (se descuenta del saldo) o la clave propia.
              - sp_registrarUsoIA: registra cada consulta y descuenta el saldo.
              - sp_obtenerCredencialIA: el consumo del mes de la empresa cuenta
                solo su clave propia; el de la plataforma, todo lo pagado con saldo.
   Requiere : 007 y 008.
   Motor    : SQL Server 2019
   Idempotente: SI.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* 1) Catálogo: todos disponibles + 2 nuevos */
MERGE dbo.tb_addon AS t
USING (VALUES
  ('monitor_entregas', N'Monitor de entregas', N'Delivery monitor',
   N'Revisa las órdenes de compra abiertas y avisa qué posiciones están vencidas o por vencer, con lo que falta entregar.',
   N'Reviews open purchase orders and flags positions that are overdue or due soon, with the quantities still pending.',
   N'OC abiertas y sus posiciones', N'Open POs and their positions',
   'local_shipping', '1,2,3,4', 1, 80),
  ('prevision_cobros', N'Previsión de cobros', N'Payment forecast',
   N'Resume el estado de cuenta: cuánto se pagó, qué está por cobrar, qué vence pronto y qué está atrasado.',
   N'Summarizes the account statement: what was paid, what is pending, what is due soon and what is late.',
   N'Estado de cuenta (pagados y por pagar)', N'Account statement (paid and pending)',
   'request_quote', '1,2,3,4', 1, 90)
) AS s (codigo_addon, nombre, nombre_en, descripcion, descripcion_en, datos_usa, datos_usa_en, icono, roles, disponible, orden)
ON t.codigo_addon = s.codigo_addon
WHEN MATCHED THEN UPDATE SET
    nombre = s.nombre, nombre_en = s.nombre_en, descripcion = s.descripcion, descripcion_en = s.descripcion_en,
    datos_usa = s.datos_usa, datos_usa_en = s.datos_usa_en, icono = s.icono, roles = s.roles,
    disponible = s.disponible, orden = s.orden
WHEN NOT MATCHED THEN INSERT (codigo_addon, nombre, nombre_en, descripcion, descripcion_en, datos_usa, datos_usa_en, icono, roles, disponible, orden)
    VALUES (s.codigo_addon, s.nombre, s.nombre_en, s.descripcion, s.descripcion_en, s.datos_usa, s.datos_usa_en, s.icono, s.roles, s.disponible, s.orden);

UPDATE dbo.tb_addon SET disponible = 1 WHERE disponible = 0;
GO

/* 2) tb_ia_uso.con_saldo */
IF COL_LENGTH('dbo.tb_ia_uso', 'con_saldo') IS NULL
BEGIN
    ALTER TABLE dbo.tb_ia_uso ADD con_saldo bit NOT NULL CONSTRAINT DF_ia_uso_con_saldo DEFAULT (0);
    PRINT 'Columna tb_ia_uso.con_saldo agregada.';
END
GO

/* 3) Procedimientos */
CREATE OR ALTER PROCEDURE dbo.sp_registrarUsoIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @usuario         varchar(50),
    @codigo_addon    varchar(40),
    @modelo          varchar(60),
    @tokens_entrada  int,
    @tokens_salida   int,
    @costo_usd       decimal(12,6),
    @con_saldo       bit
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    INSERT INTO dbo.tb_ia_uso (adm_cliente_id, codigo_sociedad, usuario, codigo_addon, modelo,
                               tokens_entrada, tokens_salida, costo_usd, con_saldo)
    VALUES (@adm_cliente_id, @codigo_sociedad, @usuario, @codigo_addon, @modelo,
            @tokens_entrada, @tokens_salida, @costo_usd, @con_saldo);

    -- El saldo puede quedar levemente negativo por la última consulta; la siguiente se bloquea.
    IF @con_saldo = 1
        UPDATE dbo.tb_ia_saldo
           SET saldo_usd = saldo_usd - @costo_usd, reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
         WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad;
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_obtenerCredencialIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @inicio_mes datetime = DATEFROMPARTS(YEAR(getdate()), MONTH(getdate()), 1);
    DECLARE @es_plataforma bit = CASE WHEN @adm_cliente_id = 'PLATAFORMA' THEN 1 ELSE 0 END;

    SELECT c.proveedor, c.modelo, c.secreto_ref, c.sufijo, c.estado, c.fecha_verificacion, c.limite_mensual_usd,
           COALESCE(c.reg_usuario_modificacion, c.reg_usuario_creacion) AS modificado_por,
           COALESCE(c.reg_fecha_modificacion, c.reg_fecha_creacion) AS fecha_modificacion,
           ISNULL(u.costo, 0) AS consumo_mes_usd,
           ISNULL(u.consultas, 0) AS consultas_mes
    FROM dbo.tb_ia_credencial c
    OUTER APPLY (
        SELECT SUM(x.costo_usd) AS costo, COUNT(*) AS consultas
        FROM dbo.tb_ia_uso x
        WHERE x.fecha >= @inicio_mes
          AND (   (@es_plataforma = 1 AND x.con_saldo = 1)
               OR (@es_plataforma = 0 AND x.con_saldo = 0
                   AND x.adm_cliente_id = c.adm_cliente_id AND x.codigo_sociedad = c.codigo_sociedad))
    ) u
    WHERE c.adm_cliente_id = @adm_cliente_id AND c.codigo_sociedad = @codigo_sociedad AND c.reg_estado = '1';
END
GO

/* 4) Permisos del usuario de la app (si tiene permisos objeto por objeto)
        GRANT EXECUTE ON OBJECT::dbo.sp_registrarUsoIA TO <usuario_app>;   */
IF DATABASE_PRINCIPAL_ID(N'rol_app_consenso') IS NOT NULL
    GRANT EXECUTE ON SCHEMA::dbo TO rol_app_consenso;
GO

PRINT '009_agentes_ia.sql aplicado.';
GO

/* Verificación:
   SELECT codigo_addon, disponible FROM dbo.tb_addon ORDER BY orden;   -- 9 filas, todas disponible = 1
   SELECT COL_LENGTH('dbo.tb_ia_uso', 'con_saldo');                      -- no NULL
   Rollback: DROP PROCEDURE dbo.sp_registrarUsoIA; (restaurar sp_obtenerCredencialIA desde 007)
*/
