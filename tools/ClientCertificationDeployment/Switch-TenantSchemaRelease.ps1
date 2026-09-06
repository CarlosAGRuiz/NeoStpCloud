[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidatePattern('^\d{8}T\d{6}Z-[a-f0-9]{32}$')][string]$OldId,
 [Parameter(Mandatory)][ValidatePattern('^\d{8}T\d{6}Z-[a-f0-9]{32}$')][string]$NewId,
 [Parameter(Mandatory)][ValidatePattern('^[A-F0-9]{64}$')][string]$OldSha,
 [Parameter(Mandatory)][ValidatePattern('^[A-F0-9]{64}$')][string]$NewSha,
 [switch]$ValidateOnly
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($oldId-ceq$newId){throw 'DIFFERENT_RELEASES_REQUIRED'}
$root=Join-Path $repo ('out/client-certification-release/'+$newId)
$candidate=Join-Path $repo ('tmp/production-candidate/'+$newId)
$staged=Get-Content -LiteralPath (Join-Path $root 'staging-manifest.json') -Raw|ConvertFrom-Json
if($staged.CandidateId-cne$newId-or$staged.CandidateManifestSha256-cne$newSha-or(Get-FileHash -LiteralPath (Join-Path $candidate 'candidate-manifest.json')).Hash-cne$newSha){throw 'ENVELOPE_RELEASE_REJECTED'}
$manifest=Get-Content -LiteralPath (Join-Path $candidate 'candidate-manifest.json') -Raw|ConvertFrom-Json
if($manifest.CandidateId-cne$newId-or-not$manifest.PublicationComplete-or$manifest.EnvironmentConfigurationIncluded){throw 'CANDIDATE_NOT_COMPLETE'}
# Verify rollback binaries before stopping anything. Both releases must still match
# their independently supplied publication manifest hashes.
foreach($releaseSpec in @(@{Id=$oldId;Sha=$oldSha},@{Id=$newId;Sha=$newSha})){
 $releaseRoot=Join-Path $repo ('out/client-certification-release/'+$releaseSpec.Id)
 $candidateManifest=Join-Path $repo ('tmp/production-candidate/'+$releaseSpec.Id+'/candidate-manifest.json')
 if((Get-FileHash -LiteralPath $candidateManifest -Algorithm SHA256).Hash-cne$releaseSpec.Sha){throw 'RELEASE_MANIFEST_HASH_CHANGED'}
 $verifiedManifest=Get-Content -LiteralPath $candidateManifest -Raw|ConvertFrom-Json
 foreach($entry in @($verifiedManifest.Files|Where-Object{$_.Root-in@('api','web')})){
  $base=Join-Path $releaseRoot $entry.Root
  $file=[IO.Path]::GetFullPath((Join-Path $base $entry.Path))
  if(-not$file.StartsWith($base.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'RELEASE_PATH_REJECTED'}
  $cursor=$file
  while($cursor-and$cursor.Length-ge$repo.Length){if((Get-Item -LiteralPath $cursor -Force).Attributes-band[IO.FileAttributes]::ReparsePoint){throw 'RELEASE_REPARSE_REJECTED'};$cursor=[IO.Path]::GetDirectoryName($cursor)}
  if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash-cne$entry.Sha256){throw 'RELEASE_BINARY_HASH_CHANGED'}
 }
}
foreach($app in @('api','web')){
 $private=Get-Content -LiteralPath (Join-Path $root ($app+'/appsettings.Local.json')) -Raw|ConvertFrom-Json
 $policy=$private.Dte.TenantSchemas.'23'
 if($policy.Nit-cne'06232705261148'-or$policy.Ambiente-cne'PRUEBAS'-or$policy.Profile-cne'MH_20260825'){throw 'TENANT_SCHEMA_CONFIGURATION_REJECTED'}
 $private=$null;$policy=$null
}
foreach($entry in @($manifest.Files|Where-Object{$_.Root-in@('api','web')})){
 $file=Join-Path (Join-Path $root $entry.Root) $entry.Path
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash-cne$entry.Sha256){throw 'ENVELOPE_STAGED_HASH_CHANGED'}
}
$wrapper=Join-Path $PSScriptRoot 'Start-StagedHost.ps1'
$original=[IO.File]::ReadAllText($wrapper)
if(-not$original.Contains($oldId)-or-not$original.Contains($oldSha)){throw 'ORIGINAL_WRAPPER_CHANGED'}
$hosts=@(@{App='api';Name='NeoSTP API';Assembly='NeoSTP.Api';Port=5058},@{App='web';Name='NeoSTP Web';Assembly='NeoSTP.Web';Port=5031})
$report=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('O');Passed=$false;Before=$oldId;After=$newId;DatabaseSchemaChanged=$false;Health=@()}
foreach($item in $hosts){
 $listeners=@(Get-NetTCPConnection -State Listen -LocalPort $item.Port)
 $pids=@($listeners.OwningProcess|Select-Object -Unique)
 if($pids.Count-ne1){throw 'HOST_LISTENER_CHANGED'}
 $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$pids[0])
 $expected=Join-Path $repo ('out/client-certification-release/'+$oldId+'/'+$item.App+'/'+$item.Assembly+'.exe')
 if($process.ExecutablePath-cne$expected){throw 'OLD_RELEASE_PROCESS_CHANGED'}
 $owner=Invoke-CimMethod -InputObject $process -MethodName GetOwner
 if($owner.ReturnValue-ne0-or($owner.Domain+'\'+$owner.User)-ine[Security.Principal.WindowsIdentity]::GetCurrent().Name){throw 'HOST_OWNER_CHANGED'}
 $item.PID=$pids[0];$item.Path=$expected
 $task=Get-ScheduledTask -TaskName $item.Name
 if($task.State-eq'Disabled'-or$task.Actions.Count-ne1-or-not$task.Actions.Arguments.Contains($wrapper)){throw 'TASK_WRAPPER_CHANGED'}
}
if($ValidateOnly){[pscustomobject]@{Passed=$true;ValidateOnly=$true;Before=$oldId;After=$newId;RuntimeChanged=$false;DatabaseSchemaChanged=$false}|ConvertTo-Json;return}
if(Test-Path -LiteralPath (Join-Path $root 'Start-StagedHost.before.ps1')){throw 'RELEASE_SWITCH_ALREADY_ATTEMPTED'}
[IO.File]::WriteAllText((Join-Path $root 'Start-StagedHost.before.ps1'),$original)
try{
 foreach($item in $hosts){Disable-ScheduledTask -TaskName $item.Name|Out-Null}
 foreach($item in $hosts){Stop-ScheduledTask -TaskName $item.Name}
 foreach($item in $hosts){
  $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$item.PID)
  if($process){if($process.ExecutablePath-cne$item.Path){throw 'PID_REUSED'};$termination=Invoke-CimMethod -InputObject $process -MethodName Terminate -Arguments @{Reason=[uint32]0};if($termination.ReturnValue-ne0){throw 'HOST_TERMINATION_FAILED'}}
 }
 Start-Sleep -Milliseconds 500
 if(Get-NetTCPConnection -State Listen -LocalPort 5031,5058 -ErrorAction SilentlyContinue){throw 'HOST_PORTS_NOT_FREE'}
 [IO.File]::WriteAllText($wrapper,$original.Replace($oldId,$newId).Replace($oldSha,$newSha))
 foreach($item in $hosts){Enable-ScheduledTask -TaskName $item.Name|Out-Null;Start-ScheduledTask -TaskName $item.Name}
 foreach($item in $hosts){
  $ok=$false;$deadline=[DateTime]::UtcNow.AddSeconds(30)
  while([DateTime]::UtcNow-lt$deadline){try{$r=Invoke-WebRequest -Uri ('http://127.0.0.1:'+$item.Port+'/health/ready') -TimeoutSec 3 -SkipHttpErrorCheck;if($r.StatusCode-eq200){$ok=$true;break}}catch{};Start-Sleep -Milliseconds 500}
  if(-not$ok){throw 'NEW_HOST_NOT_HEALTHY'}
  $listener=Get-NetTCPConnection -State Listen -LocalPort $item.Port|Select-Object -First 1
  $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$listener.OwningProcess)
  $expected=Join-Path $root ($item.App+'/'+$item.Assembly+'.exe')
  if($process.ExecutablePath-cne$expected){throw 'NEW_HOST_PATH_REJECTED'}
  $report.Health+=@{Host=$item.Name;Status=200;PID=$listener.OwningProcess;Path=$process.ExecutablePath}
 }
 $report.Passed=$true
}catch{
 $failure=$_
 $report.Failure=$failure.Exception.Message
 try{
  foreach($item in $hosts){Disable-ScheduledTask -TaskName $item.Name|Out-Null;Stop-ScheduledTask -TaskName $item.Name}
  foreach($item in $hosts){
   $expectedNew=Join-Path $root ($item.App+'/'+$item.Assembly+'.exe')
   foreach($process in @(Get-CimInstance Win32_Process -Filter ("Name='"+$item.Assembly+".exe'")|Where-Object{$_.ExecutablePath-in@($item.Path,$expectedNew)})){
    $owner=Invoke-CimMethod -InputObject $process -MethodName GetOwner
    if($owner.ReturnValue-ne0-or($owner.Domain+'\'+$owner.User)-ine[Security.Principal.WindowsIdentity]::GetCurrent().Name){throw 'ROLLBACK_HOST_OWNER_CHANGED'}
    $terminated=Invoke-CimMethod -InputObject $process -MethodName Terminate -Arguments @{Reason=[uint32]0}
    if($terminated.ReturnValue-ne0){throw 'ROLLBACK_HOST_TERMINATION_FAILED'}
   }
  }
  $rollbackDeadline=[DateTime]::UtcNow.AddSeconds(10)
  while((Get-NetTCPConnection -State Listen -LocalPort 5031,5058 -ErrorAction SilentlyContinue)-and[DateTime]::UtcNow-lt$rollbackDeadline){Start-Sleep -Milliseconds 500}
  if(Get-NetTCPConnection -State Listen -LocalPort 5031,5058 -ErrorAction SilentlyContinue){throw 'ROLLBACK_PORTS_NOT_FREE'}
  [IO.File]::WriteAllText($wrapper,$original)
  foreach($item in $hosts){Enable-ScheduledTask -TaskName $item.Name|Out-Null;Start-ScheduledTask -TaskName $item.Name}
  $report.RollbackStarted=$true
  foreach($item in $hosts){
   $healthy=$false;$deadline=[DateTime]::UtcNow.AddSeconds(30)
   while([DateTime]::UtcNow-lt$deadline){try{$r=Invoke-WebRequest -Uri ('http://127.0.0.1:'+$item.Port+'/health/ready') -TimeoutSec 3 -SkipHttpErrorCheck;if($r.StatusCode-eq200){$listener=Get-NetTCPConnection -State Listen -LocalPort $item.Port|Select-Object -First 1;$p=Get-CimInstance Win32_Process -Filter ('ProcessId='+$listener.OwningProcess);if($p.ExecutablePath-ceq$item.Path){$healthy=$true;break}}}catch{};Start-Sleep -Milliseconds 500}
   if(-not$healthy){throw 'ROLLBACK_HOST_NOT_HEALTHY'}
  }
  $report.RollbackHealthVerified=$true
 }catch{$report.RollbackFailure=$_.Exception.Message}
 throw $failure
}finally{
 $report|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root 'live-release.json') -Encoding utf8
 $report|ConvertTo-Json -Depth 5
}
