#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$')]
    [string]$ReleaseVersion,
    [string]$DataRoot = 'C:\ProgramData\NeoSTP\PRODUCTION',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'PRODUCTION_RELEASE_REQUIRES_WINDOWS' }
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dataRootFull = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\', '/')
if ((Split-Path $dataRootFull -Leaf) -ne 'PRODUCTION') { throw 'PRODUCTION_DATA_ROOT_MUST_END_IN_PRODUCTION' }
if ($dataRootFull.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'PRODUCTION_DATA_ROOT_MUST_BE_OUTSIDE_REPOSITORY' }
$status = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'PRODUCTION_RELEASE_GIT_STATUS_FAILED' }
if ($status.Count -ne 0) { throw 'PRODUCTION_RELEASE_DIRTY_SOURCE_NOT_ALLOWED' }
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw 'PRODUCTION_RELEASE_GIT_COMMIT_UNAVAILABLE' }
$shortCommit = $commit.Substring(0, 12)
$releasesRoot = Join-Path $dataRootFull 'releases'
$finalRoot = Join-Path $releasesRoot "$ReleaseVersion-$shortCommit"
$temporaryRoot = Join-Path $releasesRoot ('.preparing-' + [Guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $finalRoot) { throw 'PRODUCTION_RELEASE_ALREADY_EXISTS' }
$migrationVersion = "$ReleaseVersion-$shortCommit"
$migrationArtifact = Join-Path $repoRoot "artifacts\migrations\$migrationVersion"
try {
    New-Item -ItemType Directory -Force -Path $temporaryRoot | Out-Null
    if (-not $SkipBuild) {
        & dotnet build (Join-Path $repoRoot 'NeoSTP.slnx') -c Release
        if ($LASTEXITCODE -ne 0) { throw 'PRODUCTION_RELEASE_BUILD_FAILED' }
    }
    & (Join-Path $repoRoot 'tools\Database\New-MigrationRelease.ps1') -ReleaseVersion $migrationVersion
    if ($LASTEXITCODE -ne 0) { throw 'PRODUCTION_RELEASE_MIGRATION_ARTIFACT_FAILED' }
    $migrationManifestPath = Join-Path $migrationArtifact 'manifest.json'
    $migrationSqlPath = Join-Path $migrationArtifact 'NeoSTP.Migrations.idempotent.sql'
    $migrationManifest = Get-Content -LiteralPath $migrationManifestPath -Raw | ConvertFrom-Json
    if ($migrationManifest.sourceTreeDirty -ne $false -or $migrationManifest.sourceCommit -ne $commit) { throw 'PRODUCTION_RELEASE_MIGRATION_MANIFEST_MISMATCH' }
    $projects = [ordered]@{
        api = 'src\NeoSTP.Api\NeoSTP.Api.csproj'
        web = 'src\NeoSTP.Web\NeoSTP.Web.csproj'
        worker = 'src\NeoSTP.Worker\NeoSTP.Worker.csproj'
    }
    foreach ($app in $projects.GetEnumerator()) {
        $output = Join-Path $temporaryRoot $app.Key
        New-Item -ItemType Directory -Force -Path $output | Out-Null
        & dotnet publish (Join-Path $repoRoot $app.Value) -c Release -o $output --no-restore
        if ($LASTEXITCODE -ne 0) { throw "PRODUCTION_RELEASE_PUBLISH_FAILED: $($app.Key)" }
        Get-ChildItem -LiteralPath $output -File -Filter 'appsettings.Local*.json' -ErrorAction SilentlyContinue | Remove-Item -Force
        $embeddedLogs = Join-Path $output 'logs'
        if (Test-Path -LiteralPath $embeddedLogs) { Remove-Item -LiteralPath $embeddedLogs -Recurse -Force }
        if (-not (Test-Path -LiteralPath (Join-Path $output 'appsettings.Production.json'))) { throw "PRODUCTION_RELEASE_OVERLAY_MISSING: $($app.Key)" }
    }
    $databaseRoot = Join-Path $temporaryRoot 'database'
    New-Item -ItemType Directory -Force -Path $databaseRoot | Out-Null
    Copy-Item -LiteralPath $migrationManifestPath -Destination (Join-Path $databaseRoot 'manifest.json')
    Copy-Item -LiteralPath $migrationSqlPath -Destination (Join-Path $databaseRoot 'NeoSTP.Migrations.idempotent.sql')
    $forbidden = @(Get-ChildItem -LiteralPath $temporaryRoot -Recurse -File | Where-Object {
        $_.Name -match '(?i)^appsettings\.Local.*\.json$|^secrets\.json$|^\.env(?:\..*)?$|\.(?:pfx|p12|p8|key|bak|mdf|ldf|log)$'
    })
    if ($forbidden.Count -ne 0) { throw 'PRODUCTION_RELEASE_FORBIDDEN_ARTIFACT_FOUND' }
    $apps = foreach ($name in $projects.Keys) {
        $appRoot = Join-Path $temporaryRoot $name
        $assemblyName = if ($name -eq 'api') { 'NeoSTP.Api' } elseif ($name -eq 'web') { 'NeoSTP.Web' } else { 'NeoSTP.Worker' }
        $exe = Join-Path $appRoot "$assemblyName.exe"
        $dll = Join-Path $appRoot "$assemblyName.dll"
        if (-not (Test-Path -LiteralPath $exe) -or -not (Test-Path -LiteralPath $dll)) { throw "PRODUCTION_RELEASE_BINARY_MISSING: $name" }
        [ordered]@{
            name = $name
            executable = "$name/$assemblyName.exe"
            executableSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash.ToLowerInvariant()
            assemblySha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash.ToLowerInvariant()
        }
    }
    $files = @(Get-ChildItem -LiteralPath $temporaryRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            path = [IO.Path]::GetRelativePath($temporaryRoot, $_.FullName).Replace('\', '/')
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
        }
    })
    $manifest = [ordered]@{
        schemaVersion = 1
        releaseVersion = $ReleaseVersion
        sourceCommit = $commit
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        environment = 'PRODUCTION'
        applications = @($apps)
        files = $files
        migration = [ordered]@{
            count = $migrationManifest.migrationCount
            current = $migrationManifest.currentMigration
            sqlSha256 = $migrationManifest.sqlSha256
            modelSnapshotSha256 = $migrationManifest.modelSnapshotSha256
        }
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $temporaryRoot 'release.manifest.json') -Encoding utf8NoBOM
    New-Item -ItemType Directory -Force -Path $releasesRoot | Out-Null
    if (Test-Path -LiteralPath $finalRoot) { throw 'PRODUCTION_RELEASE_CONCURRENT_PROMOTION' }
    try {
        [IO.Directory]::Move($temporaryRoot, $finalRoot)
    }
    catch [IO.IOException] {
        if (Test-Path -LiteralPath $finalRoot) { throw 'PRODUCTION_RELEASE_CONCURRENT_PROMOTION' }
        throw
    }
    [pscustomobject]@{
        Status = 'PREPARED'; ReleaseVersion = $ReleaseVersion; Commit = $commit; ReleaseRoot = $finalRoot
        Applications = 3; MigrationCount = $migrationManifest.migrationCount; CurrentMigration = $migrationManifest.currentMigration
        LocalSettingsExcluded = $true
    } | Format-List
}
catch {
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
    throw
}
