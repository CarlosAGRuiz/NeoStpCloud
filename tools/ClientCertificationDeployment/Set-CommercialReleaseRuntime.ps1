#requires -Version 7.0
[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidateSet('StopForSchemaUpgrade','StartAfterSchemaUpgrade')][string]$Mode,
 [Parameter(Mandatory)][ValidatePattern('^\d{8}T\d{6}Z-[a-f0-9]{32}$')][string]$CandidateId,
 [Parameter(Mandatory)][ValidatePattern('^[A-F0-9]{64}$')][string]$CandidateSha256,
 [string]$SchemaProofPath,
 [ValidatePattern('^[A-F0-9]{64}$')][string]$SchemaProofSha256,
 [switch]$ValidateOnly
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$oldId='20260905T232433Z-9062dc25354442429d2ef07f76f24d94'
$oldSha='98722A2B38A922BBC389FDF7274136F0A729F4EB0382867B03911FC9E75C92D8'
$root=Join-Path $repo ('out/client-certification-release/'+$CandidateId)
$wrapper=Join-Path $PSScriptRoot 'Start-StagedHost.ps1'
$savedWrapper=Join-Path $root 'Start-StagedHost.schema91.before.ps1'
$startRequested=$false
$hosts=@(@{App='api';Task='NeoSTP API';Assembly='NeoSTP.Api';Port=5058},@{App='web';Task='NeoSTP Web';Assembly='NeoSTP.Web';Port=5031})
$report=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('o');Mode=$Mode;Passed=$false;Before=$oldId;After=$CandidateId;SqlWritesIssued=$false;AutomaticRollback=$false;Old91Restarted=$false;Health=@()}
function Require([bool]$ok,[string]$code){if(-not$ok){throw $code}}
function StopExact($item,[string]$releaseId){
 $expected=Join-Path $repo ('out/client-certification-release/'+$releaseId+'/'+$item.App+'/'+$item.Assembly+'.exe')
 foreach($process in @(Get-CimInstance Win32_Process -Filter ("Name='"+$item.Assembly+".exe'")|Where-Object {$_.ExecutablePath-ceq$expected})){
  $owner=Invoke-CimMethod -InputObject $process -MethodName GetOwner
  Require ($owner.ReturnValue-eq0-and($owner.Domain+'\'+$owner.User)-ieq[Security.Principal.WindowsIdentity]::GetCurrent().Name) 'PROCESS_OWNER_CHANGED'
  Require ((Invoke-CimMethod -InputObject $process -MethodName Terminate -Arguments @{Reason=[uint32]0}).ReturnValue-eq0) 'PROCESS_STOP_FAILED'
 }
}
try{
 $hostProof=Get-Content -LiteralPath (Join-Path $root 'schema92-clone-hosts.json') -Raw|ConvertFrom-Json
 Require ($hostProof.Passed-and$hostProof.ProbeHostsStopped-and$hostProof.CandidateId-ceq$CandidateId-and$hostProof.CandidateSha256-ceq$CandidateSha256-and$hostProof.SourceDatabaseUsed-eq$false-and$hostProof.HostResults.Count-eq2-and@($hostProof.HostResults|Where-Object {-not$_.Healthy}).Count-eq0) 'TESTED_SCHEMA92_CANDIDATE_REQUIRED'
 if($Mode-eq'StopForSchemaUpgrade'){
  # The generic binary switch preflight checks both manifests, rollback binary hashes,
  # old listeners/owners, task wrapper and new fiscal policy without stopping anything.
  & (Join-Path $PSScriptRoot 'Switch-TenantSchemaRelease.ps1') -OldId $oldId -OldSha $oldSha -NewId $CandidateId -NewSha $CandidateSha256 -ValidateOnly
  Require (-not(Test-Path -LiteralPath $savedWrapper)) 'SCHEMA_CUTOVER_ALREADY_STARTED'
  if($ValidateOnly){$report.Passed=$true;return}
  [IO.File]::Copy($wrapper,$savedWrapper,$false)
  foreach($item in $hosts){Export-ScheduledTask -TaskName $item.Task|Set-Content -LiteralPath (Join-Path $root ($item.Task.Replace(' ','-')+'.schema91.before.xml')) -Encoding utf8}
  foreach($item in $hosts){Disable-ScheduledTask -TaskName $item.Task|Out-Null;Stop-ScheduledTask -TaskName $item.Task;StopExact $item $oldId}
  Start-Sleep -Milliseconds 700
  Require (-not(Get-NetTCPConnection -State Listen -LocalPort 5031,5058 -ErrorAction SilentlyContinue)) 'PORTS_NOT_STOPPED'
  $report.Passed=$true;$report.ReadyForReviewedSchemaApply=$true
 }else{
  Require (Test-Path -LiteralPath $savedWrapper -PathType Leaf) 'STOP_BASELINE_REQUIRED'
  $proofPath=[IO.Path]::GetFullPath((Join-Path $repo $SchemaProofPath))
  Require ($proofPath.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-and$SchemaProofSha256-and(Get-FileHash -LiteralPath $proofPath -Algorithm SHA256).Hash-ceq$SchemaProofSha256) 'SCHEMA_PROOF_HASH_REJECTED'
  $proof=Get-Content -LiteralPath $proofPath -Raw|ConvertFrom-Json
  Require ($proof.Mode-ceq'Apply'-and$proof.Passed-and$proof.Applied-and$proof.ExpectedMigrationCount-eq92-and$proof.ExpectedMigration-ceq'20260906152921_CLI23_CalendarBillingAndCompanyModuleGrants'-and$proof.SourceDatabase-ceq'NeoSTP_Cloud'-and$proof.TargetDatabase-ceq'NeoSTP_Cloud'-and$proof.BackupVerified-and$proof.OriginalRowsPreserved-and$proof.DbccErrors-eq0) 'APPLIED_SCHEMA92_PROOF_REQUIRED'
  $report.SchemaProofSha256=$SchemaProofSha256
  $candidate=Join-Path $repo ('tmp/production-candidate/'+$CandidateId+'/candidate-manifest.json')
  Require ((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash-ceq$CandidateSha256) 'CANDIDATE_HASH_CHANGED'
  $manifest=Get-Content -LiteralPath $candidate -Raw|ConvertFrom-Json
  Require ($manifest.PublicationComplete-and$manifest.CandidateId-ceq$CandidateId) 'CANDIDATE_INCOMPLETE'
  $staged=Get-Content -LiteralPath (Join-Path $root 'staging-manifest.json') -Raw|ConvertFrom-Json
  Require ($staged.CandidateId-ceq$CandidateId-and$staged.CandidateManifestSha256-ceq$CandidateSha256) 'STAGING_HASH_CHANGED'
  Require ($staged.RequiredStartArguments-ceq'--environment Development --Ops:Database:ApplyMigrationsOnStartup false --Ops:Database:SeedOnStartup false --SuperAdmin:BootstrapEnabled false --EmpresaPrueba:Enabled false --DemoComercial:Enabled false') 'START_ARGUMENTS_CHANGED'
  foreach($app in @('api','web')){
   $private=Get-Content -LiteralPath (Join-Path $root ($app+'/appsettings.Local.json')) -Raw|ConvertFrom-Json
   $policy=$private.Dte.TenantSchemas.'23'
   Require ($policy.Nit-ceq'06232705261148'-and$policy.Ambiente-ceq'PRUEBAS'-and$policy.Profile-ceq'MH_20260825'-and$private.Billing.Calendar.Enabled-eq($app-ceq'api')) 'STAGED_POLICY_CHANGED'
   $private=$null;$policy=$null
  }
  foreach($entry in @($manifest.Files|Where-Object {$_.Root-in@('api','web')})){
   $base=Join-Path $root $entry.Root;$file=[IO.Path]::GetFullPath((Join-Path $base $entry.Path))
   Require ($file.StartsWith($base+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-and(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash-ceq$entry.Sha256) 'STAGED_BINARY_CHANGED'
  }
  $original=[IO.File]::ReadAllText($savedWrapper)
  Require ($original.Contains($oldId)-and$original.Contains($oldSha)-and[IO.File]::ReadAllText($wrapper)-ceq$original) 'WRAPPER_BASELINE_CHANGED'
  foreach($item in $hosts){$task=Get-ScheduledTask -TaskName $item.Task;Require ($task.State-eq'Disabled'-and$task.Actions.Count-eq1-and$task.Actions.Arguments.Contains($wrapper)) 'TASK_NOT_STOPPED'}
  Require (-not(Get-NetTCPConnection -State Listen -LocalPort 5031,5058 -ErrorAction SilentlyContinue)) 'PORTS_NOT_STOPPED'
  if($ValidateOnly){$report.Passed=$true;return}
  $startRequested=$true
  [IO.File]::WriteAllText($wrapper,$original.Replace($oldId,$CandidateId).Replace($oldSha,$CandidateSha256))
  foreach($item in $hosts){Enable-ScheduledTask -TaskName $item.Task|Out-Null;Start-ScheduledTask -TaskName $item.Task}
  foreach($item in $hosts){
   $healthy=$false;$deadline=[DateTime]::UtcNow.AddSeconds(40)
   while([DateTime]::UtcNow-lt$deadline){try{$response=Invoke-WebRequest -Uri ('http://127.0.0.1:'+$item.Port+'/health/ready') -TimeoutSec 3 -SkipHttpErrorCheck;if($response.StatusCode-eq200){$listener=Get-NetTCPConnection -State Listen -LocalPort $item.Port|Select-Object -First 1;$process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$listener.OwningProcess);if($process.ExecutablePath-ceq(Join-Path $root ($item.App+'/'+$item.Assembly+'.exe'))){$healthy=$true;break}}}catch{};Start-Sleep -Milliseconds 500}
   Require $healthy 'NEW_SCHEMA92_HOST_NOT_HEALTHY'
   $report.Health+=@{App=$item.App;Status=200;PID=$listener.OwningProcess}
  }
  $report.Passed=$true
 }
}catch{
 $report.FailureCode=if($_.Exception.Message-cmatch'\A[A-Z0-9_]+\z'){$_.Exception.Message}else{'RUNTIME_REVIEW_REQUIRED'}
 if($startRequested-and-not$ValidateOnly-and$Mode-eq'StartAfterSchemaUpgrade'){
  foreach($item in $hosts){try{Disable-ScheduledTask -TaskName $item.Task|Out-Null;Stop-ScheduledTask -TaskName $item.Task;StopExact $item $CandidateId}catch{$report.StopFailure=$true}}
  $report.RecoveryRequired='SQL92 remains; tasks disabled. Old91 is schema-incompatible and was not restarted. Repair forward or execute a separately reviewed restore using the fresh backup.'
 }
}finally{
 if(-not$ValidateOnly-and(Test-Path -LiteralPath $root)){$report|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root ('schema92-runtime-'+$Mode+'.json')) -Encoding utf8}
 $report|ConvertTo-Json -Depth 5
}
if(-not$report.Passed){exit 1}
