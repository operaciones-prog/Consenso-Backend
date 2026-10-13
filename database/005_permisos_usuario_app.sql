/* =====================================================================
   005_permisos_usuario_app.sql
   ---------------------------------------------------------------------
   Objetivo : Rol de minimo privilegio para la aplicacion (backend .NET).
   Motor    : SQL Server 2019
   Idempotente: SI.

   Analisis del acceso de la app (archivos .cs de la carpeta Repositorys):
     - Todo el acceso es via procedimientos almacenados (CommandType.StoredProcedure)
       => GRANT EXECUTE ON SCHEMA::dbo. Por encadenamiento de propiedad (todos
       los objetos son de dbo) los SP pueden leer/escribir tablas, la vista
       v_Meses y las funciones sin permisos directos sobre ellas.
     - UNICA excepcion: OCRepository (~linea 230) ejecuta SQL ad-hoc con
       CommandType.Text: "insert into tb_orden_compra_det (...) values (...)".
       Requiere INSERT directo sobre dbo.tb_orden_compra_det (no SELECT).
       No se encontro ningun SELECT ad-hoc => no se otorga SELECT.
       Si en el futuro ese INSERT se reemplaza por sp_mantenerOCDet, revocar:
         REVOKE INSERT ON dbo.tb_orden_compra_det FROM rol_app_consenso;

   NO crea logins ni contiene contrasenas. El DBA debe crear el login/usuario
   segun la politica de la organizacion (ver plantilla comentada al final).
   ===================================================================== */

-- 1) Rol
IF DATABASE_PRINCIPAL_ID(N'rol_app_consenso') IS NULL
BEGIN
    CREATE ROLE rol_app_consenso AUTHORIZATION dbo;
    PRINT 'Rol rol_app_consenso creado.';
END
ELSE PRINT 'Rol rol_app_consenso ya existe.';
GO

-- 2) Permisos (GRANT es idempotente)
GRANT EXECUTE ON SCHEMA::dbo TO rol_app_consenso;
GRANT INSERT ON OBJECT::dbo.tb_orden_compra_det TO rol_app_consenso;
GO

PRINT '005_permisos_usuario_app.sql aplicado.';
GO

/* ---------------------------------------------------------------------
   3) PLANTILLA (comentada) para el DBA
   ---------------------------------------------------------------------
   -- En master (definir la contrasena fuera de este repositorio, p.ej. desde
   -- AWS Secrets Manager; NUNCA versionarla):
   -- USE master;
   -- CREATE LOGIN app_consenso WITH PASSWORD = '<definir>', CHECK_POLICY = ON;

   -- En la BD de la aplicacion:
   -- CREATE USER app_consenso FOR LOGIN app_consenso WITH DEFAULT_SCHEMA = dbo;
   -- ALTER ROLE rol_app_consenso ADD MEMBER app_consenso;

   -- Ejemplo QAS: usuario existente qa_user
   -- ALTER ROLE rol_app_consenso ADD MEMBER qa_user;
   -- Luego quitarle privilegios excesivos, si los tuviera, p.ej.:
   -- ALTER ROLE db_owner     DROP MEMBER qa_user;
   -- ALTER ROLE db_datawriter DROP MEMBER qa_user;
   -- ALTER ROLE db_datareader DROP MEMBER qa_user;
   -- ALTER ROLE db_ddladmin  DROP MEMBER qa_user;

   -- Verificar membresias del usuario:
   -- SELECT r.name AS rol, m.name AS miembro
   -- FROM sys.database_role_members rm
   -- JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
   -- JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
   -- WHERE m.name IN (N'qa_user', N'app_consenso');
   --------------------------------------------------------------------- */
