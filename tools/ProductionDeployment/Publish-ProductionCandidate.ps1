[CmdletBinding()]
param([switch]$ValidateOnly)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$targets=Join-Path $PSScriptRoot 'ProductionCandidate.targets'
[xml]$filter=[IO.File]::ReadAllText($targets)
$forbidden=[string]$filter.Project.PropertyGroup.NeoCandidateForbidden
$allowed=[string]$filter.Project.PropertyGroup.NeoCandidateAllowed
function Test-CandidateSecret([string]$text){
 # Match SQL connection context on the same line, in either key order. A JS
 # variable named password by itself is not a connection-string credential.
 $privateKey='-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----'
 $sqlCredential='(?im)^(?=[^\r\n]*\b(?:Server|Data Source|Address|Addr|Network Address)\s*=)(?=[^\r\n]*\b(?:Password|Pwd)\s*=\s*[^;\s]{3,})[^\r\n]+$'
 return ($text-match$privateKey-or$text-match$sqlCredential)
}
function Assert-NoLink([string]$path){
 $cursor=[IO.Path]::GetFullPath($path)
 while($cursor){
  if(Test-Path -LiteralPath $cursor){if((Get-Item -LiteralPath $cursor -Force).Attributes-band[IO.FileAttributes]::ReparsePoint){throw 'Reparse-point path rejected.'}}
  $parent=[IO.Path]::GetDirectoryName($cursor);if($parent-eq$cursor){break};$cursor=$parent
 }
}
# No optional output override: only a new UTC/GUID sibling under this workspace is permitted.
$parent=Join-Path $repo 'tmp/production-candidate'
Assert-NoLink $parent
Assert-NoLink $targets
$apps=@(
 @{Name='web';Project='src/NeoSTP.Web/NeoSTP.Web.csproj';Assembly='NeoSTP.Web'},
 @{Name='api';Project='src/NeoSTP.Api/NeoSTP.Api.csproj';Assembly='NeoSTP.Api'},
 @{Name='worker';Project='src/NeoSTP.Worker/NeoSTP.Worker.csproj';Assembly='NeoSTP.Worker'})
foreach($app in $apps){
 $project=Join-Path $repo $app.Project
 if(-not(Test-Path -LiteralPath $project)){throw 'Required project missing.'}
 Assert-NoLink $project
}
# Read no appsettings, user secrets, certificate stores, databases or service configuration.
$negative=@('appsettings.json','appsettings.Local.json','appsettings.Development.json','appsettings.Local.example.json','storage/blob.pdf','DataProtection/key-a.xml','keys/key.xml','certs/private.pfx','.env','secrets.json','backup.bak','uploads/customer.png')
foreach($name in $negative){if($name-notmatch$forbidden){throw 'Candidate forbidden-path contract failed.'}}
foreach($name in @('NeoSTP.Api.dll','NeoSTP.Web.deps.json','NeoSTP.Worker.runtimeconfig.json','wwwroot/css/neostp.css')){
 if($name-match$forbidden-or$name-notmatch$allowed){throw 'Candidate runtime allowlist contract failed.'}
}
# Regression checks use the actual public asset and synthetic non-operational strings.
$loginAsset=Join-Path $repo 'src/NeoSTP.Web/wwwroot/js/login-password.js'
if(Test-CandidateSecret ([IO.File]::ReadAllText($loginAsset))){throw 'Legitimate password UI asset was incorrectly rejected.'}
if(Test-CandidateSecret 'const password = document.getElementById("Password");'){throw 'JavaScript password variable was incorrectly rejected.'}
foreach($sample in @('Server=synthetic.invalid;Database=Fixture;User ID=synthetic;Password=non-operational-fixture;', 'Pwd=non-operational-fixture;Data Source=synthetic.invalid;Initial Catalog=Fixture;', '-----BEGIN PRIVATE KEY-----', '-----BEGIN RSA PRIVATE KEY-----')){
 if(-not(Test-CandidateSecret $sample)){throw 'Synthetic credential/private-key regression was not rejected.'}
}
# StaticWebAssets has its own copy pipeline. Inspect every source wwwroot before
# any dotnet invocation; never follow reparse points or print rejected filenames.
$staticSourceFileCount=0
foreach($app in $apps){
 $staticRoot=Join-Path ([IO.Path]::GetDirectoryName((Join-Path $repo $app.Project))) 'wwwroot'
 if(-not(Test-Path -LiteralPath $staticRoot)){continue}
 Assert-NoLink $staticRoot
 $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($staticRoot)
 while($pending.Count){
  foreach($entry in @(Get-ChildItem -LiteralPath $pending.Pop() -Force)){
   if($entry.Attributes-band[IO.FileAttributes]::ReparsePoint){throw 'Source static assets contain a reparse point; publication rejected.'}
   $relative=$entry.FullName.Substring($staticRoot.Length+1).Replace('\','/')
   if($relative-match$forbidden){throw 'Source static assets contain a prohibited path; publication rejected.'}
   if($entry.PSIsContainer){$pending.Push($entry.FullName);continue}
   if($relative-notmatch$allowed){throw 'Source static asset type is not approved; publication rejected.'}
   if($entry.Extension-match'^\.(json|config|js|css|html|htm|svg|txt|md|map)$'-or$entry.Name-eq'LICENSE'){
    if(Test-CandidateSecret ([IO.File]::ReadAllText($entry.FullName))){throw 'Source static asset content failed the credential check; publication rejected.'}
   }
   $staticSourceFileCount++
  }
 }
}
if($ValidateOnly){Write-Output 'Candidate path/filter preflight passed; no dotnet, output artifact, host or database operation.';return}
$id=[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')+'-'+[Guid]::NewGuid().ToString('N')
$root=[IO.Path]::GetFullPath((Join-Path $parent $id))
if(-not$root.StartsWith($parent+'\',[StringComparison]::OrdinalIgnoreCase)-or(Test-Path -LiteralPath $root)){throw 'Fresh candidate identity rejected.'}
[void][IO.Directory]::CreateDirectory($root)
$logs=Join-Path $root 'logs';[void][IO.Directory]::CreateDirectory($logs)
$manifest=[ordered]@{SchemaVersion=1;CandidateId=$id;GeneratedAtUtc=[DateTime]::UtcNow.ToString('O');Configuration='Release';Framework='net10.0';SelfContained=$false;ReadyForDeployment=$false;PublicationComplete=$false;EnvironmentConfigurationIncluded=$false;HostsStarted=$false;DatabaseAccessed=$false;ServicesChanged=$false;StaticSourceFilesChecked=$staticSourceFileCount;Roots=@();Files=@();FailureType=$null}
try{
 foreach($app in $apps){
  $output=Join-Path $root $app.Name
  if(Test-Path -LiteralPath $output){throw 'Candidate project output already exists.'}
  $project=Join-Path $repo $app.Project
  $log=Join-Path $logs ($app.Name+'-publish.log')
  # Framework-dependent, sequential publication. No service installer or host is invoked.
  & dotnet publish $project -c Release --no-restore --self-contained false -o $output '-p:NeoProductionCandidate=true' ('-p:CustomAfterMicrosoftCommonTargets='+$targets) '-p:DebugType=None' '-p:DebugSymbols=false' '-v:minimal' *> $log
  if($LASTEXITCODE-ne0){throw 'Candidate publication failed; inspect the scoped publish log.'}
  Assert-NoLink $output
  if(-not(Test-Path -LiteralPath (Join-Path $output '.production-filter-applied'))){throw 'Publish filter marker absent; candidate rejected.'}
  foreach($suffix in @('.dll','.deps.json','.runtimeconfig.json')){
   if(-not(Test-Path -LiteralPath (Join-Path $output ($app.Assembly+$suffix)))){throw 'Required runtime artifact missing.'}
  }
  $entries=@(Get-ChildItem -LiteralPath $output -Recurse -Force)
  if(@($entries|Where-Object{$_.Attributes-band[IO.FileAttributes]::ReparsePoint}).Count){throw 'Candidate contains a reparse point.'}
  foreach($file in @($entries|Where-Object{-not$_.PSIsContainer})){
   $relative=$file.FullName.Substring($output.Length+1).Replace('\','/')
   if($relative-ne'.production-filter-applied'-and($relative-match$forbidden-or$relative-notmatch$allowed)){throw 'Candidate contains a prohibited or unapproved file; no deployment is permitted.'}
   if($file.Extension-match'^\.(json|config|js|css|html|htm|svg|txt|map)$'){
    $text=[IO.File]::ReadAllText($file.FullName)
    if(Test-CandidateSecret $text){throw 'Potential secret material detected; candidate rejected without displaying its content.'}
   }
   $manifest.Files+=@{Root=$app.Name;Path=$relative;Bytes=$file.Length;Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
  }
  $manifest.Roots+=@{Name=$app.Name;RelativePath=$app.Name;Assembly=$app.Assembly;FileCount=@($manifest.Files|Where-Object{$_.Root-eq$app.Name}).Count}
  Write-Output ('Candidate root validated: '+$app.Name)
 }
 $manifest.PublicationComplete=$true
 $manifest.ToolHashes=@(@('Publish-ProductionCandidate.ps1','ProductionCandidate.targets')|ForEach-Object{@{Path=$_;Sha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $_) -Algorithm SHA256).Hash}})
 Write-Output ('Candidate prepared (not configured or installed): '+$root)
}catch{
 $manifest.FailureType=$_.Exception.GetType().Name
 throw
}finally{
 $manifest|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $root 'candidate-manifest.json') -Encoding utf8
}