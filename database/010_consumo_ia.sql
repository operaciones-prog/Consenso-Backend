/* =====================================================================
   010_consumo_ia.sql
   ---------------------------------------------------------------------
   Objetivo : Reporte de consumo de IA (tokens y costo).
              sp_listarUsoIA devuelve el consumo agrupado por cliente,
              sociedad, usuario, complemento, modelo y día; el backend arma
              los totales y los desgloses.
   Requiere : 007, 008 y 009.
   Motor    : SQL Server 2019
   Idempotente: SI.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- @adm_cliente_id / @codigo_sociedad NULL = todas (plataforma).
-- @solo_saldo = 1: solo lo pagado con la clave de la plataforma (saldo).
CREATE OR ALTER PROCEDURE dbo.sp_listarUsoIA
    @adm_cliente_id  varchar(12) = NULL,
    @codigo_sociedad varchar(18) = NULL,
    @desde           date,
    @hasta           date,
    @solo_saldo      bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.adm_cliente_id, u.codigo_sociedad, s.razon_social, u.usuario, u.codigo_addon,
           a.nombre AS nombre_addon, a.nombre_en AS nombre_addon_en, u.modelo, u.con_saldo,
           CAST(u.fecha AS date) AS dia,
           COUNT(*) AS consultas,
           SUM(CAST(u.tokens_entrada AS bigint)) AS tokens_entrada,
           SUM(CAST(u.tokens_salida AS bigint)) AS tokens_salida,
           SUM(u.costo_usd) AS costo_usd
    FROM dbo.tb_ia_uso u
    LEFT JOIN dbo.tb_addon a ON a.codigo_addon = u.codigo_addon
    OUTER APPLY (SELECT TOP 1 so.denominacion_sociedad AS razon_social
                 FROM dbo.tb_sociedad so
                 WHERE so.adm_cliente_id = u.adm_cliente_id AND so.codigo_sociedad = u.codigo_sociedad) s
    WHERE u.fecha >= @desde AND u.fecha < DATEADD(day, 1, @hasta)
      AND (@adm_cliente_id IS NULL OR u.adm_cliente_id = @adm_cliente_id)
      AND (@codigo_sociedad IS NULL OR u.codigo_sociedad = @codigo_sociedad)
      AND (@solo_saldo = 0 OR u.con_saldo = 1)
    GROUP BY u.adm_cliente_id, u.codigo_sociedad, s.razon_social, u.usuario, u.codigo_addon,
             a.nombre, a.nombre_en, u.modelo, u.con_saldo, CAST(u.fecha AS date);
END
GO

/* Permisos del usuario de la app (si tiene permisos objeto por objeto)
      GRANT EXECUTE ON OBJECT::dbo.sp_listarUsoIA TO <usuario_app>;   */
IF DATABASE_PRINCIPAL_ID(N'rol_app_consenso') IS NOT NULL
    GRANT EXECUTE ON SCHEMA::dbo TO rol_app_consenso;
GO

PRINT '010_consumo_ia.sql aplicado.';
GO

/* Verificación: EXEC dbo.sp_listarUsoIA '1001', 'EC01', '2026-10-01', '2026-10-31';
   Rollback:     DROP PROCEDURE dbo.sp_listarUsoIA; */
