[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BackupPath,

    [string]$TargetDatabase,

    [string]$EvidencePath = 'artifacts/dr/restore-drill.json',

    [switch]$KeepRestoredDatabase,

    [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]{0,127}$')]
    [string]$ConnectionEnvironmentVariable = 'NEOSTP_SQLSERVER_ADMIN_CONNECTION'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'NeoStp.DisasterRecovery.psm1') -Force

$resolvedBackupPath = Resolve-NeoStpAbsolutePath -Path $BackupPath -BasePath (Get-Location).Path
if (-not (Test-Path -LiteralPath $resolvedBackupPath -PathType Leaf)) {
    throw "Backup file does not exist: $resolvedBackupPath"
}

if ([string]::IsNullOrWhiteSpace($TargetDatabase)) {
    $suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $TargetDatabase = 'NeoSTP_Drill_' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss') + '_' + $suffix
}
Assert-NeoStpSqlIdentifier -Value $TargetDatabase -ParameterName 'TargetDatabase'
if (-not $TargetDatabase.StartsWith('NeoSTP_Drill_', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'TargetDatabase must start with NeoSTP_Drill_. Only isolated drill databases are allowed.'
}

$connectionString = Get-NeoStpAdminConnectionString -EnvironmentVariable $ConnectionEnvironmentVariable
$evidenceFile = Resolve-NeoStpAbsolutePath -Path $EvidencePath -BasePath (Get-Location).Path
$backupLiteral = ConvertTo-NeoStpSqlLiteral $resolvedBackupPath
$targetIdentifier = ConvertTo-NeoStpSqlIdentifier $TargetDatabase
$targetLiteral = ConvertTo-NeoStpSqlLiteral $TargetDatabase
$backupSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedBackupPath).Hash.ToLowerInvariant()
$startedAt = [DateTimeOffset]::UtcNow
$timer = [Diagnostics.Stopwatch]::StartNew()
$connection = $null
$passed = $false
$failure = $null
$userTableCount = $null
$cleanupVerified = $false
$targetWasAbsent = $false
$verifyOnlyPassed = $false
$checkDbPassed = $false

try {
    $connection = Open-NeoStpSqlConnection $connectionString
    $existing = Invoke-NeoStpSqlTable -Connection $connection -Sql "SELECT DB_ID($targetLiteral) AS database_id;"
    if ($existing.Rows[0].database_id -isnot [DBNull]) {
        throw "Target drill database '$TargetDatabase' already exists; refusing to overwrite it."
    }

    $targetWasAbsent = $true
    $null = Invoke-NeoStpSqlNonQuery -Connection $connection -Sql "RESTORE VERIFYONLY FROM DISK = $backupLiteral WITH CHECKSUM;"
    $verifyOnlyPassed = $true
    $files = Invoke-NeoStpSqlTable -Connection $connection -Sql "RESTORE FILELISTONLY FROM DISK = $backupLiteral;"
    if ($files.Rows.Count -eq 0) {
        throw 'RESTORE FILELISTONLY returned no database files.'
    }

    $paths = Invoke-NeoStpSqlTable -Connection $connection -Sql @"
SELECT
    CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) AS data_path,
    CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)) AS log_path;
"@
    $dataPath = [string]$paths.Rows[0].data_path
    $logPath = [string]$paths.Rows[0].log_path
    if ([string]::IsNullOrWhiteSpace($dataPath) -or [string]::IsNullOrWhiteSpace($logPath)) {
        throw 'SQL Server did not report default data and log paths.'
    }

    $moves = @()
    foreach ($file in $files.Rows) {
        $type = [string]$file.Type
        if ($type -notin @('D', 'L')) {
            throw "Unsupported backup file type '$type' in restore drill."
        }
        $logicalName = ConvertTo-NeoStpSqlLiteral ([string]$file.LogicalName)
        $extension = if ($type -eq 'L') { '.ldf' } elseif ($moves.Count -eq 0) { '.mdf' } else { '.ndf' }
        $root = if ($type -eq 'L') { $logPath } else { $dataPath }
        $physicalPath = Join-Path $root ($TargetDatabase + '_' + [string]$file.FileId + $extension)
        $moves += 'MOVE ' + $logicalName + ' TO ' + (ConvertTo-NeoStpSqlLiteral $physicalPath)
    }

    $restoreSql = "RESTORE DATABASE $targetIdentifier FROM DISK = $backupLiteral WITH RECOVERY, CHECKSUM, STATS = 10, " + ($moves -join ', ') + ';'
    $null = Invoke-NeoStpSqlNonQuery -Connection $connection -Sql $restoreSql
    $null = Invoke-NeoStpSqlNonQuery -Connection $connection -Sql "DBCC CHECKDB ($targetLiteral) WITH NO_INFOMSGS, ALL_ERRORMSGS;"
    $checkDbPassed = $true
    $verification = Invoke-NeoStpSqlTable -Connection $connection -Sql @"
SELECT
    d.[state_desc],
    d.[user_access_desc],
    (SELECT COUNT(*) FROM $targetIdentifier.sys.tables WHERE [is_ms_shipped] = 0) AS user_table_count
FROM sys.databases d
WHERE d.[name] = $targetLiteral;
"@
    if ($verification.Rows.Count -ne 1 -or [string]$verification.Rows[0].state_desc -ne 'ONLINE') {
        throw 'Restored drill database is not ONLINE.'
    }
    $userTableCount = [int]$verification.Rows[0].user_table_count
    $passed = $true
}
catch {
    $failure = $_.Exception.Message
}
finally {
    if ($null -ne $connection) {
        if ($targetWasAbsent) {
            try {
                $current = Invoke-NeoStpSqlTable -Connection $connection -Sql "SELECT DB_ID($targetLiteral) AS database_id;"
                $databaseExists = $current.Rows[0].database_id -isnot [DBNull]
                if ($databaseExists -and -not $KeepRestoredDatabase) {
                    $null = Invoke-NeoStpSqlNonQuery -Connection $connection -Sql "ALTER DATABASE $targetIdentifier SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $targetIdentifier;"
                    $current = Invoke-NeoStpSqlTable -Connection $connection -Sql "SELECT DB_ID($targetLiteral) AS database_id;"
                    $databaseExists = $current.Rows[0].database_id -isnot [DBNull]
                }
                $cleanupVerified = -not $databaseExists
            }
            catch {
                $passed = $false
                $failure = "Cleanup failed: $($_.Exception.Message)"
            }
        }
        $connection.Dispose()
    }
    $timer.Stop()
}

$evidence = [ordered]@{
    schemaVersion = 1
    operation = 'isolated-sql-restore-drill'
    status = if ($passed) { 'passed' } else { 'failed' }
    startedAtUtc = $startedAt.ToString('O')
    completedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    durationSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3)
    sourceBackupFile = [System.IO.Path]::GetFileName($resolvedBackupPath)
    sourceBackupSha256 = $backupSha256
    targetDatabase = $TargetDatabase
    sqlVerifyOnly = $verifyOnlyPassed
    checkDb = $checkDbPassed
    userTableCount = $userTableCount
    databaseRetained = [bool]$KeepRestoredDatabase
    cleanupVerified = $cleanupVerified
    error = if ($passed) { $null } else { 'restore_failed' }
}
Write-NeoStpJsonEvidence -Value $evidence -Path $evidenceFile

if (-not $passed) {
    throw "Restore drill failed. Sanitized evidence: $evidenceFile. Error: $failure"
}

Write-Output "Restore drill passed: $TargetDatabase"
Write-Output "Duration seconds: $($evidence.durationSeconds)"
Write-Output "User tables: $userTableCount"
Write-Output "Cleanup verified: $cleanupVerified"
Write-Output "Sanitized evidence: $evidenceFile"
