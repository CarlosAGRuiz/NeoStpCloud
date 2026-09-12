#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$DataRoot = (Join-Path $env:LOCALAPPDATA 'NeoSTP\STAGING'),
    [string]$TunnelName = 'neostp-staging-local',
    [string]$WebHostname = 'staging.neostp.com',
    [string]$ApiHostname = 'staging-api.neostp.com',
    [int]$WebPort = 5131,
    [int]$ApiPort = 5158,
    [string]$PublicDnsServer = '1.1.1.1'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $IsWindows) { throw 'LOCAL_STAGING_TUNNEL_REQUIRES_WINDOWS' }
if (-not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
    throw 'LOCAL_STAGING_CLOUDFLARED_NOT_FOUND'
}
if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) {
    throw 'LOCAL_STAGING_CURL_NOT_FOUND'
}
if ($WebPort -eq $ApiPort -or $WebPort -notin 1..65535 -or $ApiPort -notin 1..65535) {
    throw 'LOCAL_STAGING_TUNNEL_PORTS_INVALID'
}
foreach ($hostname in $WebHostname, $ApiHostname) {
    if ($hostname -notmatch '^[a-z0-9](?:[a-z0-9.-]{0,251}[a-z0-9])?$') {
        throw "LOCAL_STAGING_HOSTNAME_INVALID: $hostname"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$DataRoot = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\', '/')
if ((Split-Path $DataRoot -Leaf) -ne 'STAGING') { throw 'LOCAL_STAGING_DATA_ROOT_MUST_END_IN_STAGING' }
if ($DataRoot.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'LOCAL_STAGING_DATA_ROOT_MUST_BE_OUTSIDE_REPOSITORY'
}

$deploymentPath = Join-Path $DataRoot 'deployment.json'
if (-not (Test-Path -LiteralPath $deploymentPath)) { throw 'LOCAL_STAGING_DEPLOYMENT_NOT_FOUND' }
$deployment = Get-Content -LiteralPath $deploymentPath -Raw | ConvertFrom-Json
if ($deployment.EnvironmentId -ne 'STAGING' -or $deployment.Database -ne 'NeoSTP_Staging') {
    throw 'LOCAL_STAGING_DEPLOYMENT_INVALID'
}
if ([int]$deployment.WebPort -ne $WebPort -or [int]$deployment.ApiPort -ne $ApiPort) {
    throw 'LOCAL_STAGING_TUNNEL_PORT_MISMATCH'
}

$originCertificate = Join-Path $env:USERPROFILE '.cloudflared\cert.pem'
if (-not (Test-Path -LiteralPath $originCertificate)) {
    throw 'LOCAL_STAGING_CLOUDFLARE_ORIGIN_CERTIFICATE_NOT_FOUND'
}

$cloudflareRoot = Join-Path $DataRoot 'cloudflare'
$secretsRoot = Join-Path $DataRoot 'secrets'
$logsRoot = Join-Path $DataRoot 'logs'
foreach ($directory in $cloudflareRoot, $secretsRoot, $logsRoot) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$listOutput = & cloudflared tunnel --origincert $originCertificate list --output json
if ($LASTEXITCODE -ne 0) { throw 'LOCAL_STAGING_TUNNEL_LIST_FAILED' }
$matchingTunnels = @($listOutput | ConvertFrom-Json | Where-Object { $_.name -eq $TunnelName })
if ($matchingTunnels.Count -gt 1) { throw 'LOCAL_STAGING_TUNNEL_NAME_NOT_UNIQUE' }

if ($matchingTunnels.Count -eq 0) {
    $temporaryCredentialPath = Join-Path $secretsRoot 'cloudflared-new.json'
    $createOutput = & cloudflared tunnel --origincert $originCertificate create `
        --output json `
        --credentials-file $temporaryCredentialPath `
        $TunnelName
    if ($LASTEXITCODE -ne 0) { throw 'LOCAL_STAGING_TUNNEL_CREATE_FAILED' }
    $createdTunnel = $createOutput | ConvertFrom-Json
    $tunnelId = [guid]::Parse([string]$createdTunnel.id)
    $credentialPath = Join-Path $secretsRoot "cloudflared-$tunnelId.json"
    Move-Item -LiteralPath $temporaryCredentialPath -Destination $credentialPath -Force
} else {
    $tunnelId = [guid]::Parse([string]$matchingTunnels[0].id)
    $credentialPath = Join-Path $secretsRoot "cloudflared-$tunnelId.json"
    if (-not (Test-Path -LiteralPath $credentialPath)) {
        $defaultCredentialPath = Join-Path $env:USERPROFILE ".cloudflared\$tunnelId.json"
        if (-not (Test-Path -LiteralPath $defaultCredentialPath)) {
            throw 'LOCAL_STAGING_TUNNEL_CREDENTIAL_NOT_FOUND'
        }
        Copy-Item -LiteralPath $defaultCredentialPath -Destination $credentialPath
    }
}

$credentialYaml = $credentialPath.Replace('\', '/')
$logYaml = (Join-Path $logsRoot 'cloudflared-staging.log').Replace('\', '/')
$configPath = Join-Path $cloudflareRoot 'config.yml'
$config = @"
tunnel: $tunnelId
credentials-file: $credentialYaml
metrics: 127.0.0.1:20242
no-autoupdate: true
loglevel: info
logfile: $logYaml
ingress:
  - hostname: $WebHostname
    service: http://127.0.0.1:$WebPort
    originRequest:
      httpHostHeader: $WebHostname
  - hostname: $ApiHostname
    service: http://127.0.0.1:$ApiPort
    originRequest:
      httpHostHeader: $ApiHostname
  - service: http_status:404
"@
[IO.File]::WriteAllText($configPath, $config, [Text.UTF8Encoding]::new($false))

& cloudflared tunnel --config $configPath ingress validate
if ($LASTEXITCODE -ne 0) { throw 'LOCAL_STAGING_TUNNEL_CONFIG_INVALID' }

foreach ($hostname in $WebHostname, $ApiHostname) {
    & cloudflared tunnel --origincert $originCertificate route dns --overwrite-dns $tunnelId $hostname
    if ($LASTEXITCODE -ne 0) { throw "LOCAL_STAGING_TUNNEL_DNS_FAILED: $hostname" }
}

$existingTask = Get-ScheduledTask -TaskName 'NeoSTP STAGING Tunnel' -ErrorAction SilentlyContinue
if ($existingTask) { Stop-ScheduledTask -TaskName 'NeoSTP STAGING Tunnel' -ErrorAction SilentlyContinue }
Get-CimInstance Win32_Process -Filter "Name = 'cloudflared.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.Contains($configPath, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

$identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$cloudflared = (Get-Command cloudflared).Source
$arguments = "tunnel --config `"$configPath`" --no-autoupdate run $tunnelId"
$action = New-ScheduledTaskAction -Execute $cloudflared -Argument $arguments -WorkingDirectory $cloudflareRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 999 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit (New-TimeSpan -Days 3650) `
    -MultipleInstances IgnoreNew
$task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings
Register-ScheduledTask -TaskName 'NeoSTP STAGING Tunnel' -InputObject $task -Force | Out-Null
Start-ScheduledTask -TaskName 'NeoSTP STAGING Tunnel'

function Test-PublicHealth([string]$hostname, [string]$url) {
    # Consultar DNS de forma explicita evita un falso negativo del cache local de Windows.
    # --resolve conserva SNI y la validacion TLS contra el hostname publico.
    $address = Resolve-DnsName $hostname -Type A -DnsOnly -Server $PublicDnsServer `
        -QuickTimeout -ErrorAction SilentlyContinue |
        Where-Object IPAddress |
        Select-Object -First 1 -ExpandProperty IPAddress
    if (-not $address) { return $null }

    $status = & curl.exe -sS -o NUL -w '%{http_code}' `
        --max-time 20 `
        --resolve "${hostname}:443:$address" `
        $url
    if ($LASTEXITCODE -eq 0 -and $status.Trim() -eq '200') { return 200 }
    return $null
}

$results = @{}
$targets = [ordered]@{
    Web = "https://$WebHostname/health/ready"
    Api = "https://$ApiHostname/health/ready"
}
foreach ($target in $targets.GetEnumerator()) {
    $deadline = (Get-Date).AddMinutes(3)
    do {
        Clear-DnsClientCache -ErrorAction SilentlyContinue
        $hostname = ([uri]$target.Value).Host
        $status = Test-PublicHealth $hostname $target.Value
        if ($status -eq 200) {
            $results[$target.Key] = $status
            break
        }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)

    if (-not $results.ContainsKey($target.Key)) {
        throw "LOCAL_STAGING_TUNNEL_PUBLIC_HEALTH_TIMEOUT: $($target.Value)"
    }
}

[pscustomobject]@{
    TunnelName = $TunnelName
    TunnelId = $tunnelId
    WebHostname = $WebHostname
    WebReady = $results.Web
    ApiHostname = $ApiHostname
    ApiReady = $results.Api
    TaskState = (Get-ScheduledTask -TaskName 'NeoSTP STAGING Tunnel').State
} | Format-List
