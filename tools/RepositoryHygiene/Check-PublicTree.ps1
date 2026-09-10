#requires -Version 7.0
param([switch]$SelfTest)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))

function Test-PrivatePath([string]$Path) {
    $Path -match '(?i)(^|/)(\.claude|\.codex|\.codex-remote-attachments|private|out|output|outputs|bin|obj|logs|TestResults)/|^docs/auditoria-[^/]+/|^tools/(CertHarness|Client[^/]*|CompanyPreflight|KeyRecoveryVerification|ProductionDeployment|ProductionReadinessSqlVerification|WindowsServices)/|(^|/)(appsettings(?:\.[^.]+)?\.Local\.json|secrets\.json|\.env(?:\..*)?)$|\.(pfx|p12|p8|key|bak|mdf|ldf|trx|log)$'
}
function Test-PrivateContent([string]$Body) {
    # High-confidence patterns only. Example prose and synthetic test literals are
    # not evidence of real credentials. This check complements human review.
    $Body -match '(?s)-----BEGIN (?:RSA |EC |ENCRYPTED )?PRIVATE KEY-----\s+[A-Za-z0-9+/=\r\n]{64,}' -or
    $Body -match '\bgh[pousr]_[A-Za-z0-9]{30,}\b|\bgithub_pat_[A-Za-z0-9_]{40,}\b|\bAKIA[0-9A-Z]{16}\b|\bsk_live_[A-Za-z0-9]{20,}\b'
}
if($SelfTest){
    foreach($path in @('.codex-remote-attachments/fixture/photo.jpg','private/report.json','docs/auditoria-fixture/report.md','tools/ClientFixture/run.ps1','keys/example.pfx','src/App/appsettings.Local.json','.env','out/App.dll')){
        if(-not(Test-PrivatePath $path)){throw 'Private-path regression failed.'}
    }
    foreach($path in @('src/App/Program.cs','src/App/appsettings.Local.example.json','docs/MAIN-STANDARD.md','tools/RepositoryHygiene/Check-PublicTree.ps1','tests/FixtureTests.cs')){
        if(Test-PrivatePath $path){throw 'Public-path regression failed.'}
    }
    $fakeKey='-----BEGIN PRIVATE KEY-----'+[Environment]::NewLine+('A'*80)
    if(-not(Test-PrivateContent $fakeKey)){throw 'Private-key detector regression failed.'}
    if(Test-PrivateContent 'REPLACE_WITH_LOCAL_SECRET'){throw 'Placeholder regression failed.'}
}
$tracked=& git -C $repo ls-files
if($LASTEXITCODE -ne 0){throw 'Unable to enumerate tracked files.'}
$failures=[Collections.Generic.List[string]]::new()
foreach($path in $tracked){
    if(Test-PrivatePath $path){$failures.Add('Excluded path: '+$path);continue}
    $absolute=Join-Path $repo $path
    if(-not(Test-Path -LiteralPath $absolute -PathType Leaf)){continue}
    if([IO.Path]::GetExtension($path) -match '^\.(cs|cshtml|json|md|ps1|yml|yaml|xml|config|sql|txt|js|cjs|mjs)$'){
        if(Test-PrivateContent ([IO.File]::ReadAllText($absolute))){$failures.Add('Credential pattern: '+$path)}
    }
}
if($failures.Count){$failures | ForEach-Object {Write-Output $_};throw 'Public-tree hygiene failed; review locally before publishing.'}
Write-Output ('Public-tree hygiene passed: '+@($tracked).Count+' tracked files. Human privacy review remains required.')
