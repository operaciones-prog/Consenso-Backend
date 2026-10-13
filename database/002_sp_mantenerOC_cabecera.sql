/* =====================================================================
   002_sp_mantenerOC_cabecera.sql
   ---------------------------------------------------------------------
   Objetivo : Crear sp_mantenerOC_cabecera (hoy NO existe en QAS y
              OCRepository.registrarOCCabecera lo invoca).
   Motor    : SQL Server 2019 (requiere compatibility level >= 130 por OPENJSON)
   Idempotente: SI (CREATE OR ALTER).

   Llamada desde C# (OCRepository.registrarOCCabecera):
       EXEC sp_mantenerOC_cabecera @p_json = N'[ {...}, {...} ]'
   donde cada objeto es un item de "cabecera" construido en
   OCRepository.MapearCabeceras y filtrado por
   estado_confirmacion == "Pendiente" && reg_id == "0" (OC que vienen de SAP
   y aun no existen en tb_orden_compra_cab).

   Mapeo de propiedades JSON -> columnas tb_orden_compra_cab
   ---------------------------------------------------------------------
     $.adm_cliente_id          -> adm_cliente_id          (varchar 12, obligatorio)
     $.sociedad                -> sociedad                (varchar 18, obligatorio; ver SUPUESTO 1)
     $.numero_oc               -> numero_oc               (varchar 18, obligatorio)  [SAP EBELN]
     $.tipo_oc                 -> tipo_oc                 (varchar 12)               [SAP BSART]
     $.fecha_emision           -> fecha_emision           (date, TRY_CONVERT)        [SAP FEC_EMISION]
     $.fecha_etd               -> fecha_etd               (varchar 20)               [SAP FEC_ETD]
     $.fecha_produccion        -> fecha_produccion        (varchar 20)               [SAP FEC_PRODUCCION]
     $.fecha_recibido_bodega   -> fecha_recibido_bodega   (varchar 20)               [SAP FEC_ETA]
     $.estado_oc               -> estado_oc               (varchar 18)               [SAP ESTADO]
     $.codigo_proveedor        -> codigo_proveedor        (varchar 18)               [SAP LIFNR]
     $.identificacion_fiscal   -> identificacion_fiscal   (varchar 30)               [SAP TAXNUM]
     $.denominacion_fiscal     -> denominacion_fiscal     (varchar 250)              [SAP NAME1]
     $.moneda                  -> moneda                  (varchar 12)               [SAP WAERS]
     $.forma_pago              -> forma_pago              (varchar 50)               [SAP CONDICION_PAGO]
     $.condicion_pago          -> condicion_pago          (varchar 12; ver SUPUESTO 2)
     $.numero_oferta           -> numero_oferta           (varchar 18)
     $.importe_total           -> importe_total           (decimal(18,5), TRY_CONVERT)
     $.impuesto_general        -> impuesto_general        (decimal(18,5), TRY_CONVERT)
     $.importe_final           -> importe_final           (decimal(18,5), TRY_CONVERT)
   Valores fijos (mismos que sp_mantenerOC rama CREATE):
     estado_confirmacion = 'Pendiente', reg_estado = '2',
     reg_fecha_creacion = getdate(), reg_usuario_creacion = '2'
   Propiedades ignoradas: reg_id, reg_estado, estado_confirmacion,
     observacion_confirmacion, archivo, fecha_confirmacion, reg_fecha_creacion.

   SUPUESTOS
     1) Hoy MapearCabeceras NO incluye la propiedad "sociedad" en cada item,
        pero tb_orden_compra_cab.sociedad es NOT NULL. El SP toma $.sociedad
        y, si no viene, usa el parametro opcional @p_sociedad (default NULL).
        Los items sin sociedad (ni en el JSON ni en @p_sociedad) se OMITEN.
        => El backend debe agregar "sociedad" a cada item (o pasar @p_sociedad
           con jObject["sociedad"]) para que se registre algo.
     2) El item no trae condicion_pago (codigo). Se toma $.condicion_pago si
        existe; si no, queda NULL (sp_buscarOC hace LEFT JOIN y muestra forma_pago).
     3) Fechas: se intentan los formatos ISO (yyyy-mm-dd / yyyy-mm-ddThh:mi:ss),
        yyyymmdd, dd.mm.yyyy y dd/mm/yyyy. Si no se puede convertir -> NULL.
        Importes: TRY_CONVERT(decimal(18,5)); si no es numerico -> NULL.
     4) Textos se recortan (LEFT) al largo de la columna para evitar error de
        truncamiento.
     5) Clave unica: UQ (adm_cliente_id, numero_oc) en tb_orden_compra_cab.
        Solo se insertan OC que no existen (INSERT ... WHERE NOT EXISTS con
        UPDLOCK, HOLDLOCK) y se deduplica el propio JSON por esa clave.
   Resultado: no devuelve result set (C# usa ExecuteNonQuery). Emite PRINT
   informativo con filas insertadas.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_mantenerOC_cabecera](
    @p_json nvarchar(max),
    @p_sociedad varchar(18) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @p_json IS NULL OR ISJSON(@p_json) = 0
        THROW 50201, 'sp_mantenerOC_cabecera: @p_json es NULL o no es un JSON valido.', 1;

    BEGIN TRY
        BEGIN TRAN;

        ;WITH src AS (
            SELECT
                LEFT(j.adm_cliente_id, 12)                          AS adm_cliente_id,
                LEFT(COALESCE(NULLIF(j.sociedad, ''), @p_sociedad), 18) AS sociedad,
                LEFT(j.numero_oc, 18)                               AS numero_oc,
                LEFT(j.tipo_oc, 12)                                 AS tipo_oc,
                COALESCE(
                    TRY_CONVERT(date, j.fecha_emision, 23),
                    TRY_CONVERT(date, TRY_CONVERT(datetime2, j.fecha_emision, 126)),
                    TRY_CONVERT(date, j.fecha_emision, 112),
                    TRY_CONVERT(date, j.fecha_emision, 104),
                    TRY_CONVERT(date, j.fecha_emision, 103))        AS fecha_emision,
                LEFT(j.fecha_etd, 20)                               AS fecha_etd,
                LEFT(j.fecha_produccion, 20)                        AS fecha_produccion,
                LEFT(j.fecha_recibido_bodega, 20)                   AS fecha_recibido_bodega,
                LEFT(j.estado_oc, 18)                               AS estado_oc,
                LEFT(j.codigo_proveedor, 18)                        AS codigo_proveedor,
                LEFT(j.identificacion_fiscal, 30)                   AS identificacion_fiscal,
                LEFT(j.denominacion_fiscal, 250)                    AS denominacion_fiscal,
                LEFT(j.moneda, 12)                                  AS moneda,
                LEFT(j.forma_pago, 50)                              AS forma_pago,
                LEFT(NULLIF(j.condicion_pago, ''), 12)              AS condicion_pago,
                LEFT(j.numero_oferta, 18)                           AS numero_oferta,
                TRY_CONVERT(decimal(18,5), j.importe_total)         AS importe_total,
                TRY_CONVERT(decimal(18,5), j.impuesto_general)      AS impuesto_general,
                TRY_CONVERT(decimal(18,5), j.importe_final)         AS importe_final
            FROM OPENJSON(@p_json)
            WITH (
                adm_cliente_id        nvarchar(100)  '$.adm_cliente_id',
                sociedad              nvarchar(100)  '$.sociedad',
                numero_oc             nvarchar(100)  '$.numero_oc',
                tipo_oc               nvarchar(100)  '$.tipo_oc',
                fecha_emision         nvarchar(100)  '$.fecha_emision',
                fecha_etd             nvarchar(100)  '$.fecha_etd',
                fecha_produccion      nvarchar(100)  '$.fecha_produccion',
                fecha_recibido_bodega nvarchar(100)  '$.fecha_recibido_bodega',
                estado_oc             nvarchar(100)  '$.estado_oc',
                codigo_proveedor      nvarchar(100)  '$.codigo_proveedor',
                identificacion_fiscal nvarchar(100)  '$.identificacion_fiscal',
                denominacion_fiscal   nvarchar(500)  '$.denominacion_fiscal',
                moneda                nvarchar(100)  '$.moneda',
                forma_pago            nvarchar(200)  '$.forma_pago',
                condicion_pago        nvarchar(100)  '$.condicion_pago',
                numero_oferta         nvarchar(100)  '$.numero_oferta',
                importe_total         nvarchar(100)  '$.importe_total',
                impuesto_general      nvarchar(100)  '$.impuesto_general',
                importe_final         nvarchar(100)  '$.importe_final'
            ) AS j
        ),
        dedup AS (
            SELECT s.*,
                   ROW_NUMBER() OVER (PARTITION BY s.adm_cliente_id, s.numero_oc ORDER BY (SELECT NULL)) AS rn
            FROM src s
            WHERE NULLIF(s.adm_cliente_id, '') IS NOT NULL
              AND NULLIF(s.numero_oc, '') IS NOT NULL
              AND NULLIF(s.sociedad, '') IS NOT NULL
        )
        INSERT INTO dbo.tb_orden_compra_cab
            (adm_cliente_id, sociedad, numero_oc, tipo_oc, fecha_emision,
             fecha_etd, fecha_produccion, fecha_recibido_bodega,
             estado_oc, codigo_proveedor, identificacion_fiscal, denominacion_fiscal, moneda,
             forma_pago, condicion_pago, numero_oferta, importe_total, impuesto_general,
             importe_final, estado_confirmacion,
             reg_estado, reg_fecha_creacion, reg_usuario_creacion)
        SELECT d.adm_cliente_id, d.sociedad, d.numero_oc, d.tipo_oc, d.fecha_emision,
               d.fecha_etd, d.fecha_produccion, d.fecha_recibido_bodega,
               d.estado_oc, d.codigo_proveedor, d.identificacion_fiscal, d.denominacion_fiscal, d.moneda,
               d.forma_pago, d.condicion_pago, d.numero_oferta, d.importe_total, d.impuesto_general,
               d.importe_final, 'Pendiente',
               '2', getdate(), '2'
        FROM dedup d
        WHERE d.rn = 1
          AND NOT EXISTS (
                SELECT 1
                FROM dbo.tb_orden_compra_cab oc WITH (UPDLOCK, HOLDLOCK)
                WHERE oc.adm_cliente_id = d.adm_cliente_id
                  AND oc.numero_oc = d.numero_oc
          );

        DECLARE @insertadas int = @@ROWCOUNT;

        COMMIT;

        PRINT CONCAT('sp_mantenerOC_cabecera: OC insertadas = ', @insertadas);
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

PRINT '002_sp_mantenerOC_cabecera.sql aplicado.';
GO
