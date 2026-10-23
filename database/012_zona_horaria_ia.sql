/* =====================================================================
   012_zona_horaria_ia.sql
   ---------------------------------------------------------------------
   Objetivo : Fechas de IA en hora de Ecuador.
              tb_ia_uso.fecha se guarda con getdate() y el servidor (AWS RDS)
              está en UTC: el historial mostraba la hora 5 horas adelantada y
              los filtros por día ("este mes", "hoy") dejaban fuera lo
              consultado después de las 19:00 de Ecuador.
              Se sigue guardando en UTC; al listar:
              - @desde/@hasta (días de Ecuador) se pasan a UTC para filtrar
                (la columna queda indexable);
              - la fecha y el día que se devuelven van en hora de Ecuador.
              Afecta a sp_listarHistorialIA (011) y sp_listarUsoIA (010).
   Requiere : 010 y 011.
   Motor    : SQL Server 2019 (AT TIME ZONE).
   Idempotente: SI.
   Zona     : 'SA Pacific Standard Time' = UTC-5 sin horario de verano (Ecuador).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_listarUsoIA
    @adm_cliente_id  varchar(12) = NULL,
    @codigo_sociedad varchar(18) = NULL,
    @desde           date,
    @hasta           date,
    @solo_saldo      bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    -- Días de Ecuador → límites en UTC (como se guarda tb_ia_uso.fecha)
    DECLARE @desde_utc datetime = CONVERT(datetime, CAST(@desde AS datetime2) AT TIME ZONE 'SA Pacific Standard Time' AT TIME ZONE 'UTC');
    DECLARE @hasta_utc datetime = CONVERT(datetime, CAST(DATEADD(day, 1, @hasta) AS datetime2) AT TIME ZONE 'SA Pacific Standard Time' AT TIME ZONE 'UTC');

    SELECT u.adm_cliente_id, u.codigo_sociedad, s.razon_social, u.usuario, u.codigo_addon,
           a.nombre AS nombre_addon, a.nombre_en AS nombre_addon_en, u.modelo, u.con_saldo,
           l.dia,
           COUNT(*) AS consultas,
           SUM(CAST(u.tokens_entrada AS bigint)) AS tokens_entrada,
           SUM(CAST(u.tokens_salida AS bigint)) AS tokens_salida,
           SUM(u.costo_usd) AS costo_usd
    FROM dbo.tb_ia_uso u
    CROSS APPLY (SELECT CAST(u.fecha AT TIME ZONE 'UTC' AT TIME ZONE 'SA Pacific Standard Time' AS date) AS dia) l
    LEFT JOIN dbo.tb_addon a ON a.codigo_addon = u.codigo_addon
    OUTER APPLY (SELECT TOP 1 so.denominacion_sociedad AS razon_social
                 FROM dbo.tb_sociedad so
                 WHERE so.adm_cliente_id = u.adm_cliente_id AND so.codigo_sociedad = u.codigo_sociedad) s
    WHERE u.fecha >= @desde_utc AND u.fecha < @hasta_utc
      AND (@adm_cliente_id IS NULL OR u.adm_cliente_id = @adm_cliente_id)
      AND (@codigo_sociedad IS NULL OR u.codigo_sociedad = @codigo_sociedad)
      AND (@solo_saldo = 0 OR u.con_saldo = 1)
    GROUP BY u.adm_cliente_id, u.codigo_sociedad, s.razon_social, u.usuario, u.codigo_addon,
             a.nombre, a.nombre_en, u.modelo, u.con_saldo, l.dia;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_listarHistorialIA
    @adm_cliente_id  varchar(12),
    @codigo_sociedad varchar(18),
    @usuario         varchar(50) = NULL,
    @desde           date,
    @hasta           date,
    @buscar          nvarchar(100) = NULL,
    @pagina          int = 1,
    @tamanio         int = 15
AS
BEGIN
    SET NOCOUNT ON;
    IF @pagina < 1 SET @pagina = 1;
    IF @tamanio < 1 OR @tamanio > 100 SET @tamanio = 15;
    DECLARE @patron nvarchar(102) = CASE WHEN NULLIF(LTRIM(RTRIM(@buscar)), N'') IS NULL THEN NULL
                                         ELSE N'%' + LTRIM(RTRIM(@buscar)) + N'%' END;
    -- Días de Ecuador → límites en UTC (como se guarda tb_ia_uso.fecha)
    DECLARE @desde_utc datetime = CONVERT(datetime, CAST(@desde AS datetime2) AT TIME ZONE 'SA Pacific Standard Time' AT TIME ZONE 'UTC');
    DECLARE @hasta_utc datetime = CONVERT(datetime, CAST(DATEADD(day, 1, @hasta) AS datetime2) AT TIME ZONE 'SA Pacific Standard Time' AT TIME ZONE 'UTC');

    SELECT u.reg_id,
           CONVERT(datetime, u.fecha AT TIME ZONE 'UTC' AT TIME ZONE 'SA Pacific Standard Time') AS fecha,
           u.usuario, u.codigo_addon, a.nombre AS nombre_addon, a.nombre_en AS nombre_addon_en,
           u.pregunta, u.modelo, u.tokens_entrada, u.tokens_salida, u.costo_usd, u.con_saldo,
           COUNT(*) OVER () AS total,
           SUM(u.costo_usd) OVER () AS costo_total_usd
    FROM dbo.tb_ia_uso u
    LEFT JOIN dbo.tb_addon a ON a.codigo_addon = u.codigo_addon
    WHERE u.adm_cliente_id = @adm_cliente_id
      AND u.codigo_sociedad = @codigo_sociedad
      AND u.fecha >= @desde_utc AND u.fecha < @hasta_utc
      AND (@usuario IS NULL OR u.usuario = @usuario)
      AND (@patron IS NULL
           OR u.pregunta LIKE @patron OR u.usuario LIKE @patron
           OR u.codigo_addon LIKE @patron OR a.nombre LIKE @patron OR a.nombre_en LIKE @patron)
    ORDER BY u.fecha DESC, u.reg_id DESC
    OFFSET (@pagina - 1) * @tamanio ROWS FETCH NEXT @tamanio ROWS ONLY;
END
GO

PRINT '012_zona_horaria_ia.sql aplicado.';
GO

/* Verificación (la fecha devuelta debe coincidir con la hora local de Ecuador):
   DECLARE @hoy date = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SA Pacific Standard Time' AS date);
   EXEC dbo.sp_listarHistorialIA '1001', 'EC01', NULL, @hoy, @hoy;
   Rollback: volver a ejecutar 010_consumo_ia.sql y 011_historial_ia.sql.
*/
