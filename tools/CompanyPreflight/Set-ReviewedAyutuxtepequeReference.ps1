param([switch]$Apply)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Join-Path $repo 'tmp/mh-official-reference-2026-09-05/catalogo.xlsx'
if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash-cne'E86D7EDC503D876564CD2BF9B251FB100F838199330C0C513048F4075669B2C6'){throw 'OFFICIAL_REFERENCE_CHANGED'}
$settings=Get-Content -LiteralPath (Join-Path $repo 'src/NeoSTP.Api/appsettings.Local.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.ConnectionStrings.NeoStpDb)
if($builder.InitialCatalog-cne'NeoSTP_Cloud'-or$builder.DataSource-notin@('.','(local)','localhost',[Environment]::MachineName)-or$builder.AttachDBFilename){throw 'DATABASE_TARGET_REJECTED'}
$builder['Pooling']=$false
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$transaction=$null
$report=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('O');Applied=$false;Reference='MH Hoja1 fila158, Ayutuxtepeque 06/23/03';GlobalReferenceOnly=$true;CompanyConfigurationChanged=$false;HaciendaCalls=0}
try{
 $connection.Open()
 $command=$connection.CreateCommand()
 $command.CommandText="SELECT DB_NAME(),CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)),CAST(SERVERPROPERTY('ProductMajorVersion') AS int)"
 $reader=$command.ExecuteReader();[void]$reader.Read()
 $valid=$reader.GetString(0)-ceq'NeoSTP_Cloud'-and$reader.GetString(1)-ieq[Environment]::MachineName-and$reader.GetInt32(2)-eq16
 $reader.Dispose();$command.Dispose();if(-not$valid){throw 'DATABASE_IDENTITY_REJECTED'}
 $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
 $command=$connection.CreateCommand();$command.Transaction=$transaction;$command.CommandTimeout=30
 $command.CommandText=@'
DECLARE @lockResult int;
EXEC @lockResult=sys.sp_getapplock @Resource=N'NeoSTP:Catalog:DISTRITO_ES',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lockResult<0 THROW 51000,'CATALOG_LOCK_FAILED',1;
IF (SELECT COUNT(*) FROM __EFMigrationsHistory)<>91 THROW 51000,'SCHEMA_CHANGED',1;
IF (SELECT COUNT(*) FROM Core_Empresas e JOIN Dte_Configuracion f ON f.EmpresaId=e.Id WHERE e.Id=23 AND REPLACE(REPLACE(e.Nit,'-',''),' ','')='06232705261148' AND f.AmbienteCodigo='PRUEBAS' AND f.TiposDteAutorizadosCsv='01,03,11,14')<>1 THROW 51000,'CLIENT_CHANGED',1;
IF EXISTS(SELECT 1 FROM Core_Catalogos WHERE EmpresaId IN(2,23) AND Codigo IN('DEPARTAMENTO_ES','MUNICIPIO_ES','DISTRITO_ES')) THROW 51000,'TENANT_CATALOG_OVERRIDE_REQUIRES_REVIEW',1;
IF (SELECT COUNT(*) FROM Core_Catalogos WHERE EmpresaId IS NULL AND Codigo IN('DEPARTAMENTO_ES','MUNICIPIO_ES','DISTRITO_ES'))<>3 THROW 51000,'GLOBAL_CATALOG_AMBIGUOUS',1;
IF (SELECT COUNT(*) FROM Core_Catalogos c JOIN Core_CatalogoItems i ON i.CatalogoId=c.Id WHERE c.EmpresaId IS NULL AND c.Activo=1 AND c.Codigo='DEPARTAMENTO_ES' AND i.Codigo='SAN_SALVADOR' AND i.Activo=1 AND JSON_VALUE(i.MetadataJson,'$.codigoMH')='06')<>1 THROW 51000,'DEPARTMENT_REJECTED',1;
IF (SELECT COUNT(*) FROM Core_Catalogos c JOIN Core_CatalogoItems i ON i.CatalogoId=c.Id WHERE c.EmpresaId IS NULL AND c.Activo=1 AND c.Codigo='MUNICIPIO_ES' AND i.Codigo='SAN_SALVADOR_CENTRO' AND i.ParentCodigo='SAN_SALVADOR' AND i.Activo=1 AND JSON_VALUE(i.MetadataJson,'$.codigoMH')='23')<>1 THROW 51000,'MUNICIPALITY_REJECTED',1;
DECLARE @catalog int=(SELECT Id FROM Core_Catalogos WHERE EmpresaId IS NULL AND Codigo='DISTRITO_ES' AND Activo=1);
IF @catalog IS NULL THROW 51000,'DISTRICT_CATALOG_MISSING',1;
IF EXISTS(SELECT 1 FROM Core_CatalogoItems WHERE CatalogoId=@catalog AND Codigo<>'AYUTUXTEPEQUE' AND Activo=1 AND ParentCodigo='SAN_SALVADOR_CENTRO' AND (Valor='Ayutuxtepeque' OR JSON_VALUE(MetadataJson,'$.codigoMH')='03')) THROW 51000,'AMBIGUOUS_DISTRICT',1;
IF EXISTS(SELECT 1 FROM Core_CatalogoItems WHERE CatalogoId=@catalog AND Codigo='AYUTUXTEPEQUE' AND (Activo<>1 OR ParentCodigo<>'SAN_SALVADOR_CENTRO' OR ISNULL(JSON_VALUE(MetadataJson,'$.codigoMH'),'')<>'03')) THROW 51000,'CONFLICTING_DISTRICT',1;
DECLARE @exists bit=CASE WHEN EXISTS(SELECT 1 FROM Core_CatalogoItems WHERE CatalogoId=@catalog AND Codigo='AYUTUXTEPEQUE') THEN 1 ELSE 0 END;
IF @apply=1 AND @exists=0
BEGIN
 INSERT INTO Core_CatalogoItems(CatalogoId,Codigo,Valor,Descripcion,Orden,EsSistema,Activo,ParentCodigo,MetadataJson,CreatedAt,CreatedBy)
 VALUES(@catalog,'AYUTUXTEPEQUE','Ayutuxtepeque','Catálogo oficial MH, Hoja1 fila158, verificado 2026-09-05',3,1,1,'SAN_SALVADOR_CENTRO','{"codigoMH":"03","departamentoMH":"06","municipioMH":"23"}',GETUTCDATE(),'certification-provisioning');
 INSERT INTO Core_Auditoria(EmpresaId,Username,Modulo,Accion,Entidad,EntidadId,DatosAntes,DatosDespues,Resultado,Detalle,CreatedAt)
 VALUES(23,'certification-provisioning','CATALOGOS','VERIFIED_DISTRICT_REFERENCE_ADDED','CatalogoItem','AYUTUXTEPEQUE',NULL,'{"departamento":"06","municipio":"23","distrito":"03"}','OK','Referencia global oficial para receptor CCF de prueba. No modifica ninguna empresa ni configuración fiscal.',GETUTCDATE());
END;
SELECT @exists PreviouslyExisted,CASE WHEN @apply=1 AND @exists=0 THEN 1 ELSE 0 END Inserted;
'@
 [void]$command.Parameters.AddWithValue('@apply',[bool]$Apply)
 $reader=$command.ExecuteReader();[void]$reader.Read();$report.PreviouslyExisted=$reader.GetBoolean(0);$report.Inserted=$reader.GetInt32(1);$reader.Dispose();$command.Dispose()
 if($Apply){$transaction.Commit();$report.Applied=$true}else{$transaction.Rollback()}
}catch{if($transaction){try{$transaction.Rollback()}catch{}};throw}
finally{
 if($transaction){$transaction.Dispose()};$connection.Dispose();$builder=$null;$settings=$null
 $report|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $repo ('tmp/client-certification-release-2026-09-05/ayutuxtepeque-reference-'+[bool]$Apply+'.json')) -Encoding utf8
 $report|ConvertTo-Json
}
