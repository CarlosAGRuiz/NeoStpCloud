param([ValidateSet('api','web')][string]$App)
$ErrorActionPreference='Stop'
# Restrict this wrapper's module lookup to the native Windows PowerShell installation.
$env:PSModulePath=[IO.Path]::Combine($env:WINDIR,'System32','WindowsPowerShell','v1.0','Modules')
if($App-notin@('api','web')){throw 'HOST_REQUIRED'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release=Join-Path $repo 'out/client-certification-release/20260905T232433Z-9062dc25354442429d2ef07f76f24d94'
$manifest=Get-Content -LiteralPath (Join-Path $release 'staging-manifest.json') -Raw|ConvertFrom-Json
if($manifest.CandidateManifestSha256-cne'98722A2B38A922BBC389FDF7274136F0A729F4EB0382867B03911FC9E75C92D8'){throw 'STAGED_HOST_MANIFEST_REJECTED'}
$port=if($App-eq'api'){5058}else{5031}
$assembly=if($App-eq'api'){'NeoSTP.Api'}else{'NeoSTP.Web'}
$root=Join-Path $release $App
$arguments=$manifest.RequiredStartArguments+' --urls http://127.0.0.1:'+$port
$process=Start-Process -FilePath (Join-Path $root ($assembly+'.exe')) -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -PassThru -Wait
exit $process.ExitCode
