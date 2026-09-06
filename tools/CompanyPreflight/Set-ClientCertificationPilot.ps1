param([ValidateSet('Preview','ApplyClone','ApplyLive')][string]$Mode='Preview')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidenceDir=Join-Path $repo 'tmp/client-certification-release-2026-09-05'
$manifest=Get-Content -LiteralPath "$evidenceDir/clone-manifest.json" -Raw|ConvertFrom-Json
$rehearsal=Get-Content -LiteralPath "$evidenceDir/rehearsal-results.json" -Raw|ConvertFrom-Json
if($manifest.SourceDatabase-cne'NeoSTP_Cloud'-or$manifest.RunId-notmatch'^[a-f0-9]{32}$'-or$manifest.TargetDatabase-cne('NeoProductionAudit_'+$manifest.RunId)){throw 'VERIFIED_CLONE_IDENTITY_REJECTED'}
if($rehearsal.Passed-ne22-or$rehearsal.Failed-ne0-or$rehearsal.TargetDatabase-cne$manifest.TargetDatabase-or$rehearsal.AppliedMigrations.Count-ne91){throw 'REHEARSAL_NOT_VERIFIED'}
if((Get-FileHash -LiteralPath $manifest.BackupFile -Algorithm SHA256).Hash-cne$manifest.BackupSha256){throw 'BACKUP_HASH_CHANGED'}
$settings=Get-Content -LiteralPath (Join-Path $repo 'src/NeoSTP.Api/appsettings.Local.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.ConnectionStrings.NeoStpDb)
if($builder.InitialCatalog-cne'NeoSTP_Cloud'-or$builder.DataSource-notin@('.','(local)','localhost',[Environment]::MachineName)){throw 'DATABASE_TARGET_REJECTED'}
$target=if($Mode-eq'ApplyLive'){'NeoSTP_Cloud'}else{$manifest.TargetDatabase}
if($Mode-ne'ApplyLive'-and($target-eq'NeoSTP_Cloud'-or$target-cne('NeoProductionAudit_'+$manifest.RunId)-or$target-notmatch'^NeoProductionAudit_[a-f0-9]{32}$')){throw 'CLONE_TARGET_REJECTED'}
$builder['Initial Catalog']=$target;$builder['Pooling']=$false
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$transaction=$null
$campaign=[guid]'5f681bfb-149d-4650-9a1b-5723feeebef3'
$report=[ordered]@{ObservedAtUtc=[DateTime]::UtcNow.ToString('O');Mode=$Mode;TargetDatabase=$target;EmpresaId=23;CampaignPublicId=$campaign.ToString();Applied=$false;HaciendaCalls=0;AllowedTypes=@('01','03','11','14');PilotBudget=4}
try{
 $connection.Open()
 $cmd=$connection.CreateCommand();$cmd.CommandText="SELECT CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)),CAST(SERVERPROPERTY('ProductMajorVersion') AS int),DB_NAME(),(SELECT COUNT(*) FROM __EFMigrationsHistory)"
 $reader=$cmd.ExecuteReader();[void]$reader.Read();$valid=$reader.GetString(0)-ieq[Environment]::MachineName-and$reader.GetInt32(1)-eq16-and$reader.GetString(2)-ceq$target-and$reader.GetInt32(3)-eq91;$reader.Dispose();$cmd.Dispose()
 if(-not$valid){throw 'DATABASE_IDENTITY_OR_SCHEMA_REJECTED'}
 $cmd=$connection.CreateCommand();$cmd.CommandText='SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId';$reader=$cmd.ExecuteReader();$migrations=@();while($reader.Read()){$migrations+=$reader.GetString(0)};$reader.Dispose();$cmd.Dispose()
 if(($migrations-join'|')-cne(@($rehearsal.AppliedMigrations)-join'|')){throw 'EXACT_MIGRATION_HISTORY_MISMATCH'}
 if($Mode-eq'Preview'){$report.SchemaPreviewOnly=$true;$report.ClientProvisioningGuardsEvaluated=$false;return}
 $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
 $cmd=$connection.CreateCommand();$cmd.Transaction=$transaction;$cmd.CommandTimeout=30
 $cmd.CommandText=@'
DECLARE @lockResult int;
EXEC @lockResult=sys.sp_getapplock @Resource=N'NeoSTP:DTE-LIMIT:23',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lockResult<0 THROW 51000,'CLIENT_LOCK_FAILED',1;
IF (SELECT COUNT(*) FROM Core_Empresas e JOIN Dte_Configuracion c ON c.EmpresaId=e.Id WHERE e.Id=23 AND REPLACE(REPLACE(e.Nit,'-',''),' ','')='06232705261148' AND e.EstadoCodigo='ACTIVA' AND c.AmbienteCodigo='PRUEBAS')<>1 THROW 51000,'CLIENT_IDENTITY_REJECTED',1;
IF EXISTS(SELECT 1 FROM Dte_Configuracion WHERE EmpresaId=23 AND TiposDteAutorizadosCsv IS NOT NULL AND TiposDteAutorizadosCsv<>'01,03,11,14') THROW 51000,'EXISTING_AUTHORIZATION_CONFLICT',1;
IF EXISTS(SELECT 1 FROM Core_Empresas WHERE Id=23 AND (Departamento NOT IN ('La Libertad','LA_LIBERTAD','05') OR Municipio NOT IN ('La Libertad Este','La Libertad Centro','LA_LIBERTAD_ESTE','LA_LIBERTAD_CENTRO','26','24') OR NULLIF(Distrito,'') IS NOT NULL AND Distrito NOT IN ('San Juan Opico','SAN_JUAN_OPICO','15'))) THROW 51000,'EXISTING_TERRITORY_CONFLICT',1;
IF EXISTS(SELECT 1 FROM Core_Catalogos WHERE EmpresaId=23 AND Codigo IN ('DEPARTAMENTO_ES','MUNICIPIO_ES','DISTRITO_ES')) THROW 51000,'TENANT_CATALOG_OVERRIDE_REQUIRES_REVIEW',1;
IF (SELECT COUNT(*) FROM Core_Catalogos WHERE EmpresaId IS NULL AND Codigo IN ('DEPARTAMENTO_ES','MUNICIPIO_ES','DISTRITO_ES'))<>3 OR (SELECT COUNT(DISTINCT Codigo) FROM Core_Catalogos WHERE EmpresaId IS NULL AND Activo=1 AND Codigo IN ('DEPARTAMENTO_ES','MUNICIPIO_ES','DISTRITO_ES'))<>3 THROW 51000,'GLOBAL_CATALOG_SELECTION_AMBIGUOUS',1;
IF (SELECT COUNT(*) FROM Core_Catalogos c JOIN Core_CatalogoItems i ON i.CatalogoId=c.Id WHERE c.Codigo='DEPARTAMENTO_ES' AND c.EmpresaId IS NULL AND c.Activo=1 AND i.Codigo='LA_LIBERTAD' AND i.Activo=1 AND i.ParentCodigo IS NULL AND JSON_VALUE(i.MetadataJson,'$.codigoMH')='05')<>1 THROW 51000,'DEPARTMENT_CATALOG_REJECTED',1;
IF (SELECT COUNT(*) FROM Core_Catalogos c JOIN Core_CatalogoItems i ON i.CatalogoId=c.Id WHERE c.Codigo='MUNICIPIO_ES' AND c.EmpresaId IS NULL AND c.Activo=1 AND i.Codigo='LA_LIBERTAD_CENTRO' AND i.Activo=1 AND i.ParentCodigo='LA_LIBERTAD' AND JSON_VALUE(i.MetadataJson,'$.codigoMH')='24')<>1 THROW 51000,'MUNICIPALITY_CATALOG_REJECTED',1;
DECLARE @districtCatalog int=(SELECT Id FROM Core_Catalogos WHERE Codigo='DISTRITO_ES' AND EmpresaId IS NULL AND Activo=1);
IF @districtCatalog IS NULL THROW 51000,'DISTRICT_CATALOG_MISSING',1;
IF EXISTS(SELECT 1 FROM Core_CatalogoItems WHERE CatalogoId=@districtCatalog AND Codigo<>'SAN_JUAN_OPICO' AND Activo=1 AND ParentCodigo='LA_LIBERTAD_CENTRO' AND (Valor='San Juan Opico' OR JSON_VALUE(MetadataJson,'$.codigoMH')='15')) THROW 51000,'AMBIGUOUS_DISTRICT_MATCH',1;
IF EXISTS(SELECT 1 FROM Core_CatalogoItems WHERE CatalogoId=@districtCatalog AND Codigo='SAN_JUAN_OPICO' AND (Activo<>1 OR ParentCodigo<>'LA_LIBERTAD_CENTRO' OR ISNULL(JSON_VALUE(MetadataJson,'$.codigoMH'),'')<>'15')) THROW 51000,'DISTRICT_CATALOG_CONFLICT',1;
IF NOT EXISTS(SELECT 1 FROM Core_CatalogoItems WHERE CatalogoId=@districtCatalog AND Codigo='SAN_JUAN_OPICO')
 INSERT INTO Core_CatalogoItems(CatalogoId,Codigo,Valor,Descripcion,Orden,EsSistema,Activo,ParentCodigo,MetadataJson,CreatedAt,CreatedBy)
 VALUES(@districtCatalog,'SAN_JUAN_OPICO','San Juan Opico','CAT-008 MH; código y parentesco verificados 2026-09-05',15,1,1,'LA_LIBERTAD_CENTRO','{"codigoMH":"15","departamentoMH":"05","municipioMH":"24"}',GETUTCDATE(),'certification-provisioning');
DECLARE @before nvarchar(max)=(SELECT Departamento,Municipio,Distrito FROM Core_Empresas WHERE Id=23 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
UPDATE Core_Empresas SET Departamento='La Libertad',Municipio='La Libertad Centro',Distrito='SAN_JUAN_OPICO',Direccion=@street,UpdatedAt=GETUTCDATE(),UpdatedBy='certification-provisioning' WHERE Id=23;
UPDATE Dte_Configuracion SET TiposDteAutorizadosCsv='01,03,11,14',UpdatedAt=GETUTCDATE(),UpdatedBy='certification-provisioning' WHERE EmpresaId=23;
IF EXISTS(SELECT 1 FROM Dte_CertificationCampaigns WHERE EmpresaId=23 AND MatrixReference='CLIENT23-PILOT-4-V1' AND PublicId<>@campaign) THROW 51000,'OTHER_PILOT_CAMPAIGN_EXISTS',1;
IF NOT EXISTS(SELECT 1 FROM Dte_CertificationCampaigns WHERE PublicId=@campaign)
BEGIN
 INSERT INTO Dte_CertificationCampaigns(PublicId,EmpresaId,ExpectedNit,AmbienteCodigo,Status,StartsAtUtc,ExpiresAtUtc,TotalBudget,MatrixReference,CreatedAt,CreatedBy)
 VALUES(@campaign,23,'06232705261148','PRUEBAS','ACTIVE',TODATETIMEOFFSET(GETUTCDATE(),'+00:00'),DATEADD(day,2,TODATETIMEOFFSET(GETUTCDATE(),'+00:00')),4,'CLIENT23-PILOT-4-V1',GETUTCDATE(),'certification-provisioning');
 DECLARE @id int=SCOPE_IDENTITY();
 INSERT INTO Dte_CertificationCampaignTypeBudgets(CampaignId,TipoDteCodigo,Budget) VALUES(@id,'01',1),(@id,'03',1),(@id,'11',1),(@id,'14',1);
END;
IF (SELECT COUNT(*) FROM Dte_CertificationCampaigns WHERE PublicId=@campaign AND EmpresaId=23 AND ExpectedNit='06232705261148' AND AmbienteCodigo='PRUEBAS' AND Status='ACTIVE' AND TotalBudget=4 AND MatrixReference='CLIENT23-PILOT-4-V1' AND ExpiresAtUtc>SYSDATETIMEOFFSET())<>1 THROW 51000,'PILOT_CAMPAIGN_CONFLICT',1;
IF (SELECT COUNT(*) FROM Dte_CertificationCampaignTypeBudgets b JOIN Dte_CertificationCampaigns c ON c.Id=b.CampaignId WHERE c.PublicId=@campaign AND b.Budget=1 AND b.TipoDteCodigo IN ('01','03','11','14'))<>4 THROW 51000,'PILOT_BUDGET_CONFLICT',1;
INSERT INTO Core_Auditoria(EmpresaId,Username,Modulo,Accion,Entidad,EntidadId,DatosAntes,DatosDespues,Resultado,Detalle,CreatedAt)
VALUES(23,'certification-provisioning','DTE_CONFIGURACION','CLIENT_PILOT_PROVISIONED','Empresa','23',@before,'{"departamento":"05","municipio":"24","distrito":"15","tipos":"01,03,11,14","pilotBudget":4}','OK','Dirección confirmada por titular y catálogo MH; cuatro tipos según captura. No cambia ambiente ni acredita certificación.',GETUTCDATE());
'@
 [void]$cmd.Parameters.AddWithValue('@campaign',$campaign)
 [void]$cmd.Parameters.AddWithValue('@street','CALLE PRINCIPAL, 3 POL G-1, CASERIO LA LIMONERA')
 [void]$cmd.ExecuteNonQuery();$cmd.Dispose();$transaction.Commit();$report.Applied=$true
}catch{$report.FailureType=$_.Exception.GetType().Name;if($transaction){try{$transaction.Rollback()}catch{}};throw [InvalidOperationException]::new('Client provisioning failed; transaction rolled back, inspect fixed guards.')}
finally{if($transaction){$transaction.Dispose()};$connection.Dispose();$settings=$null;$builder=$null;$report|ConvertTo-Json -Depth 4|Set-Content -LiteralPath "$evidenceDir/provision-$Mode.json" -Encoding utf8;$report|ConvertTo-Json -Depth 4}
