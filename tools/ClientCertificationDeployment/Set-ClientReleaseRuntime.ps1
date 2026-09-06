param([ValidateSet('StopReviewedHosts','StartReviewedRelease')][string]$Mode)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release=Join-Path $repo 'out/client-certification-release/20260905T222711Z-7b556dd2c9b74a8680c6dbeb4edc03d6'
$proof=Get-Content -LiteralPath (Join-Path $repo 'tmp/client-certification-release-2026-09-05/staged-clone-hosts.json') -Raw|ConvertFrom-Json
if(-not$proof.Passed-or-not$proof.ProbeHostsStopped-or$proof.HostResults.Count-ne2){throw 'CLONE_HOST_PROOF_REQUIRED'}
$report=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('O');Mode=$Mode;Passed=$false;Tasks=@()}
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class ReviewedClientProcess {
 [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder text,ref uint size);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool TerminateProcess(IntPtr h,uint code);
 public static string Image(int pid){var h=OpenProcess(0x1000,false,pid);if(h==IntPtr.Zero)return null;try{var s=new StringBuilder(32768);uint n=32768;return QueryFullProcessImageName(h,0,s,ref n)?s.ToString():null;}finally{CloseHandle(h);}}
 public static void Stop(int pid){var h=OpenProcess(1,false,pid);if(h==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());try{if(!TerminateProcess(h,0))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}finally{CloseHandle(h);}}
}
'@
try{
 $tasks=@(Get-ScheduledTask -TaskName 'NeoSTP API','NeoSTP Web')
 if($tasks.Count-ne2){throw 'EXACT_TASKS_REQUIRED'}
 if($Mode-eq'StopReviewedHosts'){
  $hosts=@(@{PID=19600;Parent=19360;Assembly='NeoSTP.Api';Port=5058},@{PID=12504;Parent=17576;Assembly='NeoSTP.Web';Port=5031})
  foreach($hostInfo in $hosts){
   $expected=Join-Path $repo ('src/'+$hostInfo.Assembly+'/bin/Debug/net10.0/'+$hostInfo.Assembly+'.exe')
   $process=Get-CimInstance Win32_Process -Filter ('ProcessId='+$hostInfo.PID)
   if([ReviewedClientProcess]::Image($hostInfo.PID)-cne$expected-or$process.ParentProcessId-ne$hostInfo.Parent-or[ReviewedClientProcess]::Image($hostInfo.Parent)-cne'C:\Program Files\dotnet\dotnet.exe'){throw 'REVIEWED_PROCESS_CHANGED'}
   $owner=Invoke-CimMethod -InputObject $process -MethodName GetOwner
   if($owner.ReturnValue-ne0-or($owner.Domain+'\'+$owner.User)-ine[Security.Principal.WindowsIdentity]::GetCurrent().Name){throw 'PROCESS_OWNER_CHANGED'}
   $parent=Get-CimInstance Win32_Process -Filter ('ProcessId='+$hostInfo.Parent)
   $parentOwner=Invoke-CimMethod -InputObject $parent -MethodName GetOwner
   if($parentOwner.ReturnValue-ne0-or($parentOwner.Domain+'\'+$parentOwner.User)-ine[Security.Principal.WindowsIdentity]::GetCurrent().Name){throw 'PARENT_PROCESS_OWNER_CHANGED'}
   $ports=@(Get-NetTCPConnection -State Listen -LocalPort $hostInfo.Port)
   if($ports.Count-eq0-or@($ports|Where-Object{$_.OwningProcess-ne$hostInfo.PID}).Count-gt0){throw 'REVIEWED_LISTENER_CHANGED'}
  }
  foreach($task in $tasks){
   $saved=Join-Path $release ($task.TaskName.Replace(' ','-')+'.before.xml')
   if(Test-Path -LiteralPath $saved){if($task.State-ne'Disabled'){throw 'TASK_BASELINE_ALREADY_SAVED'}}
   else{Export-ScheduledTask -TaskName $task.TaskName|Set-Content -LiteralPath $saved -Encoding utf8}
  }
  foreach($task in $tasks){Disable-ScheduledTask -TaskName $task.TaskName|Out-Null}
  foreach($hostInfo in $hosts){
   [ReviewedClientProcess]::Stop($hostInfo.Parent)
   if(Get-Process -Id $hostInfo.PID -ErrorAction SilentlyContinue){[ReviewedClientProcess]::Stop($hostInfo.PID)}
  }
  Start-Sleep -Milliseconds 700
  if(Get-NetTCPConnection -State Listen -LocalPort 5031,5058 -ErrorAction SilentlyContinue){throw 'LISTENERS_NOT_STOPPED'}
  $report.Passed=$true
 }elseif($Mode-eq'StartReviewedRelease'){
  # The application also validates the exact schema before serving. This guard prevents starting before provisioning.
  $provision=Get-Content -LiteralPath (Join-Path $repo 'tmp/client-certification-release-2026-09-05/provision-ApplyLive.json') -Raw|ConvertFrom-Json
  if(-not$provision.Applied-or$provision.TargetDatabase-cne'NeoSTP_Cloud'-or$provision.EmpresaId-ne23){throw 'CLIENT_PROVISIONING_REQUIRED'}
  if(@($tasks|Where-Object{$_.State-ne'Disabled'}).Count-gt0-or(Get-NetTCPConnection -State Listen -LocalPort 5031,5058 -ErrorAction SilentlyContinue)){throw 'CLEAN_START_REQUIRED'}
  $wrapper=Join-Path $PSScriptRoot 'Start-StagedHost.ps1'
  foreach($app in @('api','web')){
   $name=if($app-eq'api'){'NeoSTP API'}else{'NeoSTP Web'}
   $arguments='-NoProfile -NonInteractive -WindowStyle Hidden -File "'+$wrapper+'" -App '+$app
   $action=New-ScheduledTaskAction -Execute (Join-Path $PSHOME 'powershell.exe') -Argument $arguments -WorkingDirectory $repo
   if(-not(Test-Path -LiteralPath $action.Execute)){$action=New-ScheduledTaskAction -Execute (Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe') -Argument $arguments -WorkingDirectory $repo}
   Set-ScheduledTask -TaskName $name -Action $action|Out-Null
   Enable-ScheduledTask -TaskName $name|Out-Null
   Start-ScheduledTask -TaskName $name
   $report.Tasks+=@{Name=$name;Release='20260905T222711Z-7b556dd2c9b74a8680c6dbeb4edc03d6'}
  }
  $report.ScheduledStartRequested=$true
  $report.RuntimeHealthVerified=$false
  $report.Passed=$true
 }else{throw 'MODE_REQUIRED'}
}finally{
 $report|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $repo ('tmp/client-certification-release-2026-09-05/runtime-'+$Mode+'.json')) -Encoding utf8
 $report|ConvertTo-Json -Depth 4
}
