param([ValidateSet('Check','PrepareDirectory','VerifyDirectory')][string]$Mode='Check',[string]$RunId='')
$ErrorActionPreference='Stop'
$allowedRoot='C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup'
function Assert-NoReparse([string]$path){
 $directory=[IO.DirectoryInfo]::new([IO.Path]::GetFullPath($path))
 while($null-ne$directory){if(($directory.Attributes-band[IO.FileAttributes]::ReparsePoint)-ne0){throw 'Reparse point rejected.'};$directory=$directory.Parent}
}
$currentSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$sqlSid=([Security.Principal.NTAccount]::new('NT SERVICE','MSSQLSERVER')).Translate([Security.Principal.SecurityIdentifier]).Value
$allowedSids=@('S-1-5-18','S-1-5-32-544',$currentSid,$sqlSid)
function Assert-RestrictedAcl([string]$path){
 $rules=(Get-Acl -LiteralPath $path).GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])
 if(@($rules).Count-eq0){throw 'ACL missing.'}
 foreach($rule in $rules){
  if($rule.AccessControlType-eq[Security.AccessControl.AccessControlType]::Allow-and$allowedSids-notcontains$rule.IdentityReference.Value){
   if($rule.IdentityReference.Value-eq'S-1-3-0'-and($rule.PropagationFlags-band[Security.AccessControl.PropagationFlags]::InheritOnly)-ne0){continue}
   throw 'Unexpected backup ACL grant.'
  }
 }
 $owner=(Get-Acl -LiteralPath $path).GetOwner([Security.Principal.SecurityIdentifier]).Value
 if($allowedSids-notcontains$owner){throw 'Unexpected backup directory owner.'}
}
if(-not(Test-Path -LiteralPath $allowedRoot -PathType Container)){throw 'Backup parent missing.'}
Assert-NoReparse $allowedRoot
Assert-RestrictedAcl $allowedRoot
if($Mode-ne'Check'){
 if($RunId-notmatch'\A[a-f0-9]{32}\z'){throw 'Run identity rejected.'}
 $target=[IO.Path]::GetFullPath((Join-Path $allowedRoot ('NeoClientUpgrade_'+$RunId)))
 if($target-ne(Join-Path $allowedRoot ('NeoClientUpgrade_'+$RunId))){throw 'Backup directory rejected.'}
 if($Mode-eq'PrepareDirectory'){
  if(Test-Path -LiteralPath $target){throw 'Backup directory already exists.'}
  [void][IO.Directory]::CreateDirectory($target)
 }
 if(-not(Test-Path -LiteralPath $target -PathType Container)){throw 'Backup directory missing.'}
 Assert-NoReparse $target
 Assert-RestrictedAcl $target
 if($Mode-eq'VerifyDirectory'){
  $backup=Join-Path $target 'source-copy-only.bak'
  if(-not(Test-Path -LiteralPath $backup -PathType Leaf)){throw 'Backup artifact missing.'}
  if(((Get-Item -LiteralPath $backup).Attributes-band[IO.FileAttributes]::ReparsePoint)-ne0){throw 'Backup reparse point rejected.'}
  Assert-RestrictedAcl $backup
 }
 [pscustomobject]@{Passed=$true;Directory=$target;RestrictedInheritedAclVerified=$true;BackupFileAclVerified=($Mode-eq'VerifyDirectory')}|ConvertTo-Json -Compress
 exit 0
}
$tasks=@(Get-ScheduledTask -TaskName 'NeoSTP API','NeoSTP Web')
if($tasks.Count-ne2){throw 'Exact host task inventory required.'}
$processes=@(Get-CimInstance Win32_Process|Where-Object{$_.Name-match'\ANeoSTP\.(Api|Web|Worker)(\.exe)?\z'-or($_.CommandLine-and$_.CommandLine-match'(?i)\bNeoSTP\.(Api|Web|Worker)(\.dll|\.exe|\s|"|$)')})
$listeners=@(Get-NetTCPConnection -State Listen|Where-Object{$_.LocalPort-in@(5031,5058)})
$disabled=@($tasks|Where-Object{$_.State-ne'Disabled'}).Count-eq0
[pscustomobject]@{Passed=($disabled-and$processes.Count-eq0-and$listeners.Count-eq0);TasksDisabled=$disabled;HostProcessCount=$processes.Count;ListenerCount=$listeners.Count;RestrictedBackupParentAclVerified=$true}|ConvertTo-Json -Compress
