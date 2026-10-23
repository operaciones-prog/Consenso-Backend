/* =====================================================================
   011_historial_ia.sql
   ---------------------------------------------------------------------
   Objetivo : Historial de consultas de IA (qué se preguntó y cuánto costó).
              - tb_ia_uso.pregunta: texto de la consulta (en el asistente, lo
                que escribió el usuario; en los agentes, un resumen de los
                datos de pantalla enviados).
              - sp_registrarUsoIA: recibe @pregunta (opcional, compatible
                con las llamadas anteriores).
              - sp_listarHistorialIA: una fila por consulta, paginado, con el
                total y el costo de todo el filtro.
   Requiere : 007, 009 y 010.
   Motor    : SQL Server 2019
   Idempotente: SI.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* 1) tb_ia_uso.pregunta (las consultas anteriores quedan con NULL) */
IF COL_LENGTH('dbo.tb_ia_uso', 'pregunta') IS NULL
BEGIN
    ALTER TABLE dbo.tb_ia_uso ADD pregunta nvarchar(2000) NULL;
    PRINT 'Columna tb_ia_uso.pregunta agregada.';
END
GO

/* 2) Procedimientos */
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

    -- El saldo puede quedar levemente negativo por la última consulta; la siguiente se bloquea.
    IF @con_saldo = 1
        UPDATE dbo.tb_ia_saldo
           SET saldo_usd = saldo_usd - @costo_usd, reg_fecha_modificacion = getdate(), reg_usuario_modificacion = @usuario
         WHERE adm_cliente_id = @adm_cliente_id AND codigo_sociedad = @codigo_sociedad;
    COMMIT;
END
GO

-- @usuario NULL = todas las consultas de la sociedad (administrador); si no, solo las de ese usuario.
-- @buscar: texto libre sobre pregunta, usuario, código o nombre del complemento.
-- total y costo_total_usd se calculan sobre todo el filtro (antes de paginar).
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

    SELECT u.reg_id, u.fecha, u.usuario, u.codigo_addon, a.nombre AS nombre_addon, a.nombre_en AS nombre_addon_en,
           u.pregunta, u.modelo, u.tokens_entrada, u.tokens_salida, u.costo_usd, u.con_saldo,
           COUNT(*) OVER () AS total,
           SUM(u.costo_usd) OVER () AS costo_total_usd
    FROM dbo.tb_ia_uso u
    LEFT JOIN dbo.tb_addon a ON a.codigo_addon = u.codigo_addon
    WHERE u.adm_cliente_id = @adm_cliente_id
      AND u.codigo_sociedad = @codigo_sociedad
      AND u.fecha >= @desde AND u.fecha < DATEADD(day, 1, @hasta)
      AND (@usuario IS NULL OR u.usuario = @usuario)
      AND (@patron IS NULL
           OR u.pregunta LIKE @patron OR u.usuario LIKE @patron
           OR u.codigo_addon LIKE @patron OR a.nombre LIKE @patron OR a.nombre_en LIKE @patron)
    ORDER BY u.fecha DESC, u.reg_id DESC
    OFFSET (@pagina - 1) * @tamanio ROWS FETCH NEXT @tamanio ROWS ONLY;
END
GO

/* 3) Permisos del usuario de la app (si tiene permisos objeto por objeto)
        GRANT EXECUTE ON OBJECT::dbo.sp_listarHistorialIA TO <usuario_app>;   */
IF DATABASE_PRINCIPAL_ID(N'rol_app_consenso') IS NOT NULL
    GRANT EXECUTE ON SCHEMA::dbo TO rol_app_consenso;
GO

PRINT '011_historial_ia.sql aplicado.';
GO

/* Verificación:
   SELECT COL_LENGTH('dbo.tb_ia_uso', 'pregunta');                                   -- 4000 (nvarchar(2000))
   EXEC dbo.sp_listarHistorialIA '1001', 'EC01', NULL, '2026-10-01', '2026-10-31';
   Rollback: DROP PROCEDURE dbo.sp_listarHistorialIA;
             ALTER TABLE dbo.tb_ia_uso DROP COLUMN pregunta;  (y restaurar sp_registrarUsoIA desde 009)
*/
