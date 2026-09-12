#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ReleaseRoot,
    [string]$Database = 'NeoSTP_Production',
    [string]$SqlAdminSettingsPath = (Join-Path $env:LOCALAPPDATA 'NeoSTP\PRODUCTION\sql-admin.json'),
    [switch]$SkipDatabase
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $IsWindows) { throw 'PRODUCTION_RELEASE_VERIFY_REQUIRES_WINDOWS' }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$releaseRootFull = (Resolve-Path -LiteralPath $ReleaseRoot).Path.TrimEnd('\', '/')
$releaseParent = Split-Path $releaseRootFull -Parent
$releasesRoot = Split-Path $releaseParent -Parent

if ((Split-Path $releaseParent -Leaf) -ne 'releases' -or (Split-Path $releasesRoot -Leaf) -ne 'PRODUCTION') {
    throw 'PRODUCTION_RELEASE_PATH_INVALID'
}
if ($releaseRootFull.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'PRODUCTION_RELEASE_MUST_BE_OUTSIDE_REPOSITORY'
}

$manifestPath = Join-Path $releaseRootFull 'release.manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'PRODUCTION_RELEASE_MANIFEST_MISSING' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.environment -ne 'PRODUCTION' -or $manifest.sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'PRODUCTION_RELEASE_MANIFEST_INVALID'
}

$forbidden = @(Get-ChildItem -LiteralPath $releaseRootFull -Recurse -File | Where-Object {
    $_.Name -match '(?i)^appsettings\.Local.*\.json$|^secrets\.json$|^\.env(?:\..*)?$|\.(?:pfx|p12|p8|key|bak|mdf|ldf|log)$'
})
if ($forbidden.Count -ne 0) { throw 'PRODUCTION_RELEASE_FORBIDDEN_ARTIFACT_FOUND' }

$expectedApps = 'api,web,worker'
$actualApps = (@($manifest.applications | ForEach-Object { [string]$_.name }) | Sort-Object) -join ','
if ($actualApps -ne $expectedApps) { throw 'PRODUCTION_RELEASE_APPLICATION_SET_INVALID' }

$manifestFiles = @($manifest.files)
$actualFiles = @(Get-ChildItem -LiteralPath $releaseRootFull -Recurse -File |
    Where-Object Name -ne 'release.manifest.json' |
    ForEach-Object { [IO.Path]::GetRelativePath($releaseRootFull, $_.FullName).Replace('\', '/') })
if ($manifestFiles.Count -ne $actualFiles.Count) { throw 'PRODUCTION_RELEASE_FILE_INVENTORY_MISMATCH' }

$fileHashes = @{}
foreach ($entry in $manifestFiles) {
    $path = [string]$entry.path
    if ([string]::IsNullOrWhiteSpace($path) -or $fileHashes.ContainsKey($path)) {
        throw 'PRODUCTION_RELEASE_FILE_MANIFEST_INVALID'
    }
    $fileHashes[$path] = [string]$entry.sha256
}
foreach ($path in $actualFiles) {
    if (-not $fileHashes.ContainsKey($path)) { throw 'PRODUCTION_RELEASE_FILE_INVENTORY_MISMATCH' }
    $fullPath = Join-Path $releaseRootFull $path.Replace('/', '\')
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant() -ne $fileHashes[$path]) {
        throw "PRODUCTION_RELEASE_FILE_HASH_MISMATCH: $path"
    }
}

foreach ($app in $manifest.applications) {
    if ($app.name -notin @('api', 'web', 'worker')) { throw 'PRODUCTION_RELEASE_APPLICATION_INVALID' }
    $executable = Join-Path $releaseRootFull ([string]$app.executable).Replace('/', '\')
    $assemblyName = if ($app.name -eq 'api') { 'NeoSTP.Api.dll' } elseif ($app.name -eq 'web') { 'NeoSTP.Web.dll' } else { 'NeoSTP.Worker.dll' }
    $assembly = Join-Path (Join-Path $releaseRootFull $app.name) $assemblyName
    if (-not (Test-Path -LiteralPath $executable) -or -not (Test-Path -LiteralPath $assembly)) {
        throw "PRODUCTION_RELEASE_BINARY_MISSING: $($app.name)"
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $executable).Hash.ToLowerInvariant() -ne $app.executableSha256) {
        throw "PRODUCTION_RELEASE_EXECUTABLE_HASH_MISMATCH: $($app.name)"
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $assembly).Hash.ToLowerInvariant() -ne $app.assemblySha256) {
        throw "PRODUCTION_RELEASE_ASSEMBLY_HASH_MISMATCH: $($app.name)"
    }
}

$migrationSql = Join-Path $releaseRootFull 'database\NeoSTP.Migrations.idempotent.sql'
$migrationManifestPath = Join-Path $releaseRootFull 'database\manifest.json'
if (-not (Test-Path -LiteralPath $migrationSql) -or -not (Test-Path -LiteralPath $migrationManifestPath)) {
    throw 'PRODUCTION_RELEASE_MIGRATION_ARTIFACT_MISSING'
}
$migrationManifest = Get-Content -LiteralPath $migrationManifestPath -Raw | ConvertFrom-Json
$sqlHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $migrationSql).Hash.ToLowerInvariant()
if ($sqlHash -ne $manifest.migration.sqlSha256 -or $sqlHash -ne $migrationManifest.sqlSha256) {
    throw 'PRODUCTION_RELEASE_MIGRATION_HASH_MISMATCH'
}
if ($manifest.migration.count -ne $migrationManifest.migrationCount -or
    $manifest.migration.current -ne $migrationManifest.currentMigration) {
    throw 'PRODUCTION_RELEASE_MIGRATION_MANIFEST_MISMATCH'
}

$databaseVerified = $false
if (-not $SkipDatabase) {
    if ($Database -ne 'NeoSTP_Production') { throw 'PRODUCTION_DATABASE_NAME_INVALID' }
    if (-not (Test-Path -LiteralPath $SqlAdminSettingsPath)) { throw 'PRODUCTION_SQL_ADMIN_SETTINGS_MISSING' }

    Add-Type -AssemblyName System.Data
    $settings = Get-Content -LiteralPath $SqlAdminSettingsPath -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$settings.connectionString)) { throw 'PRODUCTION_SQL_ADMIN_CONNECTION_MISSING' }
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$settings.connectionString)
    $builder.InitialCatalog = $Database
    $connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT [MigrationId] FROM [__EFMigrationsHistory] ORDER BY [MigrationId]'
        $reader = $command.ExecuteReader()
        $applied = [Collections.Generic.List[string]]::new()
        while ($reader.Read()) { $applied.Add($reader.GetString(0)) }
        $reader.Close()
    }
    finally {
        $connection.Dispose()
    }

    if ($applied.Count -ne [int]$manifest.migration.count -or $applied[$applied.Count - 1] -ne $manifest.migration.current) {
        throw 'PRODUCTION_DATABASE_MIGRATIONS_MISMATCH'
    }
    $databaseVerified = $true
}

[pscustomobject]@{
    Status = 'VERIFIED'
    ReleaseVersion = $manifest.releaseVersion
    Commit = $manifest.sourceCommit
    Applications = @($manifest.applications).Count
    MigrationCount = $manifest.migration.count
    CurrentMigration = $manifest.migration.current
    DatabaseVerified = $databaseVerified
} | Format-List
