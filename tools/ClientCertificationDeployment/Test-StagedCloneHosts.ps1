$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release=Join-Path $repo 'out/client-certification-release/20260905T222711Z-7b556dd2c9b74a8680c6dbeb4edc03d6'
$manifest=Get-Content -LiteralPath (Join-Path $repo 'tmp/client-certification-release-2026-09-05/clone-manifest.json') -Raw|ConvertFrom-Json
if($manifest.TargetDatabase-cne('NeoProductionAudit_'+$manifest.RunId)-or$manifest.RunId-notmatch'^[a-f0-9]{32}$'-or$manifest.SourceDatabase-cne'NeoSTP_Cloud'){throw 'CLONE_IDENTITY_REJECTED'}
$report=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('O');Mode='STAGED_HOSTS_CLONE';Passed=$false;ActiveDatabaseUsed=$false;FiscalTransmissions=0;HostResults=@()}
$started=@()
try{
 foreach($app in @(@{Name='api';Assembly='NeoSTP.Api';Port=5068;Health='/health/ready'},@{Name='web';Assembly='NeoSTP.Web';Port=5041;Health='/health/ready'})){
  if(Get-NetTCPConnection -State Listen -LocalPort $app.Port -ErrorAction SilentlyContinue){throw 'CLONE_PROBE_PORT_OCCUPIED'}
  $root=Join-Path $release $app.Name
  $local=Get-Content -LiteralPath (Join-Path $root 'appsettings.Local.json') -Raw|ConvertFrom-Json
  $connection=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($local.ConnectionStrings.NeoStpDb)
  if($connection.InitialCatalog-cne'NeoSTP_Cloud'){throw 'STAGED_CONNECTION_REJECTED'}
  $connection['Initial Catalog']=$manifest.TargetDatabase
  $argsText='--environment Development --urls http://127.0.0.1:'+$app.Port+' --Ops:Database:ApplyMigrationsOnStartup false --Ops:Database:SeedOnStartup false --SuperAdmin:BootstrapEnabled false --EmpresaPrueba:Enabled false --DemoComercial:Enabled false'
  $process=Start-Process -FilePath (Join-Path $root ($app.Assembly+'.exe')) -ArgumentList $argsText -WorkingDirectory $root -WindowStyle Hidden -PassThru -Environment @{ConnectionStrings__NeoStpDb=$connection.ConnectionString} -RedirectStandardOutput (Join-Path $root 'clone-probe.stdout.log') -RedirectStandardError (Join-Path $root 'clone-probe.stderr.log')
  $started+=@{Process=$process;Executable=(Join-Path $root ($app.Assembly+'.exe'))}
  $healthy=$false;$status=$null;$deadline=[DateTime]::UtcNow.AddSeconds(35)
  while([DateTime]::UtcNow-lt$deadline-and-not$process.HasExited){
   try{$response=Invoke-WebRequest -Uri ('http://127.0.0.1:'+$app.Port+$app.Health) -TimeoutSec 3 -SkipHttpErrorCheck;$status=[int]$response.StatusCode;if($status-eq200){$healthy=$true;break}}catch{}
   Start-Sleep -Milliseconds 500
  }
  $report.HostResults+=@{Name=$app.Name;Port=$app.Port;HealthStatus=$status;Healthy=$healthy;Exited=$process.HasExited}
  if(-not$healthy){throw 'STAGED_CLONE_HOST_NOT_HEALTHY'}
 }
 $report.Passed=$true
}catch{$report.FailureType=$_.Exception.GetType().Name;throw [InvalidOperationException]::new('Staged clone host verification failed; inspect private host logs.')}
finally{
 foreach($item in $started){$p=$item.Process;if(-not$p.HasExited){Stop-Process -Id $p.Id -ErrorAction Stop;$p.WaitForExit(10000)|Out-Null};$p.Dispose()}
 $report.ProbeHostsStopped=$true
 $report|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $repo 'tmp/client-certification-release-2026-09-05/staged-clone-hosts.json') -Encoding utf8
 $report|ConvertTo-Json -Depth 5
 $local=$null;$connection=$null
}
