param(
 [Parameter(Mandatory=$true)][int]$EmpresaId,
 [Parameter(Mandatory=$true)][AllowEmptyString()][string]$ExpectedNit
)
$ErrorActionPreference='Stop'
# CERT-0 is intentionally unable to authorize or perform transmission.
# Reject the wrong tenant before configuration/filesystem/database access.
if($EmpresaId -ne 23 -or $ExpectedNit -cne '06232705261148'){
 [ordered]@{Passed=$false;ReasonCode='CLIENT_IDENTITY_ARGUMENT_REJECTED';DatabaseConnectionOpened=$false;ReadyForTransmission=$false}|ConvertTo-Json
 exit 1
}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputDirectory=Join-Path $repo 'tmp/client-certification-2026-09-05'
$outputFile=Join-Path $outputDirectory 'readiness-sanitized.json'
$observedAt=[DateTime]::UtcNow
$monthStart=[DateTime]::new($observedAt.Year,$observedAt.Month,1,0,0,0,[DateTimeKind]::Utc)
$connection=$null;$configuration=$null;$builder=$null
$report=[ordered]@{
 GeneratedAtUtc=$observedAt.ToString('O');EmpresaId=23;Passed=$false;IdentityVerified=$false;EnvironmentVerified=$false
 DatabaseConnectionOpened=$false;SqlWritesIssued=$false;SecretsSelected=$false;FiscalPayloadsSelected=$false
 ReadyForTransmission=$false;PortalVerifiedLive=$false;CapturedAtUnknown=$true
 ExternalRequestsIssued=$false;SecretDecryptionPerformed=$false
 ScreenshotCounters=[ordered]@{Source='User supplied screenshot';CapturedAtUnknown=$true;PortalVerifiedLive=$false;OfficialScenarioMappingEstablished=$false;Types=@(
  [ordered]@{Tipo='01';ReportedCompleted=1;ReportedRequired=90;ArithmeticRemaining=89},
  [ordered]@{Tipo='03';ReportedCompleted=0;ReportedRequired=75;ArithmeticRemaining=75},
  [ordered]@{Tipo='11';ReportedCompleted=0;ReportedRequired=90;ArithmeticRemaining=90},
  [ordered]@{Tipo='14';ReportedCompleted=0;ReportedRequired=25;ArithmeticRemaining=25})}
}
# All SQL texts are fixed in this file. There is no SQL argument, stored procedure, EF or host container.
function Read-FixedRows([string]$query){
 $command=$connection.CreateCommand();$command.CommandTimeout=20;$command.CommandText=$query
 [void]$command.Parameters.AddWithValue('@empresa',23)
 [void]$command.Parameters.AddWithValue('@expectedNit',$ExpectedNit)
 [void]$command.Parameters.AddWithValue('@observedAt',$observedAt)
 [void]$command.Parameters.AddWithValue('@monthStart',$monthStart)
 try{
  $reader=$command.ExecuteReader()
  try{while($reader.Read()){
   $row=[ordered]@{}
   for($column=0;$column -lt $reader.FieldCount;$column++){$row[$reader.GetName($column)]=$(if($reader.IsDBNull($column)){$null}else{$reader.GetValue($column)})}
   [pscustomobject]$row
  }}finally{$reader.Dispose()}
 }finally{$command.Dispose()}
}
try{
 $configuration=Get-Content -LiteralPath (Join-Path $repo 'src/NeoSTP.Api/appsettings.Local.json') -Raw|ConvertFrom-Json
 $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($configuration.ConnectionStrings.NeoStpDb)
 $localSources=@('.','(local)','localhost',[Environment]::MachineName)
 if($builder.InitialCatalog -cne 'NeoSTP_Cloud' -or $builder.DataSource -notin $localSources){throw 'DATABASE_TARGET_REJECTED'}
 $builder['ApplicationIntent']='ReadOnly';$builder['Pooling']=$false
 $connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
 $connection.Open();$report.DatabaseConnectionOpened=$true
 $database=@(Read-FixedRows "SELECT DB_NAME() DatabaseName,CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) MachineName,CAST(SERVERPROPERTY('ProductMajorVersion') AS int) MajorVersion")[0]
 if($database.DatabaseName -cne 'NeoSTP_Cloud' -or $database.MachineName -ine [Environment]::MachineName -or $database.MajorVersion -ne 16){throw 'DATABASE_IDENTITY_REJECTED'}
 $report.DatabaseIdentityVerified=$true
 $identity=@(Read-FixedRows @'
SELECT e.Id,
 CASE WHEN REPLACE(REPLACE(e.Nit,'-',''),' ','')=@expectedNit THEN 1 ELSE 0 END NitMatchesExpected,
 e.EstadoCodigo CompanyState,c.AmbienteCodigo
 FROM dbo.Core_Empresas e JOIN dbo.Dte_Configuracion c ON c.EmpresaId=e.Id WHERE e.Id=@empresa
'@)
 if($identity.Count -ne 1 -or $identity[0].Id -ne 23 -or $identity[0].NitMatchesExpected -ne 1){throw 'CLIENT_DATABASE_IDENTITY_REJECTED'}
 $report.IdentityVerified=$true
 if($identity[0].AmbienteCodigo -cne 'PRUEBAS'){throw 'CLIENT_ENVIRONMENT_REJECTED'}
 $report.EnvironmentVerified=$true;$report.Ambiente='PRUEBAS';$report.CompanyState=$identity[0].CompanyState
 $schema=@(Read-FixedRows @'
SELECT (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) AppliedMigrationCount,
 CASE WHEN OBJECT_ID('dbo.Billing_PaymentApplications','U') IS NULL THEN 0 ELSE 1 END PaymentApplicationLedgerTablePresent,
 CASE WHEN OBJECT_ID('dbo.Billing_CheckoutIntents','U') IS NULL THEN 0 ELSE 1 END CheckoutIntentTablePresent
'@)[0]
 $report.Schema=$schema
 $report.FiscalMetadata=@(Read-FixedRows @'
SELECT
 CASE WHEN NULLIF(LTRIM(RTRIM(e.Nrc)),'') IS NULL THEN 0 ELSE 1 END NrcPresent,
 CASE WHEN LEN(e.CodigoActividad)=5 AND e.CodigoActividad NOT LIKE '%[^0-9]%' THEN 1 ELSE 0 END ActivityStoredAsFiveDigits,
 CASE WHEN LEN(e.Departamento)=2 AND e.Departamento NOT LIKE '%[^0-9]%' THEN 1 ELSE 0 END DepartmentStoredAsTwoDigits,
 CASE WHEN LEN(e.Municipio)=2 AND e.Municipio NOT LIKE '%[^0-9]%' THEN 1 ELSE 0 END MunicipalityStoredAsTwoDigits,
 CASE WHEN LEN(e.Distrito)=2 AND e.Distrito NOT LIKE '%[^0-9]%' THEN 1 ELSE 0 END DistrictStoredAsTwoDigits,
 CASE WHEN NULLIF(LTRIM(RTRIM(e.Distrito)),'') IS NULL THEN 0 ELSE 1 END DistrictPresent,
 CASE WHEN NULLIF(LTRIM(RTRIM(e.Direccion)),'') IS NULL THEN 0 ELSE 1 END AddressPresent,
 CASE WHEN NULLIF(LTRIM(RTRIM(e.Correo)),'') IS NULL THEN 0 ELSE 1 END ContactEmailPresent,
 CASE WHEN NULLIF(LTRIM(RTRIM(e.Telefono)),'') IS NULL THEN 0 ELSE 1 END PhonePresent,
 CASE WHEN c.CertificadoBlob IS NULL OR DATALENGTH(c.CertificadoBlob)=0 THEN 0 ELSE 1 END CertificatePresent,
 CASE WHEN NULLIF(c.PasswordMhCifrado,'') IS NULL THEN 0 ELSE 1 END ProtectedMhPasswordPresent,
 CASE WHEN NULLIF(c.PasswordCertificadoCifrado,'') IS NULL THEN 0 ELSE 1 END ProtectedCertificatePasswordPresent,
 c.CertificadoEmitido,c.CertificadoVence,
 (SELECT COUNT(*) FROM dbo.Core_Sucursales s WHERE s.EmpresaId=@empresa) BranchCount,
 (SELECT COUNT(*) FROM dbo.Core_PuntosVenta p JOIN dbo.Core_Sucursales s ON s.Id=p.SucursalId WHERE s.EmpresaId=@empresa) PointOfSaleCount,
 (SELECT COUNT(*) FROM dbo.Com_ConfiguracionCorreo mail WHERE mail.EmpresaId=@empresa AND mail.Activo=1) ActiveTenantSmtpConfigurationCount
 FROM dbo.Core_Empresas e JOIN dbo.Dte_Configuracion c ON c.EmpresaId=e.Id WHERE e.Id=@empresa
'@)[0]
 $report.StoredGeographyIsFormatOnly=$true;$report.GeographyCatalogValidated=$false
 $report.DocumentAggregates=@(Read-FixedRows @'
SELECT TipoDteCodigo,AmbienteCodigo,EstadoCodigo,COUNT_BIG(*) DocumentCount,
 SUM(CASE WHEN EstadoCodigo='PROCESADO' AND NULLIF(SelloRecibido,'') IS NOT NULL THEN 1 ELSE 0 END) LocallyProcessedWithSealCount,
 SUM(CASE WHEN EnviadoAt IS NOT NULL AND NULLIF(SelloRecibido,'') IS NULL AND EstadoCodigo IN ('ENVIADO','ERROR') THEN 1 ELSE 0 END) UncertainResultCandidateCount,
 SUM(CASE WHEN CreatedAt>=@monthStart THEN 1 ELSE 0 END) CurrentUtcMonthQuotaCount
 FROM dbo.Dte_Documentos WHERE EmpresaId=@empresa
 GROUP BY TipoDteCodigo,AmbienteCodigo,EstadoCodigo ORDER BY TipoDteCodigo,AmbienteCodigo,EstadoCodigo
'@)
 $report.LocalScenarioMetadata=@(Read-FixedRows @'
SELECT m.TipoDteCodigo,m.EscenariosRequeridos LocalConfiguredRequiredCount,COUNT(e.Id) ActiveScenarioCount,
 SUM(CASE WHEN e.Id IS NOT NULL AND (NULLIF(LTRIM(RTRIM(e.Descripcion)),'') IS NULL OR e.Descripcion LIKE '%Detalle pendiente%') THEN 1 ELSE 0 END) GenericOrMissingDescriptionCount,
 SUM(CASE WHEN e.Id IS NOT NULL AND NULLIF(LTRIM(RTRIM(e.Descripcion)),'') IS NOT NULL AND e.Descripcion NOT LIKE '%Detalle pendiente%' THEN 1 ELSE 0 END) NonPlaceholderDescriptionCandidateCount
 FROM dbo.Dte_CertificacionMatriz m LEFT JOIN dbo.Dte_CertificacionEscenarios e ON e.MatrizId=m.Id AND e.Activo=1
 WHERE m.Activo=1 AND m.TipoDteCodigo IN ('01','03','11','14')
 GROUP BY m.TipoDteCodigo,m.EscenariosRequeridos ORDER BY m.TipoDteCodigo
'@)
 $report.LatestLocalAssociations=@(Read-FixedRows @'
WITH Latest AS (
 SELECT p.Id,p.EmpresaId,p.EscenarioId,p.IntentoNumero,p.EstadoCodigo,p.DteDocumentoId,ROW_NUMBER() OVER(PARTITION BY p.EscenarioId ORDER BY p.IntentoNumero DESC,p.Id DESC) rn
 FROM dbo.Dte_CertificacionPruebas p WHERE p.EmpresaId=@empresa
)
SELECT m.TipoDteCodigo,COUNT(*) LatestAssociatedScenarioCount,
 SUM(CASE WHEN p.EstadoCodigo='COMPLETADO' THEN 1 ELSE 0 END) MarkedCompletedLocally,
 SUM(CASE WHEN p.EstadoCodigo='COMPLETADO' AND d.EmpresaId=@empresa AND d.TipoDteCodigo=m.TipoDteCodigo AND d.AmbienteCodigo='PRUEBAS' AND d.EstadoCodigo='PROCESADO' AND NULLIF(d.SelloRecibido,'') IS NOT NULL THEN 1 ELSE 0 END) CompletedWithMatchingLocalTestDocument,
 SUM(CASE WHEN p.EstadoCodigo='COMPLETADO' AND (d.Id IS NULL OR d.EmpresaId<>@empresa OR d.TipoDteCodigo<>m.TipoDteCodigo OR ISNULL(d.AmbienteCodigo,'')<>'PRUEBAS' OR d.EstadoCodigo<>'PROCESADO' OR NULLIF(d.SelloRecibido,'') IS NULL) THEN 1 ELSE 0 END) CompletedNeedingEvidenceReview,
 SUM(CASE WHEN p.EstadoCodigo IN ('PENDIENTE','EN_PROGRESO','ERROR') THEN 1 ELSE 0 END) OpenOrErrorAssociations,
 SUM(CASE WHEN d.EnviadoAt IS NOT NULL AND NULLIF(d.SelloRecibido,'') IS NULL AND d.EstadoCodigo IN ('ENVIADO','ERROR') THEN 1 ELSE 0 END) UncertainDocumentAssociationCandidates
 FROM Latest p JOIN dbo.Dte_CertificacionEscenarios e ON e.Id=p.EscenarioId AND e.Activo=1
 JOIN dbo.Dte_CertificacionMatriz m ON m.Id=e.MatrizId AND m.Activo=1
 LEFT JOIN dbo.Dte_Documentos d ON d.Id=p.DteDocumentoId
 WHERE p.rn=1 AND m.TipoDteCodigo IN ('01','03','11','14') GROUP BY m.TipoDteCodigo ORDER BY m.TipoDteCodigo
'@)
 $report.ProcessedTestDocumentsWithoutAssociation=@(Read-FixedRows @'
SELECT d.TipoDteCodigo,COUNT_BIG(*) ProcessedWithSealWithoutAnyLocalAssociation
 FROM dbo.Dte_Documentos d WHERE d.EmpresaId=@empresa AND d.AmbienteCodigo='PRUEBAS'
 AND d.TipoDteCodigo IN ('01','03','11','14') AND d.EstadoCodigo='PROCESADO' AND NULLIF(d.SelloRecibido,'') IS NOT NULL
 AND NOT EXISTS(SELECT 1 FROM dbo.Dte_CertificacionPruebas p WHERE p.EmpresaId=@empresa AND p.DteDocumentoId=d.Id)
 GROUP BY d.TipoDteCodigo ORDER BY d.TipoDteCodigo
'@)
 $licenses=@(Read-FixedRows @'
SELECT ep.Id LicenseId,ep.PlanId,p.Codigo PlanCode,p.LimiteDteMensual CatalogMonthlyDteLimit,ep.FechaInicio,ep.FechaFin,
 CASE WHEN p.LimiteUsuarios<0 OR p.LimiteSucursales<0 OR p.LimitePuntosVenta<0 OR p.LimiteDteMensual<0 THEN 1 ELSE 0 END InvalidNegativeCatalogQuota
 FROM dbo.Core_EmpresaPlan ep JOIN dbo.Core_Planes p ON p.Id=ep.PlanId
 WHERE ep.EmpresaId=@empresa AND ep.EstadoCodigo='ACTIVO' AND ep.FechaInicio<=@observedAt AND (ep.FechaFin IS NULL OR ep.FechaFin>@observedAt)
 ORDER BY ep.Id
'@)
 $used=@(Read-FixedRows 'SELECT COUNT_BIG(*) Used FROM dbo.Dte_Documentos WHERE EmpresaId=@empresa AND CreatedAt>=@monthStart')[0].Used
 $adoptedCount=0
 if($schema.PaymentApplicationLedgerTablePresent -eq 1 -and $schema.CheckoutIntentTablePresent -eq 1){
  $adoptedCount=@(Read-FixedRows @'
SELECT COUNT_BIG(*) AdoptedCount FROM dbo.Billing_PaymentApplications a JOIN dbo.Billing_CheckoutIntents i ON i.Id=a.BillingCheckoutIntentId
 WHERE i.EmpresaId=@empresa OR EXISTS(SELECT 1 FROM dbo.Core_EmpresaPlan ep WHERE ep.Id=a.EmpresaPlanId AND ep.EmpresaId=@empresa)
'@)[0].AdoptedCount
 }
 $quotaKnown=$licenses.Count -eq 1 -and $adoptedCount -eq 0 -and $licenses[0].InvalidNegativeCatalogQuota -eq 0
 $limit=$null;$remaining=$null
 if($quotaKnown -and $null -ne $licenses[0].CatalogMonthlyDteLimit -and $licenses[0].CatalogMonthlyDteLimit -gt 0){$limit=$licenses[0].CatalogMonthlyDteLimit;$remaining=[Math]::Max(0,[long]$limit-[long]$used)}
 $report.Quota=[ordered]@{Source='Read-only calculation matching current source semantics; not an executed runtime guard';ExpectedMonthlyLimit=100;ActiveLicenses=$licenses;ActiveLicenseCount=$licenses.Count;PaidApplicationCount=$adoptedCount;LegacyQuotaCalculationAvailable=$quotaKnown;Limit=$limit;Unlimited=($quotaKnown -and $null -eq $limit);MatchesExpected100=($quotaKnown -and $limit -eq 100);Used=$used;Remaining=$remaining;UtcMonthStart=$monthStart.ToString('O');CountsEveryTypeStateAndEnvironment=$true;CountsDraftsAndErrors=$true;Filter='CreatedAt >= current UTC month start, no upper bound';CurrentEntitlementReaderTablesPresent=($schema.PaymentApplicationLedgerTablePresent -eq 1 -and $schema.CheckoutIntentTablePresent -eq 1);DeployedGuardVerified=$false}
 $report.Limitations=@('SQL local acceptance and scenario associations are not live Hacienda certification counters.','Non-placeholder description is only a candidate; official scenario semantics are not validated.','Uncertain candidates require classification from durable evidence and controlled read-only reconciliation; ERROR alone does not prove an uncertain MH result.','Stored geography format does not establish catalog mapping or fiscal validity; zero branches/points does not by itself prevent the configured establishment fallback.','Paid snapshot entitlement validation is intentionally not reproduced; any adoption prevents assuming the live catalog quota.','The screenshot arithmetic is not a mapping to 279 official scenarios. No transmissions, signatures, draft creation, correlation reservation, SMTP or credentials were tested.')
 $report.Passed=$true
}catch{
 $report.Passed=$false;$report.FailureType=$_.Exception.GetType().Name
 # Driver/configuration exception bodies can contain secrets. Do not expose them.
}finally{
 if($connection){$connection.Dispose()};$configuration=$null;$builder=$null
 [void][IO.Directory]::CreateDirectory($outputDirectory)
 $json=$report|ConvertTo-Json -Depth 9
 [IO.File]::WriteAllText($outputFile,$json,[Text.UTF8Encoding]::new($false))
 $json|Write-Output
}
if(-not $report.Passed){exit 1}
