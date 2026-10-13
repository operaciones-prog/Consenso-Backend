/* =====================================================================
   003_fix_bugs_sp.sql
   ---------------------------------------------------------------------
   Objetivo : Correccion de bugs / concurrencia en procedimientos existentes.
   Motor    : SQL Server 2019
   Idempotente: SI (CREATE OR ALTER).
   Se conservan nombres de parametros y columnas de los result sets.

   Procedimientos:
     - sp_insertarCondicionPago      : UPDATE ya no escribe getdate() en
                                       reg_usuario_modificacion; setea
                                       reg_fecha_modificacion. Nuevo parametro
                                       OPCIONAL @p_usuario (default NULL -> '2').
     - sp_mantenerOC                 : transacciones en UPDATE/MODIFY y
                                       APPROVE/REFUSED, bloqueos UPDLOCK/HOLDLOCK,
                                       THROW si el UPDATE de cabecera no afecta filas.
     - sp_eliminarOC                 : ambos DELETE en una transaccion.
     - sp_eliminarCalificacionProveedor: SET NOCOUNT/XACT_ABORT (una sola sentencia).
     - sp_mantenerOCDet              : UPDATE de posicion inexistente la inserta; otros action_code se ignoran como antes.
     - sp_mantenerConstante          : @descripcion varchar(100) (largo real de la columna),
                                       respeta @reg_estado ('0'/'1'), THROW si no actualiza.
     - sp_validarOCPendiente         : MERGE WITH (HOLDLOCK), excluye codigo_proveedor NULL,
                                       SELECT final filtrado por adm_cliente_id.
     - sp_buscarOC / sp_buscarProveedor / sp_listarUsuario:
                                       filtros LIKE que no excluyen columnas NULL;
                                       join a tb_condicion_pago solo con reg_estado='1';
                                       comparaciones de reg_estado con '1' (char).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   sp_insertarCondicionPago
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_insertarCondicionPago](
    @p_id int,
    @p_codigo varchar(20),
    @p_nombre_es varchar(200),
    @p_nombre_en varchar(200),
    @p_usuario varchar(50) = NULL   -- OPCIONAL (nuevo). Si no se envia se usa '2' (usuario sistema)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @p_id IS NOT NULL AND @p_id > 0
        UPDATE [tb_condicion_pago] SET
            [codigo] = @p_codigo,
            [nombre_es] = @p_nombre_es,
            [nombre_en] = @p_nombre_en,
            [reg_fecha_modificacion] = getdate(),
            [reg_usuario_modificacion] = ISNULL(NULLIF(@p_usuario, ''), '2')
        WHERE reg_id = @p_id;
    ELSE
        INSERT INTO [dbo].[tb_condicion_pago]
            ([codigo], [nombre_es], [nombre_en], [reg_fecha_creacion], [reg_estado], [reg_usuario_creacion])
        VALUES
            (@p_codigo, @p_nombre_es, @p_nombre_en, getdate(), '1', NULLIF(@p_usuario, ''));
END
GO

/* ---------------------------------------------------------------------
   sp_mantenerOC
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_mantenerOC](
    @action_code varchar(18),
    @adm_cliente_id varchar(12),
    @sociedad varchar(18),
    @numero_oc varchar(18),
    @tipo_oc varchar(12),
    @fecha_emision date,
    @fecha_etd varchar(20),
    @fecha_produccion varchar(20),
    @fecha_recibido_bodega varchar(20),
    @estado_oc varchar(18),
    @codigo_proveedor varchar(18),
    @identificacion_fiscal varchar(30),
    @denominacion_fiscal varchar(250),
    @moneda varchar(12),
    @forma_pago varchar(50),
    @condicion_pago varchar(12),
    @numero_oferta varchar(18),
    @importe_total decimal(18,5),
    @impuesto_general decimal(18,5),
    @importe_final decimal(18,5),
    @observacion varchar(200)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @reg_version int;
    DECLARE @estado_confirmacion varchar(18);
    DECLARE @observacion_confirmacion varchar(200);
    DECLARE @fecha_confirmacion datetime;
    DECLARE @validaOC varchar(18);
    DECLARE @filas int;
    DECLARE @msg nvarchar(400);

    BEGIN TRY
        IF @action_code = 'CREATE'
        BEGIN
            insert into dbo.tb_orden_compra_cab
                (adm_cliente_id,sociedad,numero_oc,tipo_oc,fecha_emision,
                fecha_etd,fecha_produccion,fecha_recibido_bodega,
                estado_oc,codigo_proveedor,identificacion_fiscal,denominacion_fiscal,moneda,
                forma_pago,condicion_pago,numero_oferta,importe_total,impuesto_general,
                importe_final,estado_confirmacion,
                reg_estado,reg_fecha_creacion,reg_usuario_creacion)
            values
                (@adm_cliente_id,@sociedad,@numero_oc,@tipo_oc,@fecha_emision,
                @fecha_etd,@fecha_produccion,@fecha_recibido_bodega,
                @estado_oc,@codigo_proveedor,@identificacion_fiscal,@denominacion_fiscal,@moneda,
                @forma_pago,@condicion_pago,@numero_oferta,@importe_total,@impuesto_general,
                @importe_final,'Pendiente'
                ,'2',getdate(),'2');
        END
        ELSE IF @action_code = 'HISTORY'
        BEGIN
            insert into dbo.tb_orden_compra_cab
                (adm_cliente_id,sociedad,numero_oc,tipo_oc,fecha_emision,
                fecha_etd,fecha_produccion,fecha_recibido_bodega,
                estado_oc,codigo_proveedor,identificacion_fiscal,denominacion_fiscal,moneda,
                forma_pago,condicion_pago,numero_oferta,importe_total,impuesto_general,
                importe_final,estado_confirmacion,
                reg_estado,reg_fecha_creacion,reg_usuario_creacion)
            values
                (@adm_cliente_id,@sociedad,@numero_oc,@tipo_oc,@fecha_emision,
                @fecha_etd,@fecha_produccion,@fecha_recibido_bodega,
                @estado_oc,@codigo_proveedor,@identificacion_fiscal,@denominacion_fiscal,@moneda,
                @forma_pago,@condicion_pago,@numero_oferta,@importe_total,@impuesto_general,
                @importe_final,'Aprobado'
                ,'2',getdate(),'2');
        END
        ELSE IF @action_code = 'UPDATE' OR @action_code = 'MODIFY'
        BEGIN
            BEGIN TRAN;

            -- Bloquea la cabecera durante toda la transaccion y lee el estado actual
            select @estado_confirmacion = estado_confirmacion,
                   @observacion_confirmacion = observacion_confirmacion,
                   @fecha_confirmacion = fecha_confirmacion
            from tb_orden_compra_cab WITH (UPDLOCK, HOLDLOCK)
            where adm_cliente_id = @adm_cliente_id
            and sociedad = @sociedad
            and numero_oc = @numero_oc;

            select @reg_version = isnull(max(reg_version),0)
            from log_orden_compra_cab WITH (UPDLOCK, HOLDLOCK)
            where adm_cliente_id = @adm_cliente_id
            and sociedad = @sociedad
            and numero_oc = @numero_oc;

            set @reg_version = @reg_version + 1;

            insert into log_orden_compra_cab(reg_version,adm_cliente_id,sociedad,numero_oc,tipo_oc,fecha_emision,
                fecha_etd,fecha_produccion,fecha_recibido_bodega,
                estado_oc,codigo_proveedor,identificacion_fiscal,denominacion_fiscal,moneda,
                forma_pago,condicion_pago,numero_oferta,importe_total,impuesto_general,
                importe_final,estado_confirmacion,observacion_confirmacion,fecha_confirmacion,
                reg_estado,reg_fecha_creacion,reg_usuario_creacion)
                select @reg_version,adm_cliente_id,sociedad,numero_oc,tipo_oc,fecha_emision,
                fecha_etd,fecha_produccion,fecha_recibido_bodega,
                estado_oc,codigo_proveedor,identificacion_fiscal,denominacion_fiscal,moneda,
                forma_pago,condicion_pago,numero_oferta,importe_total,impuesto_general,
                importe_final,estado_confirmacion,observacion_confirmacion,fecha_confirmacion,
                reg_estado,getdate(),'2'
                from tb_orden_compra_cab
                where adm_cliente_id = @adm_cliente_id
                and sociedad = @sociedad
                and numero_oc = @numero_oc;

            insert into log_orden_compra_det
                (reg_version,adm_cliente_id,numero_oc,posicion_oc,cod_material,des_material,
                um_material,cantidad,cantidad_reparto,cantidad_por_entregar,cantidad_entregada,
                precio,total,numero_solped,posicion_solped,fecha_entrega_solped,fecha_entrega,
                fecha_entrega_estadistica,estado_posicion_oc,reg_estado,reg_fecha_creacion,
                reg_usuario_creacion)
                select @reg_version,adm_cliente_id,numero_oc,posicion_oc,cod_material,des_material,
                um_material,cantidad,cantidad_reparto,cantidad_por_entregar,cantidad_entregada,
                precio,total,numero_solped,posicion_solped,fecha_entrega_solped,fecha_entrega,
                fecha_entrega_estadistica,estado_posicion_oc,reg_estado,getdate(),'2'
                from tb_orden_compra_det
                where adm_cliente_id = @adm_cliente_id
                and numero_oc = @numero_oc;

            update tb_orden_compra_cab
                set sociedad = @sociedad,
                tipo_oc = @tipo_oc,
                fecha_emision = @fecha_emision,
                fecha_etd = @fecha_etd,
                fecha_produccion = @fecha_produccion,
                fecha_recibido_bodega = @fecha_recibido_bodega,
                estado_oc = @estado_oc,
                codigo_proveedor = @codigo_proveedor,
                identificacion_fiscal = @identificacion_fiscal,
                denominacion_fiscal = @denominacion_fiscal,
                moneda = @moneda,
                forma_pago = @forma_pago,
                condicion_pago = @condicion_pago,
                numero_oferta = @numero_oferta,
                importe_total = @importe_total,
                impuesto_general = @impuesto_general,
                importe_final = @importe_final,
                estado_confirmacion = @estado_confirmacion,
                observacion_confirmacion = @observacion_confirmacion,
                fecha_confirmacion = @fecha_confirmacion,
                reg_estado = '2',
                reg_fecha_modificacion = getdate(),
                reg_usuario_modificacion = '2'
                where adm_cliente_id = @adm_cliente_id
                and sociedad = @sociedad
                and numero_oc = @numero_oc;

            SET @filas = @@ROWCOUNT;
            IF @filas = 0
            BEGIN
                SET @msg = CONCAT(N'sp_mantenerOC (', @action_code, N'): no existe la OC ', @numero_oc,
                                  N' para el cliente ', @adm_cliente_id, N' y sociedad ', @sociedad, N'.');
                THROW 50301, @msg, 1;
            END

            COMMIT;
        END
        ELSE IF @action_code = 'APPROVE' OR @action_code = 'REFUSED'
        BEGIN
            DECLARE @nuevo_estado varchar(18) = CASE WHEN @action_code = 'APPROVE' THEN 'Aprobado' ELSE 'Rechazado' END;

            BEGIN TRAN;

            set @validaOC = '';
            select @validaOC = numero_oc
            from tb_orden_compra_cab WITH (UPDLOCK, HOLDLOCK)
            where adm_cliente_id = @adm_cliente_id
            and sociedad = @sociedad
            and numero_oc = @numero_oc;

            if @validaOC <> ''
            begin
                update tb_orden_compra_cab
                    set estado_confirmacion = @nuevo_estado,
                    observacion_confirmacion = @observacion,
                    fecha_confirmacion = getdate()
                    where adm_cliente_id = @adm_cliente_id
                    and sociedad = @sociedad
                    and numero_oc = @numero_oc;

                SET @filas = @@ROWCOUNT;
                IF @filas = 0
                BEGIN
                    SET @msg = CONCAT(N'sp_mantenerOC (', @action_code, N'): no se pudo actualizar la OC ', @numero_oc, N'.');
                    THROW 50302, @msg, 1;
                END

                delete from log_orden_compra_not
                    where adm_cliente_id = @adm_cliente_id
                    and codigo_sociedad = @sociedad
                    and numero_oc = @numero_oc;
            end
            else
            begin
                insert into dbo.tb_orden_compra_cab
                    (adm_cliente_id,sociedad,numero_oc,tipo_oc,fecha_emision,
                    fecha_etd,fecha_produccion,fecha_recibido_bodega,
                    estado_oc,codigo_proveedor,identificacion_fiscal,denominacion_fiscal,moneda,
                    forma_pago,condicion_pago,numero_oferta,importe_total,impuesto_general,
                    importe_final,estado_confirmacion,fecha_confirmacion,observacion_confirmacion,
                    reg_estado,reg_fecha_creacion,reg_usuario_creacion)
                values
                    (@adm_cliente_id,@sociedad,@numero_oc,@tipo_oc,@fecha_emision,
                    @fecha_etd,@fecha_produccion,@fecha_recibido_bodega,
                    @estado_oc,@codigo_proveedor,@identificacion_fiscal,@denominacion_fiscal,@moneda,
                    @forma_pago,@condicion_pago,@numero_oferta,@importe_total,@impuesto_general,
                    @importe_final,@nuevo_estado,getdate(),@observacion,
                    '1',getdate(),'2');
            end

            COMMIT;
        END
        -- Otros @action_code: sin accion (comportamiento original).
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

/* ---------------------------------------------------------------------
   sp_eliminarOC
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_eliminarOC](
    @adm_cliente_id varchar(12),
    @numero_oc varchar(18)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        delete from tb_orden_compra_det
        where adm_cliente_id = @adm_cliente_id
        and numero_oc = @numero_oc;

        delete from tb_orden_compra_cab
        where adm_cliente_id = @adm_cliente_id
        and numero_oc = @numero_oc;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

/* ---------------------------------------------------------------------
   sp_eliminarCalificacionProveedor (una sola sentencia: atomica por si misma)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_eliminarCalificacionProveedor]
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    delete from [dbo].[tb_calificacion_proveedor];
END
GO

/* ---------------------------------------------------------------------
   sp_mantenerOCDet
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_mantenerOCDet](
    @action_code varchar(18),
    @adm_cliente_id varchar(12),
    @numero_oc varchar(18),
    @posicion_oc varchar(12),
    @cod_material varchar(18),
    @des_material varchar(250),
    @um_material varchar(12),
    @cantidad decimal(18, 5),
    @cantidad_reparto decimal(18, 5),
    @cantidad_por_entregar decimal(18, 5),
    @cantidad_entregada decimal(18, 5),
    @precio decimal(18, 5),
    @total decimal(18, 5),
    @simbolo_moneda varchar(1),
    @numero_solped varchar(18),
    @posicion_solped varchar(12),
    @fecha_entrega_solped varchar(10),
    @fecha_entrega varchar(10),
    @fecha_entrega_estadistica varchar(10),
    @estado_posicion_oc varchar(18)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg nvarchar(400);

    -- Cada rama es una unica sentencia (atomica); no se abre transaccion propia
    -- para no interferir con una transaccion del llamador. XACT_ABORT ON
    -- garantiza que un error aborte la transaccion externa, si existe.
        IF @action_code = 'CREATE'
        BEGIN
            insert into tb_orden_compra_det
                (adm_cliente_id,numero_oc,posicion_oc,cod_material,des_material,
                um_material,cantidad,cantidad_reparto,cantidad_por_entregar,cantidad_entregada,
                precio,total,simbolo_moneda,numero_solped,posicion_solped,fecha_entrega_solped,
                fecha_entrega,fecha_entrega_estadistica,estado_posicion_oc,reg_estado,reg_fecha_creacion,
                reg_usuario_creacion)
            values
                (@adm_cliente_id,@numero_oc,@posicion_oc,@cod_material,@des_material,
                @um_material,@cantidad,@cantidad_reparto,@cantidad_por_entregar,@cantidad_entregada,
                @precio,@total,@simbolo_moneda,@numero_solped,@posicion_solped,@fecha_entrega_solped,
                @fecha_entrega,@fecha_entrega_estadistica,@estado_posicion_oc,'1',getdate(),
                '2');
        END
        ELSE IF @action_code = 'UPDATE'
        BEGIN
            update tb_orden_compra_det
            SET cod_material = @cod_material,
            des_material = @des_material,
            um_material = @um_material,
            cantidad = @cantidad,
            cantidad_reparto = @cantidad_reparto,
            cantidad_por_entregar = @cantidad_por_entregar,
            cantidad_entregada = @cantidad_entregada,
            precio = @precio,
            total = @total,
            simbolo_moneda = @simbolo_moneda,
            numero_solped = @numero_solped,
            posicion_solped = @posicion_solped,
            fecha_entrega_solped = @fecha_entrega_solped,
            fecha_entrega = @fecha_entrega,
            fecha_entrega_estadistica = @fecha_entrega_estadistica,
            estado_posicion_oc = @estado_posicion_oc,
            reg_estado = '1',
            reg_fecha_modificacion = getdate(),
            reg_usuario_modificacion = '2'
            where adm_cliente_id = @adm_cliente_id
            and numero_oc = @numero_oc
            and posicion_oc = @posicion_oc;

            -- Antes, un UPDATE de una posicion inexistente se perdia en silencio.
            -- Ahora se inserta (SAP puede agregar posiciones a una OC existente).
            IF @@ROWCOUNT = 0
            BEGIN
                insert into tb_orden_compra_det
                    (adm_cliente_id,numero_oc,posicion_oc,cod_material,des_material,
                    um_material,cantidad,cantidad_reparto,cantidad_por_entregar,cantidad_entregada,
                    precio,total,simbolo_moneda,numero_solped,posicion_solped,fecha_entrega_solped,
                    fecha_entrega,fecha_entrega_estadistica,estado_posicion_oc,reg_estado,reg_fecha_creacion,
                    reg_usuario_creacion)
                values
                    (@adm_cliente_id,@numero_oc,@posicion_oc,@cod_material,@des_material,
                    @um_material,@cantidad,@cantidad_reparto,@cantidad_por_entregar,@cantidad_entregada,
                    @precio,@total,@simbolo_moneda,@numero_solped,@posicion_solped,@fecha_entrega_solped,
                    @fecha_entrega,@fecha_entrega_estadistica,@estado_posicion_oc,'1',getdate(),
                    '2');
            END
        END
        -- Otros @action_code (APPROVE, REFUSED, vacios, etc.) no modifican el detalle, igual que antes.
END
GO

/* ---------------------------------------------------------------------
   sp_mantenerConstante
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_mantenerConstante](
    @reg_id varchar(18),
    @adm_cliente_id varchar(12),
    @tipo_constante varchar(12),
    @codigo_constante varchar(18),
    @valor_constante varchar(max),
    @descripcion varchar(100),
    @archivo char(1),
    @reg_estado char(1),
    @reg_usuario_creacion varchar(50)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @msg nvarchar(400);

    update cfg_constante
        set valor_constante = @valor_constante,
        descripcion = @descripcion,
        archivo = @archivo,
        -- solo se aplica un estado valido; otro valor ('' / NULL) conserva el actual
        reg_estado = CASE WHEN @reg_estado IN ('0','1') THEN @reg_estado ELSE reg_estado END,
        reg_fecha_modificacion = getdate(),
        reg_usuario_modificacion = @reg_usuario_creacion
        where adm_cliente_id = @adm_cliente_id
        and tipo_constante = @tipo_constante
        and codigo_constante = @codigo_constante;

    IF @@ROWCOUNT = 0
    BEGIN
        SET @msg = CONCAT(N'sp_mantenerConstante: no existe la constante ', @tipo_constante, N'/', @codigo_constante,
                          N' para el cliente ', @adm_cliente_id, N'.');
        THROW 50320, @msg, 1;
    END
END
GO

/* ---------------------------------------------------------------------
   sp_validarOCPendiente
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_validarOCPendiente](
    @adm_cliente_id varchar(12)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @intervalo int;

    select @intervalo = TRY_CONVERT(int, valor_constante)
    from cfg_constante
    where adm_cliente_id = @adm_cliente_id
    and tipo_constante = 'INTERVALO'
    and codigo_constante = 'OC_PEND';

    BEGIN TRY
        BEGIN TRAN;

        merge into log_orden_compra_not WITH (HOLDLOCK) as loc
        using(select adm_cliente_id,
            sociedad,
            numero_oc,
            codigo_proveedor,
            isnull(reg_fecha_modificacion, reg_fecha_creacion) as fecha_registro,
            datediff(minute, isnull(reg_fecha_modificacion, reg_fecha_creacion), getdate()) as intervalo
            from tb_orden_compra_cab
            where adm_cliente_id = @adm_cliente_id
            and estado_confirmacion = 'Pendiente'
            and estado_oc = 'Abierto'
            and codigo_proveedor is not null
            and datediff(minute, isnull(reg_fecha_modificacion, reg_fecha_creacion), getdate()) > @intervalo) as ref
        on loc.adm_cliente_id = ref.adm_cliente_id
        and loc.codigo_sociedad = ref.sociedad
        and loc.numero_oc = ref.numero_oc
        and loc.codigo_proveedor = ref.codigo_proveedor
        when matched and loc.reg_estado = '0' then
            update set
            loc.fecha_registro = ref.fecha_registro,
            loc.indicador_notificacion = ref.intervalo,
            loc.reg_estado = '1',
            loc.reg_fecha_modificacion = getdate(),
            loc.reg_usuario_modificacion = '1'
        when not matched then
            insert (adm_cliente_id,codigo_sociedad,numero_oc,codigo_proveedor,fecha_registro,indicador_notificacion,reg_estado,reg_fecha_creacion,reg_usuario_creacion)
            values (ref.adm_cliente_id,ref.sociedad,ref.numero_oc,ref.codigo_proveedor,ref.fecha_registro,ref.intervalo,'1',getdate(),'1');

        update log_orden_compra_not
        set reg_estado = '1'
        where adm_cliente_id = @adm_cliente_id
        and reg_estado = '2'
        and datediff(minute, fecha_notificacion, getdate()) > @intervalo;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH

    select loc.codigo_sociedad,
    soc.denominacion_sociedad,
    loc.numero_oc,
    loc.codigo_proveedor,
    pro.denominacion_fiscal,
    pro.correo,
    pro.idioma,
    convert(varchar,loc.fecha_registro,103) as fecha_registro
    from log_orden_compra_not loc
    inner join tb_proveedor pro
    on loc.adm_cliente_id = pro.adm_cliente_id
    and loc.codigo_sociedad = pro.sociedad
    and loc.codigo_proveedor = pro.codigo_proveedor
    inner join tb_sociedad soc
    on loc.adm_cliente_id = soc.adm_cliente_id
    and loc.codigo_sociedad = soc.codigo_sociedad
    where loc.adm_cliente_id = @adm_cliente_id
    and loc.reg_estado = '1';
END
GO

/* ---------------------------------------------------------------------
   sp_buscarOC
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_buscarOC](
    @operador varchar(2),
    @adm_cliente_id varchar(12),
    @sociedad varchar(18),
    @numero_oc varchar(18),
    @fecha_emision_d varchar(10),
    @fecha_emision_h varchar(10),
    @estado_oc varchar(18),
    @codigo_proveedor varchar(18),
    @tipo_oc varchar(12),
    @estado_confirmacion varchar(18),
    @idioma varchar(2) ='ES'
)
AS
BEGIN
    SET NOCOUNT ON;

    if @operador = 'B'
        select adm_cliente_id,
        oc.numero_oc,
        tipo_oc,
        fecha_emision,
        fecha_etd,
        fecha_produccion,
        fecha_recibido_bodega,
        estado_oc,
        codigo_proveedor,
        identificacion_fiscal,
        denominacion_fiscal,
        moneda,
        case when @idioma='ES' THEN co.nombre_es
            when @idioma='EN' THEN co.nombre_EN
            ELSE forma_pago END forma_pago,
        numero_oferta,
        importe_total,
        impuesto_general,
        importe_final,
        oc.reg_estado,
        oc.reg_id,
        estado_confirmacion,
        observacion_confirmacion,
        COALESCE(arc.total,0) archivo,
        DATEADD(MINUTE, -300, fecha_confirmacion) as fecha_confirmacion,
        DATEADD(MINUTE, -300, oc.reg_fecha_creacion) as reg_fecha_creacion
        from tb_orden_compra_cab oc
        left join [dbo].[tb_condicion_pago] co on co.codigo=oc.[condicion_pago] and co.reg_estado = '1'
        LEFT join (select numero_oc,sociedad,count(*) total from [dbo].[tb_orden_compra_arc] where [reg_estado]='1' group by [numero_oc],[sociedad]) arc
        on oc.numero_oc=arc.numero_oc and oc.sociedad=arc.sociedad
        where adm_cliente_id = @adm_cliente_id
        and oc.sociedad = @sociedad
        and (@numero_oc = '' OR @numero_oc IS NULL OR oc.numero_oc like '%' + @numero_oc + '%')
        and fecha_emision between @fecha_emision_d and @fecha_emision_h
        and (@codigo_proveedor = '' OR @codigo_proveedor IS NULL OR codigo_proveedor like '%' + @codigo_proveedor + '%')
        and (@tipo_oc = '' OR @tipo_oc IS NULL OR tipo_oc like '%' + @tipo_oc + '%')
    else if @operador = 'OC'
        select adm_cliente_id,
            numero_oc,
            tipo_oc,
            fecha_emision,
            fecha_etd,
            fecha_produccion,
            fecha_recibido_bodega,
            estado_oc,
            codigo_proveedor,
            identificacion_fiscal,
            denominacion_fiscal,
            moneda,
            case when @idioma='ES' THEN co.nombre_es
            when @idioma='EN' THEN co.nombre_EN
            ELSE forma_pago END forma_pago,
            numero_oferta,
            importe_total,
            impuesto_general,
            importe_final,
            oc.reg_estado,
            oc.reg_id,
            estado_confirmacion,
            observacion_confirmacion,
            '' archivo,
            DATEADD(MINUTE, -300, fecha_confirmacion) as fecha_confirmacion,
            DATEADD(MINUTE, -300, oc.reg_fecha_creacion) as reg_fecha_creacion
        from tb_orden_compra_cab OC
        left join [dbo].[tb_condicion_pago] co on co.codigo=oc.[condicion_pago] and co.reg_estado = '1'
        where adm_cliente_id = @adm_cliente_id
        and sociedad = @sociedad
        and numero_oc = @numero_oc
    else if @operador = 'OD'
        select numero_oc,
        posicion_oc,
        cod_material,
        des_material,
        um_material,
        cantidad,
        cantidad_reparto,
        cantidad_por_entregar,
        cantidad_entregada,
        precio,
        total,
        simbolo_moneda,
        numero_solped,
        posicion_solped,
        fecha_entrega_solped,
        fecha_entrega,
        fecha_entrega_estadistica,
        estado_posicion_oc
        from tb_orden_compra_det
        where adm_cliente_id = @adm_cliente_id
        and numero_oc = @numero_oc
        and estado_posicion_oc <> 'Borrado'
    else if @operador = 'HC'
        select adm_cliente_id,
            reg_version,
            numero_oc,
            tipo_oc,
            fecha_emision,
            fecha_etd,
            fecha_produccion,
            fecha_recibido_bodega,
            estado_oc,
            codigo_proveedor,
            identificacion_fiscal,
            denominacion_fiscal,
            moneda,
            case when @idioma='ES' THEN co.nombre_es
            when @idioma='EN' THEN co.nombre_EN
            ELSE forma_pago END forma_pago,
            numero_oferta,
            importe_total,
            impuesto_general,
            importe_final,
            lo.reg_estado,
            estado_confirmacion,
            observacion_confirmacion,
            '' archivo,
            DATEADD(MINUTE, -300, fecha_confirmacion) as fecha_confirmacion,
            DATEADD(MINUTE, -300, lo.reg_fecha_creacion) as reg_fecha_creacion
        from log_orden_compra_cab lo
        left join [dbo].[tb_condicion_pago] co on co.codigo=lo.[condicion_pago] and co.reg_estado = '1'
        where adm_cliente_id = @adm_cliente_id
        and sociedad = @sociedad
        and numero_oc = @numero_oc
    else if @operador = 'HD'
        select reg_version,
        numero_oc,
        posicion_oc,
        cod_material,
        des_material,
        um_material,
        cantidad,
        cantidad_reparto,
        cantidad_por_entregar,
        cantidad_entregada,
        precio,
        total,
        simbolo_moneda,
        numero_solped,
        posicion_solped,
        fecha_entrega_solped,
        fecha_entrega,
        fecha_entrega_estadistica,
        estado_posicion_oc
        from log_orden_compra_det
        where adm_cliente_id = @adm_cliente_id
        and numero_oc = @numero_oc
END
GO

/* ---------------------------------------------------------------------
   sp_buscarProveedor
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_buscarProveedor](
    @adm_cliente_id varchar(12),
    @sociedad varchar(18),
    @codigo_proveedor varchar(18),
    @identificacion_fiscal varchar(18),
    @denominacion_fiscal varchar(250)
)
AS
BEGIN
    SET NOCOUNT ON;

    select sociedad,
        codigo_proveedor,
        denominacion_fiscal,
        identificacion_fiscal,
        tipo_nif,
        direccion,
        correo,
        telefono,
        pais,
        idioma,
        bloqueo_contabilizar,
        peticion_borrado
    from tb_proveedor
    where reg_estado = '1'
    and adm_cliente_id = @adm_cliente_id
    and sociedad = @sociedad
    and (@codigo_proveedor = '' OR @codigo_proveedor IS NULL OR codigo_proveedor like '%' + @codigo_proveedor + '%')
    and (@identificacion_fiscal = '' OR @identificacion_fiscal IS NULL OR identificacion_fiscal like '%' + @identificacion_fiscal + '%')
    and (@denominacion_fiscal = '' OR @denominacion_fiscal IS NULL OR denominacion_fiscal like '%' + @denominacion_fiscal + '%')
    order by denominacion_fiscal asc
END
GO

/* ---------------------------------------------------------------------
   sp_listarUsuario (identico a 001: sin clave + LIKE tolerante a NULL)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_listarUsuario](
    @indicador varchar(2),
    @adm_cliente_id varchar(12),
    @codigo_sociedad varchar(18),
    @codigo_referencia varchar(18),
    @denominacion_fiscal varchar(250),
    @identificacion_fiscal varchar(30)
)
AS
BEGIN
    SET NOCOUNT ON;

    if @indicador = '1'
        select reg_id,
          adm_cliente_id,
          codigo_sociedad,
          codigo_referencia,
          codigo_referencia_aux,
          usuario,
          rol_id,
          denominacion_usuario,
          correo_referencia,
          referencia_id,
          reg_estado,
          reg_fecha_creacion,
          reg_usuario_creacion,
          reg_fecha_modificacion,
          reg_usuario_modificacion,
          '' as denominacion_fiscal,
          '' as identificacion_fiscal,
          '' as idioma
        from tb_usuario
        where adm_cliente_id = @adm_cliente_id
        and codigo_sociedad = @codigo_sociedad
        and codigo_referencia = '0'
        and (@denominacion_fiscal = '' OR @denominacion_fiscal IS NULL OR denominacion_usuario like '%' + @denominacion_fiscal + '%')
    else if @indicador = '2'
        select u.reg_id,
          u.adm_cliente_id,
          u.codigo_sociedad,
          u.codigo_referencia,
          u.codigo_referencia_aux,
          u.usuario,
          u.rol_id,
          isnull(r.denominacion_usuario,'-') as denominacion_usuario,
          isnull(r.correo_referencia,'-') as correo_referencia,
          u.referencia_id,
          u.reg_estado,
          u.reg_fecha_creacion,
          u.reg_usuario_creacion,
          u.reg_fecha_modificacion,
          u.reg_usuario_modificacion,
          p.denominacion_fiscal,
          p.identificacion_fiscal,
          p.idioma
        from tb_usuario u
        inner join tb_proveedor p
        on u.adm_cliente_id = p.adm_cliente_id
        and u.codigo_sociedad = p.sociedad
        and u.codigo_referencia = p.codigo_proveedor
        left join tb_usuario r
        on u.referencia_id = r.reg_id
        where u.adm_cliente_id = @adm_cliente_id
        and u.codigo_sociedad = @codigo_sociedad
        and u.codigo_referencia <> '0'
        and (@codigo_referencia = '' OR @codigo_referencia IS NULL OR u.codigo_referencia like '%' + @codigo_referencia + '%')
        and (@denominacion_fiscal = '' OR @denominacion_fiscal IS NULL OR p.denominacion_fiscal like '%' + @denominacion_fiscal + '%')
        and (@identificacion_fiscal = '' OR @identificacion_fiscal IS NULL OR p.identificacion_fiscal like '%' + @identificacion_fiscal + '%')
END
GO

PRINT '003_fix_bugs_sp.sql aplicado.';
GO
