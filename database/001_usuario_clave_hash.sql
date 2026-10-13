/* =====================================================================
   001_usuario_clave_hash.sql
   ---------------------------------------------------------------------
   Objetivo : Soportar claves hasheadas (PBKDF2/BCrypt, etc.) en tb_usuario
              y dejar de exponer la columna clave en listados.
   Motor    : SQL Server 2019
   Idempotente: SI (ALTER condicionado + CREATE OR ALTER).
   Ejecutar : en la BD de la aplicacion, ANTES de desplegar el backend nuevo.

   Cambios:
     - tb_usuario.clave           : varchar(50) NULL -> varchar(256) NULL
     - sp_validarUsuario          : @clave varchar(256). Resto identico.
     - sp_mantenerUsuario         : @clave varchar(256), @codigo_sociedad varchar(18);
                                    en UPDATE conserva la clave si @clave es NULL o '';
                                    ambos UPDATE dentro de una transaccion.
     - sp_obtenerCredencialUsuario: NUEVO. Devuelve reg_id, clave (hash) del usuario activo.
     - sp_actualizarClaveUsuario  : NUEVO. Actualiza la clave (hash) por reg_id.
     - sp_listarUsuario           : se elimina la columna clave de ambos result sets
                                    (+ filtros LIKE tolerantes a NULL, igual que en 003).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   1) Ampliar columna clave (se mantiene NULL, igual que hoy)
   --------------------------------------------------------------------- */
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.tb_usuario')
      AND c.name = N'clave'
      AND c.max_length <> -1
      AND c.max_length < 256
)
BEGIN
    ALTER TABLE dbo.tb_usuario ALTER COLUMN clave varchar(256) NULL;
    PRINT 'tb_usuario.clave ampliada a varchar(256).';
END
ELSE
    PRINT 'tb_usuario.clave ya tiene longitud >= 256. Sin cambios.';
GO

/* ---------------------------------------------------------------------
   2) sp_validarUsuario (solo se amplia @clave)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_validarUsuario](
    @adm_cliente_id varchar(12),
    @codigo_sociedad varchar(18),
    @usuario varchar(50),
    @clave varchar(256),
    @codigo_referencia varchar(18) output,
    @rol_id int output
)
AS
BEGIN
    SET NOCOUNT ON;

    set @codigo_referencia = '';
    set @rol_id = 0;

    select @codigo_referencia = codigo_referencia
    from tb_usuario
    where adm_cliente_id = @adm_cliente_id
    and codigo_sociedad = @codigo_sociedad
    and usuario = @usuario
    and clave = @clave
    and reg_estado = '1'

    select @rol_id = rol_id
    from tb_usuario
    where adm_cliente_id = @adm_cliente_id
    and codigo_sociedad = @codigo_sociedad
    and usuario = @usuario
    and clave = @clave
    and reg_estado = '1'

    select u.reg_id,
        u.adm_cliente_id,
        u.codigo_sociedad,
        u.codigo_referencia,
        u.usuario,
        u.rol_id,
        u.denominacion_usuario,
        u.correo_referencia,
        u.reg_estado,
        r.nombre_rol,
        r.nombre_rolEN
    from tb_usuario u
    inner join tb_rol r
    on u.rol_id = r.reg_id
    where u.adm_cliente_id = @adm_cliente_id
    and u.codigo_sociedad = @codigo_sociedad
    and u.usuario = @usuario
    and u.clave = @clave
    and u.reg_estado = '1'
END
GO

/* ---------------------------------------------------------------------
   3) sp_mantenerUsuario
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_mantenerUsuario](
    @reg_id int,
    @adm_cliente_id varchar(12),
    @codigo_sociedad varchar(18),
    @codigo_referencia varchar(18),
    @codigo_referencia_aux varchar(18),
    @usuario varchar(50),
    @clave varchar(256),
    @rol_id int,
    @denominacion_usuario varchar(250),
    @correo_referencia varchar(50),
    @referencia_id int,
    @idioma char(2),
    @reg_estado char(1),
    @reg_usuario_creacion varchar(50)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        IF @reg_id = 0
        BEGIN
            insert into tb_usuario
                (adm_cliente_id,codigo_sociedad,codigo_referencia,codigo_referencia_aux,usuario,
                clave,rol_id,denominacion_usuario,correo_referencia,referencia_id,reg_estado,reg_fecha_creacion,
                reg_usuario_creacion)
            values
                (@adm_cliente_id,@codigo_sociedad,@codigo_referencia,@codigo_referencia_aux,@usuario,
                @clave,@rol_id,@denominacion_usuario,@correo_referencia,@referencia_id,'1',getdate(),
                @reg_usuario_creacion);
        END
        ELSE IF @reg_id > 0
        BEGIN
            BEGIN TRAN;

            update tb_usuario
                set adm_cliente_id = @adm_cliente_id,
                codigo_sociedad = @codigo_sociedad,
                codigo_referencia = @codigo_referencia,
                codigo_referencia_aux = @codigo_referencia_aux,
                -- Si no llega clave nueva se conserva la almacenada (hash)
                clave = CASE WHEN @clave IS NULL OR @clave = '' THEN clave ELSE @clave END,
                rol_id = @rol_id,
                denominacion_usuario = @denominacion_usuario,
                correo_referencia = @correo_referencia,
                referencia_id = @referencia_id,
                reg_estado = @reg_estado,
                reg_fecha_modificacion = getdate(),
                reg_usuario_modificacion = @reg_usuario_creacion
                where adm_cliente_id = @adm_cliente_id
                and reg_id = @reg_id;

            update tb_proveedor
                set idioma = @idioma
                where adm_cliente_id = @adm_cliente_id
                and sociedad = @codigo_sociedad
                and codigo_proveedor = @codigo_referencia;

            COMMIT;
        END
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

/* ---------------------------------------------------------------------
   4) NUEVO sp_obtenerCredencialUsuario
      Devuelve el hash almacenado para que el backend lo verifique.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_obtenerCredencialUsuario](
    @adm_cliente_id varchar(12),
    @codigo_sociedad varchar(18),
    @usuario varchar(50)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT reg_id,
           clave
    FROM tb_usuario
    WHERE adm_cliente_id = @adm_cliente_id
      AND codigo_sociedad = @codigo_sociedad
      AND usuario = @usuario
      AND reg_estado = '1';
END
GO

/* ---------------------------------------------------------------------
   5) NUEVO sp_actualizarClaveUsuario
      Actualiza la clave (hash) de un usuario por reg_id. Usado p.ej. para
      migrar claves en texto plano a hash al iniciar sesion.
      reg_usuario_modificacion = '2' (usuario de sistema, Constantes.USUARIO_ID_ADM).
      Si reg_id no existe no hace nada (no lanza error).
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [dbo].[sp_actualizarClaveUsuario](
    @reg_id int,
    @clave varchar(256)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE tb_usuario
       SET clave = @clave,
           reg_fecha_modificacion = getdate(),
           reg_usuario_modificacion = '2'
     WHERE reg_id = @reg_id;
END
GO

/* ---------------------------------------------------------------------
   6) sp_listarUsuario: sin columna clave. Filtros LIKE tolerantes a NULL.
      (Definicion identica a la de 003_fix_bugs_sp.sql.)
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

PRINT '001_usuario_clave_hash.sql aplicado.';
GO
