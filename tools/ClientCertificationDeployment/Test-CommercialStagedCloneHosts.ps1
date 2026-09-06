#requires -Version 7.0
[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidatePattern('^\d{8}T\d{6}Z-[a-f0-9]{32}$')][string]$CandidateId,
 [Parameter(Mandatory)][ValidatePattern('^[A-F0-9]{64}$')][string]$CandidateSha256,
 [Parameter(Mandatory)][string]$RehearsalManifestPath,
 [Parameter(Mandatory)][ValidatePattern('^[A-F0-9]{64}$')][string]$RehearsalManifestSha256
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root=Join-Path $repo ('out/client-certification-release/'+$CandidateId)
$started=@()
$report=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('o');Passed=$false;CandidateId=$CandidateId;CandidateSha256=$CandidateSha256;SourceDatabaseUsed=$false;FiscalTransmissions=0;CalendarDisabled=$true;HostResults=@();ProbeHostsStopped=$false}
function Require([bool]$ok,[string]$code){if(-not$ok){throw $code}}
try{
 $proofPath=[IO.Path]::GetFullPath((Join-Path $repo $RehearsalManifestPath))
 Require ($proofPath.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-and(Get-FileHash -LiteralPath $proofPath -Algorithm SHA256).Hash-ceq$RehearsalManifestSha256) 'REHEARSAL_HASH_REJECTED'
 $proof=Get-Content -LiteralPath $proofPath -Raw|ConvertFrom-Json
 Require ($proof.Passed-and$proof.Mode-ceq'Rehearse'-and$proof.TargetDatabase-cmatch'^NeoClientBilling92_[a-f0-9]{32}$'-and$proof.ExpectedMigrationCount-eq92-and$proof.OriginalRowsPreserved-and$proof.DbccErrors-eq0) 'REHEARSAL_REQUIRED'
 $report.TargetDatabase=$proof.TargetDatabase;$report.RehearsalManifestSha256=$RehearsalManifestSha256
 $candidate=Join-Path $repo ('tmp/production-candidate/'+$CandidateId+'/candidate-manifest.json')
 Require ((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash-ceq$CandidateSha256) 'CANDIDATE_HASH_REJECTED'
 $manifest=Get-Content -LiteralPath $candidate -Raw|ConvertFrom-Json
 Require ($manifest.PublicationComplete-and$manifest.CandidateId-ceq$CandidateId) 'CANDIDATE_INCOMPLETE'
 foreach($entry in @($manifest.Files|Where-Object {$_.Root-in@('api','web')})){
  $base=Join-Path $root $entry.Root;$file=[IO.Path]::GetFullPath((Join-Path $base $entry.Path))
  Require ($file.StartsWith($base+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-and(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash-ceq$entry.Sha256) 'STAGED_HASH_REJECTED'
 }
 foreach($item in @(@{App='api';Assembly='NeoSTP.Api';Port=5068},@{App='web';Assembly='NeoSTP.Web';Port=5041})){
  Require (-not(Get-NetTCPConnection -State Listen -LocalPort $item.Port -ErrorAction SilentlyContinue)) 'PROBE_PORT_OCCUPIED'
  $appRoot=Join-Path $root $item.App
  $local=Get-Content -LiteralPath (Join-Path $appRoot 'appsettings.Local.json') -Raw|ConvertFrom-Json
  $connection=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($local.ConnectionStrings.NeoStpDb)
  Require ($connection.InitialCatalog-ceq'NeoSTP_Cloud'-and$connection.DataSource-in@('.','(local)','localhost','127.0.0.1',[Environment]::MachineName)-and$connection.AttachDBFilename.Length-eq0) 'STAGED_SOURCE_CONNECTION_REJECTED'
  $connection['Initial Catalog']=$proof.TargetDatabase
  $exe=Join-Path $appRoot ($item.Assembly+'.exe')
  $arguments='--environment Development --urls http://127.0.0.1:'+$item.Port+' --Ops:Database:ApplyMigrationsOnStartup false --Ops:Database:SeedOnStartup false --SuperAdmin:BootstrapEnabled false --EmpresaPrueba:Enabled false --DemoComercial:Enabled false --Billing:Calendar:Enabled false'
  $process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $appRoot -WindowStyle Hidden -PassThru -Environment @{ConnectionStrings__NeoStpDb=$connection.ConnectionString} -RedirectStandardOutput (Join-Path $appRoot 'schema92-probe.stdout.log') -RedirectStandardError (Join-Path $appRoot 'schema92-probe.stderr.log')
  $started+=@{Process=$process;Executable=$exe}
  $healthy=$false;$deadline=[DateTime]::UtcNow.AddSeconds(40)
  while([DateTime]::UtcNow-lt$deadline-and-not$process.HasExited){try{$response=Invoke-WebRequest -Uri ('http://127.0.0.1:'+$item.Port+'/health/ready') -TimeoutSec 3 -SkipHttpErrorCheck;if($response.StatusCode-eq200){$listener=Get-NetTCPConnection -State Listen -LocalPort $item.Port|Select-Object -First 1;Require ($listener.OwningProcess-eq$process.Id) 'PROBE_LISTENER_CHANGED';$healthy=$true;break}}catch{};Start-Sleep -Milliseconds 500}
  $report.HostResults+=@{App=$item.App;Port=$item.Port;Healthy=$healthy;ExitCode=if($process.HasExited){$process.ExitCode}else{$null}}
  Require $healthy 'SCHEMA92_PROBE_NOT_HEALTHY'
 }
 $report.Passed=$true
}catch{$report.FailureCode=if($_.Exception.Message-cmatch'\A[A-Z0-9_]+\z'){$_.Exception.Message}else{'CLONE_HOST_PROBE_REVIEW_REQUIRED'}}
finally{
 foreach($item in $started){$process=$item.Process;if(-not$process.HasExited){$actual=Get-CimInstance Win32_Process -Filter ('ProcessId='+$process.Id);if($actual.ExecutablePath-ceq$item.Executable){Stop-Process -Id $process.Id -ErrorAction Stop;[void]$process.WaitForExit(10000)}else{$report.Passed=$false;$report.StopFailure=$true}};$process.Dispose()}
 $report.ProbeHostsStopped=-not(Get-NetTCPConnection -State Listen -LocalPort 5068,5041 -ErrorAction SilentlyContinue)
 if(-not$report.ProbeHostsStopped){$report.Passed=$false}
 $local=$null;$connection=$null
 if(Test-Path -LiteralPath $root){$report|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root 'schema92-clone-hosts.json') -Encoding utf8}
 $report|ConvertTo-Json -Depth 5
}
if(-not$report.Passed){exit 1}
