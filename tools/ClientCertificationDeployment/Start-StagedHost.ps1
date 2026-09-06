param([ValidateSet('api','web')][string]$App)
$ErrorActionPreference='Stop'
# Restrict this wrapper's module lookup to the native Windows PowerShell installation.
$env:PSModulePath=[IO.Path]::Combine($env:WINDIR,'System32','WindowsPowerShell','v1.0','Modules')
if($App-notin@('api','web')){throw 'HOST_REQUIRED'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release=Join-Path $repo 'out/client-certification-release/20260906T153647Z-8691442423df464a9110277a78b50232'
$manifest=Get-Content -LiteralPath (Join-Path $release 'staging-manifest.json') -Raw|ConvertFrom-Json
if($manifest.CandidateManifestSha256-cne'12977FC4D0E0FDA9BE8FAC0644EBD20C2485C11E44881A0527FE74EB5860D23F'){throw 'STAGED_HOST_MANIFEST_REJECTED'}
$port=if($App-eq'api'){5058}else{5031}
$assembly=if($App-eq'api'){'NeoSTP.Api'}else{'NeoSTP.Web'}
$root=Join-Path $release $App
$arguments=$manifest.RequiredStartArguments+' --urls http://127.0.0.1:'+$port
$process=Start-Process -FilePath (Join-Path $root ($assembly+'.exe')) -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -PassThru -Wait
exit $process.ExitCode
