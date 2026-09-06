param([int]$EmpresaId=23,[string]$ExpectedNit='06232705261148')
$ErrorActionPreference='Stop'
if($EmpresaId -ne 23 -or $ExpectedNit -cne '06232705261148'){throw 'CLI23_ARGUMENT_IDENTITY_REJECTED'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$connection=$null
$report=[ordered]@{ObservedAtUtc=[DateTime]::UtcNow.ToString('O');EmpresaId=23;Mode='PREVIEW_ONLY';Passed=$false;SqlWritesIssued=$false;ExternalRequestsIssued=$false;SecretsSelected=$false;ProductOrRuntimeChanged=$false}
function Rows([string]$sql){
 $command=$connection.CreateCommand();$command.CommandTimeout=15;$command.CommandText=$sql
 [void]$command.Parameters.AddWithValue('@tenant',23);[void]$command.Parameters.AddWithValue('@nit',$ExpectedNit)
 try{$r=$command.ExecuteReader();try{while($r.Read()){$row=[ordered]@{};for($i=0;$i -lt $r.FieldCount;$i++){$row[$r.GetName($i)]=$(if($r.IsDBNull($i)){$null}else{$r.GetValue($i)})};[pscustomobject]$row}}finally{$r.Dispose()}}finally{$command.Dispose()}
}
try{
 # Follow the inspected startup wrapper without executing it. Configuration is used only in memory.
 $wrapper=[IO.File]::ReadAllText((Join-Path $repo 'tools/ClientCertificationDeployment/Start-StagedHost.ps1'))
 $match=[regex]::Matches($wrapper,"out/client-certification-release/(?<id>[0-9]{8}T[0-9]{6}Z-[a-f0-9]{32})'")
 if($match.Count -ne 1){throw 'CLI23_RUNTIME_SELECTOR_REJECTED'}
 $release=$match[0].Groups['id'].Value;$report.SelectedReleaseId=$release
 $root=Join-Path $repo ('out/client-certification-release/'+$release+'/api')
 $connString=$null
 foreach($name in @('appsettings.json','appsettings.Development.json','appsettings.Local.json')){
  $node=[IO.File]::ReadAllText((Join-Path $root $name))|ConvertFrom-Json
  if($node.ConnectionStrings -and $node.ConnectionStrings.NeoStpDb){$connString=$node.ConnectionStrings.NeoStpDb}
 }
 $node=$null
 $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($connString);$connString=$null
 if($builder.InitialCatalog -cne 'NeoSTP_Cloud' -or $builder.AttachDBFilename.Length -ne 0 -or $builder.DataSource -notin @('.','(local)','localhost','127.0.0.1',[Environment]::MachineName)){throw 'CLI23_DATABASE_TARGET_REJECTED'}
 $builder['ApplicationIntent']='ReadOnly';$builder['Pooling']=$false;$builder['Application Name']='NeoSTP CLI23 ReadOnly Preview'
 $connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$builder=$null;$connection.Open()
 $identity=@(Rows "SELECT DB_NAME() DbName,CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')) MachineName,CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) MajorVersion,SERVERPROPERTY('InstanceName') InstanceName")[0]
 if($identity.DbName -cne 'NeoSTP_Cloud' -or $identity.MachineName -ine [Environment]::MachineName -or $identity.MajorVersion -ne 16 -or $null -ne $identity.InstanceName){throw 'CLI23_DATABASE_IDENTITY_REJECTED'}
 $report.Schema=@(Rows "SELECT COUNT(*) MigrationCount,MAX(MigrationId) LastMigration FROM dbo.__EFMigrationsHistory")[0]
 if($report.Schema.MigrationCount -ne 91 -or $report.Schema.LastMigration -cne '20260905221056_CERT2_TenantDteTypeAuthorization'){throw 'CLI23_SCHEMA_REJECTED'}
 $report.Identity=@(Rows @'
SELECT e.Id,e.EstadoCodigo CompanyState,CASE WHEN REPLACE(REPLACE(e.Nit,'-',''),' ','')=@nit THEN 1 ELSE 0 END NitMatches,
 f.AmbienteCodigo FROM dbo.Core_Empresas e JOIN dbo.Dte_Configuracion f ON f.EmpresaId=e.Id WHERE e.Id=@tenant
'@)
 if($report.Identity.Count -ne 1 -or $report.Identity[0].NitMatches -ne 1 -or $report.Identity[0].AmbienteCodigo -cne 'PRUEBAS'){throw 'CLI23_TENANT_IDENTITY_REJECTED'}
 $report.Licenses=@(Rows @'
SELECT l.Id,l.PlanId,l.EstadoCodigo,l.FechaInicio,l.FechaFin,l.CreatedAt,l.UpdatedAt,p.Codigo PlanCode,p.PrecioMensual,p.MonedaCodigo,
 p.LimiteUsuarios,p.LimiteSucursales,p.LimitePuntosVenta,p.LimiteDteMensual,p.Activo CatalogPlanActive
 FROM dbo.Core_EmpresaPlan l JOIN dbo.Core_Planes p ON p.Id=l.PlanId WHERE l.EmpresaId=@tenant ORDER BY l.Id
'@)
 $report.Subscriptions=@(Rows @'
SELECT s.Id,s.BillingCustomerId,s.PlanId,s.Status,s.TrialStart,s.TrialEnd,s.CurrentPeriodStart,s.CurrentPeriodEnd,s.CanceledAt,s.CancelAtPeriodEnd,
 s.CreatedAt,s.UpdatedAt,c.Provider,CASE WHEN NULLIF(c.ExternalCustomerId,'') IS NULL THEN 0 ELSE 1 END HasExternalCustomer,
 CASE WHEN NULLIF(s.ExternalSubscriptionId,'') IS NULL THEN 0 ELSE 1 END HasExternalSubscription
 FROM dbo.Billing_Subscriptions s JOIN dbo.Billing_Customers c ON c.Id=s.BillingCustomerId WHERE c.EmpresaId=@tenant ORDER BY s.Id
'@)
 $report.Invoices=@(Rows @'
SELECT i.Id,i.BillingSubscriptionId,i.Amount,i.Currency,i.Status,i.InvoiceDate,i.DueDate,i.PaidAt,
 CASE WHEN NULLIF(i.ExternalInvoiceId,'') IS NULL THEN 0 ELSE 1 END HasExternalInvoice
 FROM dbo.Billing_Invoices i JOIN dbo.Billing_Subscriptions s ON s.Id=i.BillingSubscriptionId
 JOIN dbo.Billing_Customers c ON c.Id=s.BillingCustomerId WHERE c.EmpresaId=@tenant ORDER BY i.Id
'@)
 $report.Payments=@(Rows @'
SELECT p.Id,p.BillingSubscriptionId,p.Amount,p.Currency,p.Status,p.Metodo,p.PaidAt,p.VerificadoAt
 FROM dbo.Billing_Payments p JOIN dbo.Billing_Subscriptions s ON s.Id=p.BillingSubscriptionId
 JOIN dbo.Billing_Customers c ON c.Id=s.BillingCustomerId WHERE c.EmpresaId=@tenant ORDER BY p.Id
'@)
 $report.BillingAdoption=@(Rows @'
SELECT (SELECT COUNT(*) FROM dbo.Billing_PaymentApplications a JOIN dbo.Core_EmpresaPlan l ON l.Id=a.EmpresaPlanId WHERE l.EmpresaId=@tenant) PaymentApplications,
 (SELECT COUNT(*) FROM dbo.Billing_CheckoutIntents i WHERE i.EmpresaId=@tenant) CheckoutIntents,
 (SELECT COUNT(*) FROM dbo.Core_Auditoria a WHERE a.EmpresaId=@tenant AND a.Accion LIKE 'CLI23%') ExistingCli23AuditMarkers
'@)[0]
 $report.Agreement=[ordered]@{ImplementationReportedPaid=$true;ImplementationAmountKnown=$false;SeptemberMonthlyAmount=15;Currency='USD';SeptemberMonthlyPaid=$false;SeptemberDueLocalDate='2026-09-30';TimeZone='America/El_Salvador';AutomaticSuspensionAgreed=$false;LateFeesAgreed=$false}
 $report.Passed=$true
}catch{$report.FailureType=$_.Exception.GetType().Name;$report.Passed=$false
 # Never expose driver/configuration exception text or secrets.
}finally{if($connection){$connection.Dispose()}
 $folder=Join-Path $repo 'tmp/client-commercial-2026-09-05';[void][IO.Directory]::CreateDirectory($folder)
 $path=Join-Path $folder ('preview-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')+'-'+[Guid]::NewGuid().ToString('N')+'.json')
 $json=$report|ConvertTo-Json -Depth 10;[IO.File]::WriteAllText($path,$json,[Text.UTF8Encoding]::new($false));$json|Write-Output
 Write-Output ('Evidence: '+$path)
}
if(-not $report.Passed){exit 1}
