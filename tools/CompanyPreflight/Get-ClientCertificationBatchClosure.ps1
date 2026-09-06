#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskConnectionString=$null
foreach($taskName in @('appsettings.json','appsettings.Development.json','appsettings.Local.json')){
 $taskPath=Join-Path $taskRepo ('out/local-autostart/api/'+$taskName)
 if(Test-Path -LiteralPath $taskPath){$taskConfig=Get-Content -LiteralPath $taskPath -Raw|ConvertFrom-Json;if($taskConfig.ConnectionStrings.NeoStpDb){$taskConnectionString=$taskConfig.ConnectionStrings.NeoStpDb}}
}
$taskBuilder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($taskConnectionString)
if($taskBuilder.InitialCatalog -cne 'NeoSTP_Cloud' -or $taskBuilder.AttachDBFilename.Length-ne0 -or $taskBuilder.DataSource -notin @('.','(local)','localhost','127.0.0.1',[Environment]::MachineName)){throw 'SQL_TARGET_REJECTED'}
$taskBuilder['ApplicationIntent']='ReadOnly'
$taskConnection=[System.Data.SqlClient.SqlConnection]::new($taskBuilder.ConnectionString)
function Read-Fixed([string]$sql){
 $command=$taskConnection.CreateCommand();$command.CommandText=$sql
 try{$reader=$command.ExecuteReader();try{while($reader.Read()){$row=[ordered]@{};for($i=0;$i-lt$reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};[pscustomobject]$row}}finally{$reader.Dispose()}}finally{$command.Dispose()}
}

$taskReport=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('o');Passed=$false;EmpresaId=23;Ambiente='PRUEBAS';SqlWritesIssued=$false;ExternalRequestsIssued=$false;SecretsSelected=$false;FiscalPayloadsExported=$false;PortalVerifiedLive=$false;OfficialCounterUpdateClaimed=$false}
$taskChecks=[Collections.Generic.List[object]]::new()
function Check([bool]$condition,[string]$code){$taskChecks.Add([pscustomobject]@{Code=$code;Passed=$condition})}
function Sha([string]$value){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($value)))}
try{
 $taskConnection.Open()
 $null=Read-Fixed @'
IF DB_NAME() <> N'NeoSTP_Cloud' OR CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) <> 16 OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')) <> HOST_NAME() OR SERVERPROPERTY('InstanceName') IS NOT NULL THROW 50001,'SQL_IDENTITY_REJECTED',1;
IF NOT EXISTS(SELECT 1 FROM Core_Empresas e JOIN Dte_Configuracion c ON c.EmpresaId=e.Id WHERE e.Id=23 AND e.EstadoCodigo='ACTIVA' AND REPLACE(REPLACE(e.Nit,'-',''),' ','')='06232705261148' AND c.AmbienteCodigo='PRUEBAS' AND c.TiposDteAutorizadosCsv='01,03,11,14') THROW 50001,'TENANT_AUTHORITY_REJECTED',1;
'@
 $migrations=@(Read-Fixed 'SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;').MigrationId
 $expected=@(Get-ChildItem -LiteralPath (Join-Path $taskRepo 'src/NeoSTP.Infrastructure/Persistence/Migrations') -File -Filter '*.cs'|Where-Object {$_.Name-match'^\d{14}_.+(?<!\.Designer)\.cs$'}|Sort-Object Name|ForEach-Object {$_.BaseName})
 if($expected.Count-lt91-or'20260905221056_CERT2_TenantDteTypeAuthorization'-cnotin$expected-or($expected-join'|')-cne($migrations-join'|')){throw 'EXACT_SOURCE_MIGRATIONS_REQUIRED'}
 $taskReport.ExactSourceMigrationsVerified=$true
 $taskReport.AppliedMigrationCount=$migrations.Count
 $taskReport.LastAppliedMigration=$migrations[-1]
 $baselinePath=Join-Path $taskRepo 'tmp/client-certification-release-2026-09-05/batch-closure-baseline.json'
 $baselineHash=(Get-FileHash -LiteralPath $baselinePath -Algorithm SHA256).Hash
 if($baselineHash-cne'7FF6F3FA1922B9CBAE33EDD73E3A2042299800BBDE9CABF5385F04AEF8182D1B'){throw 'BASELINE_CHANGED'}
 $baseline=Get-Content -LiteralPath $baselinePath -Raw|ConvertFrom-Json
 $original=@(Read-Fixed @'
SELECT d.Id,d.EstadoCodigo,d.TipoDteCodigo,
CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT d.* FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)),2) DocumentRowSha256,
CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT j.* FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)),2) JsonRowSha256,
CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT c.* FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)),2) ClaimRowSha256
FROM Dte_Documentos d LEFT JOIN Dte_DocumentoJson j ON j.DocumentoId=d.Id LEFT JOIN Dte_CertificationCampaignConsumptions c ON c.DteDocumentoId=d.Id
WHERE d.EmpresaId=23 AND d.Id BETWEEN 1005 AND 1019 ORDER BY d.Id;
'@)
 Check ($original.Count-eq15-and(ConvertTo-Json -InputObject $original -Compress)-ceq(ConvertTo-Json -InputObject $baseline.Rows -Compress)) 'HISTORICAL_AND_FOUR_PILOT_FULL_ROWS_UNCHANGED'
 $history=@($original|Where-Object Id -le 1015)
 Check (@($history|Where-Object EstadoCodigo -eq 'ERROR').Count-eq8-and@($history|Where-Object EstadoCodigo -eq 'BORRADOR').Count-eq2-and@($history|Where-Object {$_.Id-eq1015-and$_.EstadoCodigo-eq'PROCESADO'}).Count-eq1) 'EIGHT_OLD_ERRORS_TWO_DRAFTS_AND_CF1015_PRESERVED'
 $campaigns=@(Read-Fixed @'
SELECT Id,PublicId,EmpresaId,ExpectedNit,AmbienteCodigo,Status,StartsAtUtc,ExpiresAtUtc,TotalBudget,MatrixReference
FROM Dte_CertificationCampaigns WHERE EmpresaId=23 AND PublicId IN('5f681bfb-149d-4650-9a1b-5723feeebef3','7bc8a3fb-08d6-4c8d-974a-01cde7fd702a');
'@)
 $pilot=@($campaigns|Where-Object TotalBudget -eq 4);$batch=@($campaigns|Where-Object TotalBudget -eq 275)
 if($campaigns.Count-ne2-or$pilot.Count-ne1-or$batch.Count-ne1){throw 'BOTH_EXACT_CAMPAIGNS_REQUIRED'}
 $pilot=$pilot[0];$batch=$batch[0]
 $budgets=@(Read-Fixed @'
SELECT b.CampaignId,b.TipoDteCodigo,b.Budget FROM Dte_CertificationCampaignTypeBudgets b JOIN Dte_CertificationCampaigns c ON c.Id=b.CampaignId
WHERE c.EmpresaId=23 AND c.PublicId IN('5f681bfb-149d-4650-9a1b-5723feeebef3','7bc8a3fb-08d6-4c8d-974a-01cde7fd702a');
'@)
 $expectedBudgets=[ordered]@{'01'=88;'03'=74;'11'=89;'14'=24}
 foreach($c in $campaigns){$own=@($budgets|Where-Object CampaignId -eq $c.Id);Check ($c.ExpectedNit-ceq'06232705261148'-and$c.AmbienteCodigo-ceq'PRUEBAS'-and$c.Status-in@('ACTIVE','CLOSED','REVOKED')-and$c.StartsAtUtc.Offset-eq[TimeSpan]::Zero-and$c.ExpiresAtUtc.Offset-eq[TimeSpan]::Zero-and$c.ExpiresAtUtc-gt$c.StartsAtUtc-and$own.Count-eq4-and($own.Budget|Measure-Object -Sum).Sum-eq$c.TotalBudget) ('CAMPAIGN_COHERENT_'+$c.TotalBudget)}
 Check ($batch.PublicId-eq[Guid]'7bc8a3fb-08d6-4c8d-974a-01cde7fd702a'-and$batch.MatrixReference-ceq'CLIENT23-BATCH275-V1'-and$batch.ExpiresAtUtc-eq$batch.StartsAtUtc.AddHours(48)-and$pilot.PublicId-eq[Guid]'5f681bfb-149d-4650-9a1b-5723feeebef3'-and$pilot.MatrixReference-ceq'CLIENT23-PILOT-4-V1') 'CAMPAIGN_TERMS_FIXED'
 foreach($type in $expectedBudgets.Keys){Check (@($budgets|Where-Object {$_.CampaignId-eq$batch.Id-and$_.TipoDteCodigo-ceq$type-and$_.Budget-eq$expectedBudgets[$type]}).Count-eq1-and@($budgets|Where-Object {$_.CampaignId-eq$pilot.Id-and$_.TipoDteCodigo-ceq$type-and$_.Budget-eq1}).Count-eq1) ('TYPE_BUDGET_'+$type)}
 $audits=@(Read-Fixed @'
SELECT Id,Entidad,Accion,Resultado,Detalle,DatosAntes,DatosDespues FROM Core_Auditoria WHERE EmpresaId=23 AND EntidadId='7bc8a3fb-08d6-4c8d-974a-01cde7fd702a' AND Accion IN('CERT_BATCH275_PLAN','CERT_BATCH275_ATTEMPT','CERT_BATCH275_STOP');
'@)
 $plans=@($audits|Where-Object Accion -eq 'CERT_BATCH275_PLAN');if($plans.Count-ne1){throw 'UNIQUE_BATCH_PLAN_REQUIRED'};$plan=$plans[0];$planHash=$plan.DatosAntes;$planEvidence=$plan.DatosDespues|ConvertFrom-Json
 Check ($plan.Resultado-ceq'PREPARED'-and$planHash-ceq'FD21C5AC3D9BC27A03693299DE54D083D25CCFCA68C646D069C77680D14C160E'-and(Sha $plan.DatosDespues)-ceq$planHash-and@($audits|Where-Object Entidad -cne 'CERTIFICATION_BATCH').Count-eq0) 'PLAN_HASH_MATCHES_DURABLE_EVIDENCE'
 Check (@($planEvidence.Pilots).Count-eq4-and(@($planEvidence.Pilots|Sort-Object Id).Id-join',')-ceq'1016,1017,1018,1019') 'EXACT_FOUR_ACCEPTED_SOURCE_PILOTS'
 $attempts=@($audits|Where-Object Accion -eq 'CERT_BATCH275_ATTEMPT');$stops=@($audits|Where-Object Accion -eq 'CERT_BATCH275_STOP')
 Check ($stops.Count-eq0-and@($attempts|Where-Object Resultado -ne 'ACCEPTED').Count-eq0) 'NO_STOP_OR_IN_PROGRESS_OR_REJECTED_MARKERS'
 $claims=@(Read-Fixed @'
SELECT c.Id,c.CampaignId,c.DteDocumentoId,c.EmpresaId,c.PublicId,c.TipoDteCodigo,c.IdempotencyKeyHash,c.RequestHash,c.ScenarioReference,c.CreatedAt,
d.EstadoCodigo,d.VersionDte,d.CodigoGeneracion,d.NumeroControl,d.EnviadoAt,
CONVERT(bit,CASE WHEN c.EmpresaId=d.EmpresaId AND c.EmpresaId=p.EmpresaId AND c.TipoDteCodigo=d.TipoDteCodigo AND d.AmbienteCodigo='PRUEBAS' AND d.IdempotencyScope='CERT' AND c.IdempotencyKeyHash=d.IdempotencyKeyHash AND c.RequestHash=d.IdempotencyRequestHash AND c.CreatedAt=d.CreatedAt AND c.CreatedAt>=CAST(p.StartsAtUtc AS datetime2) AND c.CreatedAt<CAST(p.ExpiresAtUtc AS datetime2) THEN 1 ELSE 0 END) DocumentCoherent,
CONVERT(bit,CASE WHEN NULLIF(d.SelloRecibido,'') IS NOT NULL THEN 1 ELSE 0 END) HasSeal,
CONVERT(bit,CASE WHEN JSON_VALUE(j.RespuestaHacienda,'$.estado')='PROCESADO' AND JSON_VALUE(j.RespuestaHacienda,'$.selloRecibido')=d.SelloRecibido THEN 1 ELSE 0 END) ReceiptMatches,
CONVERT(bit,CASE WHEN TRY_CONVERT(int,JSON_VALUE(j.JsonDte,'$.identificacion.version'))=d.VersionDte AND JSON_VALUE(j.JsonDte,'$.identificacion.tipoDte')=d.TipoDteCodigo AND JSON_VALUE(j.JsonDte,'$.identificacion.ambiente')='00' AND JSON_VALUE(j.JsonDte,'$.identificacion.codigoGeneracion')=d.CodigoGeneracion AND JSON_VALUE(j.JsonDte,'$.identificacion.numeroControl')=d.NumeroControl AND JSON_VALUE(j.JsonDte,'$.emisor.nit')='06232705261148' THEN 1 ELSE 0 END) JsonIdentityMatches,
CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varchar(max),j.JsonDte COLLATE Latin1_General_100_BIN2_UTF8)),2) JsonHashUtf8,
CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varchar(max),j.RespuestaHacienda COLLATE Latin1_General_100_BIN2_UTF8)),2) ResponseHashUtf8
FROM Dte_CertificationCampaignConsumptions c JOIN Dte_Documentos d ON d.Id=c.DteDocumentoId JOIN Dte_CertificationCampaigns p ON p.Id=c.CampaignId LEFT JOIN Dte_DocumentoJson j ON j.DocumentoId=d.Id WHERE c.EmpresaId=23 AND d.CreatedAt>='2026-09-01' AND d.CreatedAt<'2026-10-01' ORDER BY c.CampaignId,c.TipoDteCodigo,c.Id;
'@)
 $pilotClaims=@($claims|Where-Object CampaignId -eq $pilot.Id);$batchClaims=@($claims|Where-Object CampaignId -eq $batch.Id)
 Check ($pilotClaims.Count-eq4-and$batchClaims.Count-eq275-and$claims.Count-eq279-and$attempts.Count-eq275) 'EXACT_279_CLAIMS_AND_275_BATCH_ACCEPTANCE_MARKERS'
 $versions=@{'01'=2;'03'=4;'11'=3;'14'=2};$coherent=@();$caseIssues=[Collections.Generic.List[string]]::new()
 foreach($claim in $claims){$valid=$claim.DocumentCoherent-and$claim.PublicId-ne[Guid]::Empty-and$claim.IdempotencyKeyHash-cmatch'\A[0-9A-F]{64}\z'-and$claim.RequestHash-cmatch'\A[0-9A-F]{64}\z'-and-not[string]::IsNullOrWhiteSpace($claim.ScenarioReference)-and$claim.ScenarioReference.Length-le128-and-not($claim.ScenarioReference.ToCharArray()|Where-Object {[char]::IsControl($_)})-and@($claims|Where-Object DteDocumentoId -eq $claim.DteDocumentoId).Count-eq1-and$claim.CampaignId-in@($pilot.Id,$batch.Id);if($valid){$coherent+=$claim};if(-not($claim.EstadoCodigo-ceq'PROCESADO'-and$claim.HasSeal-and$claim.ReceiptMatches-and$claim.JsonIdentityMatches-and$claim.VersionDte-eq$versions[$claim.TipoDteCodigo]-and$null-ne$claim.EnviadoAt)){$caseIssues.Add('DOCUMENT_NOT_ACCEPTED_'+$claim.DteDocumentoId)}}
 Check ($coherent.Count-eq$claims.Count) 'ALL_MONTHLY_CLAIMS_MATCH_FISCAL_DOCUMENT_AND_HASHES'
 foreach($type in $expectedBudgets.Keys){$own=@($batchClaims|Where-Object TipoDteCodigo -eq $type);Check ($own.Count-le$expectedBudgets[$type]-and@($pilotClaims|Where-Object TipoDteCodigo -eq $type).Count-eq1) ('NO_OVERBUDGET_'+$type)
  for($ordinal=1;$ordinal-le$expectedBudgets[$type];$ordinal++){
   $key='batch275-v1-{0}-{1:D3}'-f$type,$ordinal;$reference='CLIENT23-BATCH275-V1:{0}:{1:D3}'-f$type,$ordinal
   $quantity=1+($ordinal-1)%5;$price=1+[Math]::Floor(($ordinal-1)/5)%10;$priceText=$price.ToString('F2',[Globalization.CultureInfo]::InvariantCulture)
   $keyHash=Sha ('7bc8a3fb08d64c8d974a01cde7fd702a:'+$key);$payloadHash=Sha ($planHash+'|'+$key+'|'+$quantity+'|'+$priceText);$requestHash=Sha ($payloadHash+'|'+$type+'|'+$reference)
   $matching=@($batchClaims|Where-Object ScenarioReference -ceq $reference);$marks=@($attempts|Where-Object Detalle -ceq $key)
   if($matching.Count-ne1-or$marks.Count-ne1){$caseIssues.Add('MISSING_OR_DUPLICATE_'+$key);continue};$claim=$matching[0];$mark=$marks[0];$receipt=$mark.DatosDespues|ConvertFrom-Json
   if($claim.IdempotencyKeyHash-cne$keyHash-or$claim.RequestHash-cne$requestHash-or$mark.DatosAntes-cne$planHash-or$mark.Resultado-cne'ACCEPTED'-or$receipt.DocumentId-ne$claim.DteDocumentoId-or$receipt.ConsumptionId-ne$claim.Id-or$receipt.AttemptId-ne$mark.Id-or$receipt.CaseKey-cne$key-or$receipt.PlanHash-cne$planHash){$caseIssues.Add('CASE_HASH_OR_RECEIPT_MISMATCH_'+$key)}
  }
 }
 Check ($caseIssues.Count-eq0) 'ALL_275_CASE_HASHES_MARKERS_VERSIONS_AND_RECEIPTS_MATCH'
 foreach($source in $planEvidence.Pilots){$claim=@($pilotClaims|Where-Object DteDocumentoId -eq $source.Id);Check ($claim.Count-eq1-and$claim[0].TipoDteCodigo-ceq$source.Type-and$claim[0].JsonHashUtf8-ceq$source.JsonHash-and$claim[0].ResponseHashUtf8-ceq$source.ResponseHash) ('SOURCE_PILOT_HASHES_PRESERVED_'+$source.Id)}
 $totals=@(Read-Fixed @'
SELECT TipoDteCodigo,COUNT(*) ProcessedWithSeal FROM Dte_Documentos WHERE EmpresaId=23 AND AmbienteCodigo='PRUEBAS' AND EstadoCodigo='PROCESADO' AND NULLIF(SelloRecibido,'') IS NOT NULL GROUP BY TipoDteCodigo ORDER BY TipoDteCodigo;
'@)
 # Only the fixed campaign cohort plus historical Factura 1015 fulfills these
 # targets. Subsequent documents cannot compensate for a missing campaign case.
 $certificationTotals=@(Read-Fixed @'
SELECT d.TipoDteCodigo,COUNT(*) ProcessedWithSeal FROM Dte_Documentos d
WHERE d.EmpresaId=23 AND d.AmbienteCodigo='PRUEBAS' AND d.EstadoCodigo='PROCESADO' AND NULLIF(d.SelloRecibido,'') IS NOT NULL
AND (d.Id=1015 OR EXISTS(SELECT 1 FROM Dte_CertificationCampaignConsumptions cc JOIN Dte_CertificationCampaigns cp ON cp.Id=cc.CampaignId
 WHERE cc.DteDocumentoId=d.Id AND cc.EmpresaId=d.EmpresaId AND cp.EmpresaId=d.EmpresaId
 AND cp.PublicId IN('5f681bfb-149d-4650-9a1b-5723feeebef3','7bc8a3fb-08d6-4c8d-974a-01cde7fd702a')))
GROUP BY d.TipoDteCodigo ORDER BY d.TipoDteCodigo;
'@)
 $expectedTotals=@{'01'=90;'03'=75;'11'=90;'14'=25};foreach($type in $expectedTotals.Keys){Check (@($certificationTotals|Where-Object {$_.TipoDteCodigo-ceq$type-and$_.ProcessedWithSeal-eq$expectedTotals[$type]}).Count-eq1) ('CERTIFICATION_COHORT_ACCEPTED_TOTAL_'+$type)}
 Check ($certificationTotals.Count-eq4) 'CERTIFICATION_COHORT_EXACT_FOUR_TYPES'
 $month=@(Read-Fixed "SELECT COUNT(*) Total FROM Dte_Documentos WHERE EmpresaId=23 AND CreatedAt>='2026-09-01' AND CreatedAt<'2026-10-01';")[0].Total
 $outsideCampaign=@(Read-Fixed @'
WITH classified AS (
 SELECT d.TipoDteCodigo,d.EstadoCodigo,
 CASE WHEN EXISTS(SELECT 1 FROM Dte_EventoDocumentosRelacionados rel JOIN Dte_Eventos ev ON ev.Id=rel.EventoId
  WHERE rel.DocumentoId=d.Id AND ev.EmpresaId=d.EmpresaId AND ev.AmbienteCodigo=d.AmbienteCodigo) THEN 'EVENT_LINKED' ELSE 'NO_EVENT_LINK' END EvidenceCategory
 FROM Dte_Documentos d WHERE d.EmpresaId=23 AND d.CreatedAt>='2026-09-01' AND d.CreatedAt<'2026-10-01'
 AND NOT EXISTS(SELECT 1 FROM Dte_CertificationCampaignConsumptions cc WHERE cc.DteDocumentoId=d.Id)
)
SELECT TipoDteCodigo,EstadoCodigo,EvidenceCategory,COUNT(*) DocumentCount FROM classified
GROUP BY TipoDteCodigo,EstadoCodigo,EvidenceCategory ORDER BY TipoDteCodigo,EstadoCodigo,EvidenceCategory;
'@)
 $commercial=$month-$coherent.Count
 $outsideCount=($outsideCampaign|Measure-Object DocumentCount -Sum).Sum
 Check ($commercial-ge0-and$commercial-eq$outsideCount-and$month-eq($claims.Count+$outsideCount)) 'SEPTEMBER_CAMPAIGN_AND_NON_CAMPAIGN_COUNTS_RECONCILE'
 $neo=@(Read-Fixed @'
SELECT e.Id EmpresaId,e.Nit,e.Nrc,e.RazonSocial,e.NombreComercial,e.CodigoActividad,e.ActividadEconomica,e.Departamento,e.Municipio,e.Distrito,e.Direccion,e.Telefono,e.Correo,e.EstadoCodigo,c.AmbienteCodigo FROM Core_Empresas e JOIN Dte_Configuracion c ON c.EmpresaId=e.Id WHERE e.Id=2;
'@)[0]
 $neoHash=Sha (ConvertTo-Json -InputObject $neo -Compress);Check ($neoHash-ceq'317182F1ABD7FE71963FF846D03E7F529E130A163059CFCAEC07687D729AE677'-and$planEvidence.NeoSnapshotHash-ceq$neoHash) 'NEO_PUBLIC_SNAPSHOT_UNCHANGED'
 $taskReport.Campaigns=@($campaigns|Select-Object PublicId,TotalBudget,Status,MatrixReference)
 $taskReport.BatchConsumptions=$batchClaims.Count;$taskReport.PilotConsumptions=$pilotClaims.Count;$taskReport.AcceptedMarkers=@($attempts|Where-Object Resultado -eq 'ACCEPTED').Count;$taskReport.StopMarkers=$stops.Count
 $taskReport.CertificationCohortProcessedWithSeal=$certificationTotals
 $taskReport.LocalProcessedWithSeal=$totals;$taskReport.MonthlyCreated=$month;$taskReport.CommercialSeptember=$commercial
 $taskReport.NonCampaignSeptember=$outsideCampaign
 $taskReport.NonCampaignDocumentsRemainCommercial=$true
 $taskReport.NonCampaignScopeNote='Event-linked requires a same-tenant event relationship. Neither category is excluded from commercial usage. Passed evaluates fixed campaign closure and reconciliation, not production readiness or completion of subsequent documents.'
 $taskReport.CommercialCountingMethod='Same coherent claim exclusions as CertificationCampaignAccess; validated known campaigns, exact type budgets, no overflow, document scope/hash/date links, unique claims and scenario format before subtraction. No expiry-based refund.'
 $taskReport.PlanHash=$planHash;$taskReport.BaselineSha256=$baselineHash;$taskReport.NeoSnapshotHash=$neoHash
 $taskReport.CaseIssueCount=$caseIssues.Count;$taskReport.FirstCaseIssues=@($caseIssues|Select-Object -First 12)
 $taskReport.CheckedCases=275;$taskReport.Checks=$taskChecks;$taskReport.Passed=@($taskChecks|Where-Object {-not$_.Passed}).Count-eq0
}catch{$taskReport.ErrorType=$_.Exception.GetType().Name;$taskReport.Code=if($_.Exception.Message-cmatch'\A[A-Z0-9_]+\z'){$_.Exception.Message}else{'BATCH_CLOSURE_READ_FAILED'};$taskReport.Checks=$taskChecks;$taskReport.Passed=$false}
finally{$taskConnection.Dispose();$taskOutput=Join-Path $taskRepo 'tmp/client-certification-release-2026-09-05/sanitized-batch-closure.json';$taskReport|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $taskOutput -Encoding utf8;$taskReport|ConvertTo-Json -Depth 7}
if(-not$taskReport.Passed){exit 1}
