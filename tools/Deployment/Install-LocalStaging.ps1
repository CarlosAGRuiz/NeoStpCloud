#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$DataRoot = (Join-Path $env:LOCALAPPDATA 'NeoSTP\STAGING'),
    [string]$SqlAdminSettingsPath = 'src\NeoSTP.Api\appsettings.Local.json',
    [int]$ApiPort = 5158,
    [int]$WebPort = 5131,
    [switch]$SkipPublish,
    [switch]$SkipDatabase,
    [switch]$SkipTasks,
    [switch]$EnableWorker
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $IsWindows) { throw 'LOCAL_STAGING_REQUIRES_WINDOWS' }
if ($ApiPort -eq $WebPort -or $ApiPort -notin 1..65535 -or $WebPort -notin 1..65535) {
    throw 'LOCAL_STAGING_PORTS_INVALID'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$DataRoot = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\', '/')
if ((Split-Path $DataRoot -Leaf) -ne 'STAGING') { throw 'LOCAL_STAGING_DATA_ROOT_MUST_END_IN_STAGING' }
if ($DataRoot.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'LOCAL_STAGING_DATA_ROOT_MUST_BE_OUTSIDE_REPOSITORY'
}

$releasesRoot = [IO.Path]::GetFullPath((Join-Path $DataRoot 'releases')).TrimEnd('\', '/')
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$commit = (& git -C $repoRoot rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{12}$') { throw 'LOCAL_STAGING_GIT_COMMIT_UNAVAILABLE' }

$releaseRoot = Join-Path $DataRoot "releases\$commit"
$directories = @(
    $DataRoot,
    (Join-Path $DataRoot 'DataProtection'),
    (Join-Path $DataRoot 'logs'),
    (Join-Path $DataRoot 'backups'),
    (Join-Path $DataRoot 'secrets'),
    (Join-Path $DataRoot 'bin'),
    $releaseRoot
)
foreach ($directory in $directories) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }

# La raÃƒÂ­z pertenece ÃƒÂºnicamente a la identidad de STAGING actual y SYSTEM.
# Los hijos heredan esas ACE; no se aplican flags de directorio directamente a archivos.
& icacls $DataRoot /inheritance:r /grant:r "${identity}:(OI)(CI)F" 'SYSTEM:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'LOCAL_STAGING_ACL_CONFIGURATION_FAILED' }
& icacls "$DataRoot\*" /reset /T /C | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'LOCAL_STAGING_CHILD_ACL_CONFIGURATION_FAILED' }

function Protect-Text([string]$plainText) {
    ConvertTo-SecureString $plainText -AsPlainText -Force | ConvertFrom-SecureString
}

function Unprotect-Text([string]$cipherText) {
    $secure = ConvertTo-SecureString $cipherText
    [Net.NetworkCredential]::new('', $secure).Password
}

function New-RandomText([int]$bytes) {
    [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes($bytes))
}

$secretsPath = Join-Path $DataRoot 'secrets\staging.secrets.json'
if (Test-Path -LiteralPath $secretsPath) {
    $secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
} else {
    $secrets = [ordered]@{
        Username = 'staging.admin'
        Email = 'staging.admin@neostp.local'
        AdminPassword = Protect-Text ("Ns1!" + (New-RandomText 30))
        JwtKey = Protect-Text (New-RandomText 64)
        CertificateBackupPassword = Protect-Text (New-RandomText 32)
        DatabasePassword = Protect-Text ("Ns1!" + (New-RandomText 30))
    }
    $secrets | ConvertTo-Json | Set-Content -LiteralPath $secretsPath -Encoding utf8NoBOM
}

$secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
if ($secrets.PSObject.Properties.Name -notcontains 'DatabasePassword') {
    $secrets | Add-Member -NotePropertyName DatabasePassword -NotePropertyValue (Protect-Text ("Ns1!" + (New-RandomText 30)))
    $secrets | ConvertTo-Json | Set-Content -LiteralPath $secretsPath -Encoding utf8NoBOM
}

foreach ($name in 'Username', 'Email', 'AdminPassword', 'JwtKey', 'CertificateBackupPassword', 'DatabasePassword') {
    if ([string]::IsNullOrWhiteSpace($secrets.$name)) { throw "LOCAL_STAGING_SECRET_INVALID: $name" }
}

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.FriendlyName -eq 'NeoSTP STAGING Data Protection' -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1
if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Subject 'CN=NeoSTP STAGING Data Protection' `
        -FriendlyName 'NeoSTP STAGING Data Protection' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -KeyExportPolicy Exportable `
        -KeyUsage KeyEncipherment, DataEncipherment `
        -NotAfter (Get-Date).AddYears(5)
}
if (-not $certificate.HasPrivateKey) { throw 'LOCAL_STAGING_DATA_PROTECTION_CERTIFICATE_INVALID' }

$certificateBackup = Join-Path $DataRoot 'backups\neostp-staging-dataprotection.pfx'
if (-not (Test-Path -LiteralPath $certificateBackup)) {
    $backupPassword = ConvertTo-SecureString (Unprotect-Text $secrets.CertificateBackupPassword) -AsPlainText -Force
    Export-PfxCertificate -Cert $certificate -FilePath $certificateBackup -Password $backupPassword | Out-Null
}

$runtimeBuilder = $null
if (-not $SkipDatabase) {
    $adminSettings = if ([IO.Path]::IsPathFullyQualified($SqlAdminSettingsPath)) {
        $SqlAdminSettingsPath
    } else {
        Join-Path $repoRoot $SqlAdminSettingsPath
    }
    if (-not (Test-Path -LiteralPath $adminSettings)) { throw 'LOCAL_STAGING_SQL_ADMIN_SETTINGS_NOT_FOUND' }
    $adminConfig = Get-Content -LiteralPath $adminSettings -Raw | ConvertFrom-Json
    $adminConnectionString = $adminConfig.ConnectionStrings.NeoStpDb
    if ([string]::IsNullOrWhiteSpace($adminConnectionString)) { throw 'LOCAL_STAGING_SQL_ADMIN_CONNECTION_MISSING' }

    $adminBuilder = [Data.SqlClient.SqlConnectionStringBuilder]::new($adminConnectionString)
    $masterBuilder = [Data.SqlClient.SqlConnectionStringBuilder]::new($adminConnectionString)
    $masterBuilder['Initial Catalog'] = 'master'
    $adminConnection = [Data.SqlClient.SqlConnection]::new($masterBuilder.ConnectionString)
    try {
        $adminConnection.Open()
        $command = $adminConnection.CreateCommand()
        $command.CommandText = "IF DB_ID(N'NeoSTP_Staging') IS NULL CREATE DATABASE [NeoSTP_Staging];"
        [void]$command.ExecuteNonQuery()
    } finally {
        $adminConnection.Dispose()
    }

    $migrationVersion = "staging-$commit"
    & (Join-Path $repoRoot 'tools\Database\New-MigrationRelease.ps1') -ReleaseVersion $migrationVersion
    if ($LASTEXITCODE -ne 0) { throw 'LOCAL_STAGING_MIGRATION_RELEASE_FAILED' }
    $migrationManifestPath = Join-Path $repoRoot "artifacts\migrations\$migrationVersion\manifest.json"
    $migrationManifest = Get-Content -LiteralPath $migrationManifestPath -Raw | ConvertFrom-Json
    if ($migrationManifest.sourceTreeDirty -ne $false) { throw 'LOCAL_STAGING_DIRTY_SOURCE_NOT_ALLOWED' }
    $migrationScript = Join-Path $repoRoot "artifacts\migrations\$migrationVersion\NeoSTP.Migrations.idempotent.sql"
    if (-not (Test-Path -LiteralPath $migrationScript)) { throw 'LOCAL_STAGING_MIGRATION_SCRIPT_NOT_FOUND' }

    $sqlcmdArgs = @('-S', $adminBuilder.DataSource, '-d', 'NeoSTP_Staging', '-b', '-m', '1', '-C', '-i', $migrationScript)
    try {
        if ($adminBuilder.IntegratedSecurity) {
            $sqlcmdArgs += '-E'
        } else {
            $env:SQLCMDPASSWORD = $adminBuilder.Password
            $sqlcmdArgs += @('-U', $adminBuilder.UserID)
        }
        $sqlOutput = & sqlcmd @sqlcmdArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            $diagnostic = ($sqlOutput | Select-Object -Last 20) -join [Environment]::NewLine
            throw ('LOCAL_STAGING_MIGRATION_APPLY_FAILED' + [Environment]::NewLine + $diagnostic)
        }
    } finally {
        Remove-Item Env:\SQLCMDPASSWORD -ErrorAction SilentlyContinue
    }

    $appLogin = 'neostp_staging_app'
    $databasePassword = Unprotect-Text $secrets.DatabasePassword
    $escapedDatabasePassword = $databasePassword.Replace("'", "''")
    $grantSql = @"
IF SUSER_ID(N'neostp_staging_app') IS NULL
    CREATE LOGIN [neostp_staging_app] WITH PASSWORD = N'$escapedDatabasePassword', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [neostp_staging_app] WITH PASSWORD = N'$escapedDatabasePassword';
USE [NeoSTP_Staging];
IF USER_ID(N'neostp_staging_app') IS NULL CREATE USER [neostp_staging_app] FOR LOGIN [neostp_staging_app];
IF IS_ROLEMEMBER(N'db_datareader', N'neostp_staging_app') <> 1 ALTER ROLE [db_datareader] ADD MEMBER [neostp_staging_app];
IF IS_ROLEMEMBER(N'db_datawriter', N'neostp_staging_app') <> 1 ALTER ROLE [db_datawriter] ADD MEMBER [neostp_staging_app];
GRANT EXECUTE TO [neostp_staging_app];
"@
    $adminConnection = [Data.SqlClient.SqlConnection]::new($masterBuilder.ConnectionString)
    try {
        $adminConnection.Open()
        $command = $adminConnection.CreateCommand()
        $command.CommandText = $grantSql
        [void]$command.ExecuteNonQuery()
    } finally {
        $adminConnection.Dispose()
    }

    $runtimeBuilder = [Data.SqlClient.SqlConnectionStringBuilder]::new()
    $runtimeBuilder['Data Source'] = $adminBuilder.DataSource
    $runtimeBuilder['Initial Catalog'] = 'NeoSTP_Staging'
    $runtimeBuilder['User ID'] = $appLogin
    $runtimeBuilder['Password'] = $databasePassword
    $runtimeBuilder['Encrypt'] = $true
    $runtimeBuilder['TrustServerCertificate'] = $true
    $runtimeBuilder['MultipleActiveResultSets'] = $true
} else {
    $existingDeployment = Get-Content -LiteralPath (Join-Path $DataRoot 'deployment.json') -Raw | ConvertFrom-Json
    $runtimeBuilder = [Data.SqlClient.SqlConnectionStringBuilder]::new()
    $runtimeBuilder['Data Source'] = $existingDeployment.SqlServer
    $runtimeBuilder['Initial Catalog'] = $existingDeployment.Database
    $runtimeBuilder['User ID'] = $existingDeployment.SqlLogin
    $runtimeBuilder['Password'] = Unprotect-Text $secrets.DatabasePassword
    $runtimeBuilder['Encrypt'] = $true
    $runtimeBuilder['TrustServerCertificate'] = $true
    $runtimeBuilder['MultipleActiveResultSets'] = $true
}

if (-not $SkipPublish) {
    $projects = [ordered]@{
        api = 'src\NeoSTP.Api\NeoSTP.Api.csproj'
        web = 'src\NeoSTP.Web\NeoSTP.Web.csproj'
        worker = 'src\NeoSTP.Worker\NeoSTP.Worker.csproj'
    }
    foreach ($app in $projects.GetEnumerator()) {
        $output = Join-Path $releaseRoot $app.Key
        New-Item -ItemType Directory -Path $output -Force | Out-Null
        & dotnet publish (Join-Path $repoRoot $app.Value) -c Release -o $output --no-restore
        if ($LASTEXITCODE -ne 0) { throw "LOCAL_STAGING_PUBLISH_FAILED: $($app.Key)" }
    }
}

$launcherSource = Join-Path $PSScriptRoot 'Start-LocalStagingApp.ps1'
$launcher = Join-Path $DataRoot 'bin\Start-LocalStagingApp.ps1'
Copy-Item -LiteralPath $launcherSource -Destination $launcher -Force

$deployment = [ordered]@{
    EnvironmentId = 'STAGING'
    Commit = $commit
    ReleaseRoot = $releaseRoot
    DataProtectionThumbprint = $certificate.Thumbprint
    SqlServer = $runtimeBuilder.DataSource
    Database = 'NeoSTP_Staging'
    SqlLogin = 'neostp_staging_app'
    ApiPort = $ApiPort
    WebPort = $WebPort
    WorkerEnabled = $EnableWorker.IsPresent
    InstalledAtUtc = [DateTime]::UtcNow.ToString('O')
}
$deployment | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $DataRoot 'deployment.json') -Encoding utf8NoBOM

$env:ConnectionStrings__NeoStpDb = $runtimeBuilder.ConnectionString
$env:NEOSTP_STAGING_ADMIN_USERNAME = $secrets.Username
$env:NEOSTP_STAGING_ADMIN_EMAIL = $secrets.Email
$env:NEOSTP_STAGING_ADMIN_PASSWORD = Unprotect-Text $secrets.AdminPassword
try {
    & dotnet run --project (Join-Path $repoRoot 'tools\Deployment\NeoSTP.StagingBootstrap\NeoSTP.StagingBootstrap.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'LOCAL_STAGING_ADMIN_BOOTSTRAP_FAILED' }
} finally {
    Remove-Item Env:\ConnectionStrings__NeoStpDb -ErrorAction SilentlyContinue
    Remove-Item Env:\NEOSTP_STAGING_ADMIN_USERNAME -ErrorAction SilentlyContinue
    Remove-Item Env:\NEOSTP_STAGING_ADMIN_EMAIL -ErrorAction SilentlyContinue
    Remove-Item Env:\NEOSTP_STAGING_ADMIN_PASSWORD -ErrorAction SilentlyContinue
}

if (-not $SkipTasks) {
    $taskPrefix = 'NeoSTP STAGING'
    foreach ($name in 'API', 'Web', 'Worker') {
        $existing = Get-ScheduledTask -TaskName "$taskPrefix $name" -ErrorAction SilentlyContinue
        if ($existing) { Stop-ScheduledTask -TaskName "$taskPrefix $name" -ErrorAction SilentlyContinue }
    }

    Get-Process -Name 'NeoSTP.Api', 'NeoSTP.Web', 'NeoSTP.Worker' -ErrorAction SilentlyContinue |
        ForEach-Object {
            $processPath = $_.Path
            if ($processPath -and
                $processPath.StartsWith($releasesRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
                Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            }
        }

    $portReleaseDeadline = (Get-Date).AddSeconds(30)
    while ((Get-NetTCPConnection -State Listen -LocalPort $ApiPort, $WebPort -ErrorAction SilentlyContinue) -and
           (Get-Date) -lt $portReleaseDeadline) {
        Start-Sleep -Milliseconds 250
    }
    if (Get-NetTCPConnection -State Listen -LocalPort $ApiPort, $WebPort -ErrorAction SilentlyContinue) {
        throw 'LOCAL_STAGING_PORT_STILL_IN_USE'
    }
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -RestartCount 999 `
        -RestartInterval (New-TimeSpan -Minutes 1) `
        -ExecutionTimeLimit (New-TimeSpan -Days 3650) `
        -MultipleInstances IgnoreNew
    $principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity
    $pwsh = (Get-Command pwsh).Source

    foreach ($name in 'Api', 'Web', 'Worker') {
        $arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$launcher`" -App $name -DataRoot `"$DataRoot`""
        $action = New-ScheduledTaskAction -Execute $pwsh -Argument $arguments -WorkingDirectory $releaseRoot
        $task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings
        Register-ScheduledTask -TaskName "$taskPrefix $name" -InputObject $task -Force | Out-Null
    }
    Start-ScheduledTask -TaskName "$taskPrefix API"
    Start-ScheduledTask -TaskName "$taskPrefix Web"
    if ($EnableWorker) {
        Enable-ScheduledTask -TaskName "$taskPrefix Worker" | Out-Null
        Start-ScheduledTask -TaskName "$taskPrefix Worker"
    } else {
        Disable-ScheduledTask -TaskName "$taskPrefix Worker" | Out-Null
    }
}

function Wait-Health([string]$hostName, [int]$port, [string]$path) {
    $deadline = (Get-Date).AddSeconds(120)
    do {
        try {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:$port$path" `
                -Headers @{ Host = $hostName; 'X-Forwarded-Proto' = 'https' } `
                -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    throw "LOCAL_STAGING_HEALTH_TIMEOUT: $hostName$path"
}

function Assert-ListenerRelease([int]$port) {
    $listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction Stop | Select-Object -First 1
    $process = Get-Process -Id $listener.OwningProcess -ErrorAction Stop
    $actualPath = [IO.Path]::GetFullPath($process.Path)
    $expectedPrefix = [IO.Path]::GetFullPath($releaseRoot).TrimEnd('\', '/') + '\'
    if (-not $actualPath.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "LOCAL_STAGING_LISTENER_RELEASE_MISMATCH: $port"
    }
}

if (-not $SkipTasks) {
    Wait-Health 'staging-api.neostp.com' $ApiPort '/health/live'
    Wait-Health 'staging-api.neostp.com' $ApiPort '/health/ready'
    Wait-Health 'staging.neostp.com' $WebPort '/health/live'
    Wait-Health 'staging.neostp.com' $WebPort '/health/ready'
    Assert-ListenerRelease $ApiPort
    Assert-ListenerRelease $WebPort

    foreach ($name in 'API', 'Web') {
        if ((Get-ScheduledTask -TaskName "NeoSTP STAGING $name").State -ne 'Running') {
            throw "LOCAL_STAGING_TASK_NOT_RUNNING: $name"
        }
    }
    if (-not $EnableWorker -and (Get-ScheduledTask -TaskName 'NeoSTP STAGING Worker').State -ne 'Disabled') {
        throw 'LOCAL_STAGING_WORKER_NOT_DISABLED'
    }

    $loginBody = @{ usernameOrEmail = $secrets.Username; password = Unprotect-Text $secrets.AdminPassword } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/api/auth/login" -Method Post `
        -Headers @{ Host = 'staging-api.neostp.com'; 'X-Forwarded-Proto' = 'https' } `
        -ContentType 'application/json' -Body $loginBody -TimeoutSec 15
    if ([string]::IsNullOrWhiteSpace($login.data.accessToken)) { throw 'LOCAL_STAGING_LOGIN_SMOKE_FAILED' }
}

[pscustomobject]@{
    Environment = 'STAGING'
    Commit = $commit
    Database = 'NeoSTP_Staging'
    DataRoot = $DataRoot
    ApiOrigin = 'https://staging-api.neostp.com'
    ApiLoopbackPort = $ApiPort
    WebOrigin = 'https://staging.neostp.com'
    WebLoopbackPort = $WebPort
    ApiAndWebHealthy = -not $SkipTasks
    LoginSmoke = -not $SkipTasks
    WorkerEnabled = $EnableWorker.IsPresent
    CloudflareIngressPending = $true
} | Format-List
