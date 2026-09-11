[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Database,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [ValidateSet('FULL', 'DIFFERENTIAL', 'LOG')]
    [string]$BackupType = 'FULL',

    [string]$OffsiteDirectory,

    [string]$EvidencePath,

    [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]{0,127}$')]
    [string]$ConnectionEnvironmentVariable = 'NEOSTP_SQLSERVER_ADMIN_CONNECTION'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'NeoStp.DisasterRecovery.psm1') -Force

Assert-NeoStpSqlIdentifier -Value $Database
$connectionString = Get-NeoStpAdminConnectionString -EnvironmentVariable $ConnectionEnvironmentVariable
$outputRoot = Resolve-NeoStpAbsolutePath -Path $OutputDirectory -BasePath (Get-Location).Path
$null = New-Item -ItemType Directory -Force -Path $outputRoot

$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$extension = if ($BackupType -eq 'LOG') { 'trn' } else { 'bak' }
$backupFileName = "${Database}_${BackupType}_${timestamp}.${extension}"
$backupPath = Join-Path $outputRoot $backupFileName
$manifestPath = $backupPath + '.manifest.json'
$databaseIdentifier = ConvertTo-NeoStpSqlIdentifier $Database
$backupLiteral = ConvertTo-NeoStpSqlLiteral $backupPath
$startedAt = [DateTimeOffset]::UtcNow
$connection = $null

if ((Test-Path -LiteralPath $backupPath) -or (Test-Path -LiteralPath $manifestPath)) {
    throw "Refusing to overwrite an existing local backup or manifest: $backupFileName"
}

try {
    $connection = Open-NeoStpSqlConnection $connectionString
    $databaseLiteral = ConvertTo-NeoStpSqlLiteral $Database
    $databaseInfo = Invoke-NeoStpSqlTable -Connection $connection -Sql @"
SELECT [name], [state_desc], [recovery_model_desc],
       CAST(SERVERPROPERTY('Edition') AS nvarchar(256)) AS [edition]
FROM sys.databases
WHERE [name] = $databaseLiteral;
"@
    if ($databaseInfo.Rows.Count -ne 1) {
        throw "Database '$Database' does not exist on the configured SQL Server instance."
    }
    if ([string]$databaseInfo.Rows[0].state_desc -ne 'ONLINE') {
        throw "Database '$Database' must be ONLINE before backup."
    }
    if ($BackupType -eq 'LOG' -and [string]$databaseInfo.Rows[0].recovery_model_desc -ne 'FULL') {
        throw "LOG backup requires database '$Database' to use the FULL recovery model."
    }

    $supportsCompression = -not ([string]$databaseInfo.Rows[0].edition).Contains('Express')
    $commonOptions = if ($supportsCompression) { 'CHECKSUM, COMPRESSION, STATS = 10' } else { 'CHECKSUM, STATS = 10' }
    $options = switch ($BackupType) {
        'FULL' { $commonOptions }
        'DIFFERENTIAL' { 'DIFFERENTIAL, ' + $commonOptions }
        'LOG' { $commonOptions }
    }
    $statement = if ($BackupType -eq 'LOG') {
        "BACKUP LOG $databaseIdentifier TO DISK = $backupLiteral WITH $options;"
    } else {
        "BACKUP DATABASE $databaseIdentifier TO DISK = $backupLiteral WITH $options;"
    }

    $null = Invoke-NeoStpSqlNonQuery -Connection $connection -Sql $statement
    $null = Invoke-NeoStpSqlNonQuery -Connection $connection -Sql "RESTORE VERIFYONLY FROM DISK = $backupLiteral WITH CHECKSUM;"
}
finally {
    if ($null -ne $connection) {
        $connection.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    throw "SQL Server reported success but the backup file is not visible at '$backupPath'."
}

$backupFile = Get-Item -LiteralPath $backupPath
$sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupPath).Hash.ToLowerInvariant()
$offsite = [ordered]@{ copied = $false }

if (-not [string]::IsNullOrWhiteSpace($OffsiteDirectory)) {
    $offsiteRoot = Resolve-NeoStpAbsolutePath -Path $OffsiteDirectory -BasePath (Get-Location).Path
    if ([string]::Equals($offsiteRoot.TrimEnd('\'), $outputRoot.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OffsiteDirectory must be different from OutputDirectory.'
    }

    $null = New-Item -ItemType Directory -Force -Path $offsiteRoot
    $offsitePath = Join-Path $offsiteRoot $backupFileName
    $offsiteManifestPath = $offsitePath + '.manifest.json'
    if ((Test-Path -LiteralPath $offsitePath) -or (Test-Path -LiteralPath $offsiteManifestPath)) {
        throw "Refusing to overwrite an existing off-site artifact: $backupFileName"
    }
    $partialPath = $offsitePath + '.partial'
    Copy-Item -LiteralPath $backupPath -Destination $partialPath
    $offsiteSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $partialPath).Hash.ToLowerInvariant()
    if ($offsiteSha256 -ne $sha256) {
        Remove-Item -LiteralPath $partialPath -Force
        throw 'Off-site copy checksum does not match the source backup.'
    }
    Move-Item -LiteralPath $partialPath -Destination $offsitePath
    $offsite = [ordered]@{
        copied = $true
        fileName = $backupFileName
        sha256 = $offsiteSha256
    }
}

$evidence = [ordered]@{
    schemaVersion = 1
    operation = 'physical-sql-backup'
    status = 'passed'
    database = $Database
    backupType = $BackupType
    createdAtUtc = $startedAt.ToString('O')
    completedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    fileName = $backupFileName
    sizeBytes = $backupFile.Length
    sha256 = $sha256
    sqlVerifyOnly = $true
    compressionUsed = $supportsCompression
    offsite = $offsite
}
Write-NeoStpJsonEvidence -Value $evidence -Path $manifestPath

if ($offsite.copied) {
    $partialManifestPath = $offsiteManifestPath + '.partial'
    Copy-Item -LiteralPath $manifestPath -Destination $partialManifestPath
    $manifestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash.ToLowerInvariant()
    $offsiteManifestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $partialManifestPath).Hash.ToLowerInvariant()
    if ($manifestSha256 -ne $offsiteManifestSha256) {
        Remove-Item -LiteralPath $partialManifestPath -Force
        throw 'Off-site manifest checksum does not match the local manifest.'
    }
    Move-Item -LiteralPath $partialManifestPath -Destination $offsiteManifestPath
}

if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
    $resolvedEvidence = Resolve-NeoStpAbsolutePath -Path $EvidencePath -BasePath (Get-Location).Path
    Write-NeoStpJsonEvidence -Value $evidence -Path $resolvedEvidence
}

Write-Output "Physical backup passed: $backupFileName"
Write-Output "Bytes: $($backupFile.Length)"
Write-Output "SHA-256: $sha256"
Write-Output "Manifest: $manifestPath"
