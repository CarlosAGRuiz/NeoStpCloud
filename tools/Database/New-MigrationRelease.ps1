[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$')]
    [string]$ReleaseVersion,

    [string]$OutputRoot = 'artifacts/migrations',

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$migrationsPath = Join-Path $repoRoot 'src\NeoSTP.Infrastructure\Persistence\Migrations'
$snapshotPath = Join-Path $migrationsPath 'NeoStpDbContextModelSnapshot.cs'
$sourceManifestPath = Join-Path $repoRoot 'deploy\migrations\manifest.json'

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
    }
}

Push-Location $repoRoot
try {
    $migrationFiles = @(
        Get-ChildItem -LiteralPath $migrationsPath -File -Filter '*.cs' |
            Where-Object {
                $_.Name -notlike '*.Designer.cs' -and
                $_.Name -ne 'NeoStpDbContextModelSnapshot.cs'
            } |
            Sort-Object Name
    )

    if ($migrationFiles.Count -eq 0) {
        throw 'No EF migrations were found.'
    }

    $sourceManifest = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json
    $firstMigration = $migrationFiles[0].BaseName
    $currentMigration = $migrationFiles[-1].BaseName
    $snapshotSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $snapshotPath).Hash.ToLowerInvariant()

    $manifestErrors = @()
    if ($sourceManifest.firstMigration -ne $firstMigration) {
        $manifestErrors += "firstMigration must be $firstMigration"
    }
    if ($sourceManifest.currentMigration -ne $currentMigration) {
        $manifestErrors += "currentMigration must be $currentMigration"
    }
    if ([int]$sourceManifest.migrationCount -ne $migrationFiles.Count) {
        $manifestErrors += "migrationCount must be $($migrationFiles.Count)"
    }
    if ($sourceManifest.modelSnapshotSha256 -ne $snapshotSha256) {
        $manifestErrors += "modelSnapshotSha256 must be $snapshotSha256"
    }

    if ($manifestErrors.Count -gt 0) {
        throw "deploy/migrations/manifest.json is stale:`n - $($manifestErrors -join "`n - ")"
    }

    if (-not $SkipBuild) {
        Invoke-Checked dotnet @('tool', 'restore')
        Invoke-Checked dotnet @('restore', 'tools/SchemaDesign/SchemaDesign.csproj')
        Invoke-Checked dotnet @('build', 'tools/SchemaDesign/SchemaDesign.csproj', '-c', 'Release', '--no-restore')
    }

    $efArguments = @(
        '--project', 'src/NeoSTP.Infrastructure/NeoSTP.Infrastructure.csproj',
        '--startup-project', 'tools/SchemaDesign/SchemaDesign.csproj',
        '--configuration', 'Release',
        '--no-build',
        '--context', 'NeoStpDbContext',
        '--', '--offline-schema'
    )

    Invoke-Checked dotnet (@('tool', 'run', 'dotnet-ef', '--', 'migrations', 'has-pending-model-changes') + $efArguments)

    $resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
        $OutputRoot
    } else {
        Join-Path $repoRoot $OutputRoot
    }
    $releaseDirectory = Join-Path $resolvedOutputRoot $ReleaseVersion
    $null = New-Item -ItemType Directory -Force -Path $releaseDirectory

    $sqlPath = Join-Path $releaseDirectory 'NeoSTP.Migrations.idempotent.sql'
    Invoke-Checked dotnet (@(
        'tool', 'run', 'dotnet-ef', '--', 'migrations', 'script',
        '--idempotent',
        '--output', $sqlPath
    ) + $efArguments)

    # sqlcmd does not guarantee the SET options required by filtered indexes,
    # indexed views or indexes over computed columns. Make the release artifact
    # self-contained so reviewed SQL behaves consistently in every environment.
    $sessionPreamble = @'
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

'@
    $generatedSql = [System.IO.File]::ReadAllText($sqlPath)
    $scriptedMigrations = @(
        [regex]::Matches(
            $generatedSql,
            "VALUES \(N'(?<id>[0-9]{14}_[^']+)', N'[^']+'\);") |
            ForEach-Object { $_.Groups['id'].Value } |
            Sort-Object -Unique
    )
    $expectedMigrations = @($migrationFiles | ForEach-Object { $_.BaseName })
    if (@(Compare-Object -ReferenceObject $expectedMigrations -DifferenceObject $scriptedMigrations).Count -gt 0) {
        throw 'Generated SQL does not cover the migration manifest. Rebuild SchemaDesign before packaging.'
    }
    [System.IO.File]::WriteAllText(
        $sqlPath,
        $sessionPreamble + $generatedSql,
        [System.Text.UTF8Encoding]::new($false))

    $sourceCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to resolve the source Git commit.'
    }

    $sourceStatus = @(& git status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to resolve the source Git status.'
    }
    $sourceTreeDirty = $sourceStatus.Count -gt 0

    $artifactManifest = [ordered]@{
        schemaVersion = 1
        releaseVersion = $ReleaseVersion
        sourceCommit = $sourceCommit
        sourceTreeDirty = $sourceTreeDirty
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        dbContext = $sourceManifest.dbContext
        provider = $sourceManifest.provider
        firstMigration = $firstMigration
        currentMigration = $currentMigration
        migrationCount = $migrationFiles.Count
        modelSnapshotSha256 = $snapshotSha256
        sqlFile = [System.IO.Path]::GetFileName($sqlPath)
        sqlSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sqlPath).Hash.ToLowerInvariant()
    }

    $artifactManifestPath = Join-Path $releaseDirectory 'manifest.json'
    $artifactManifest | ConvertTo-Json | Set-Content -LiteralPath $artifactManifestPath -Encoding utf8

    Write-Output "Migration release artifact created at $releaseDirectory"
    Write-Output "Current migration: $currentMigration"
    Write-Output "SQL SHA-256: $($artifactManifest.sqlSha256)"
}
finally {
    Pop-Location
}
