[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Environment = 'Development',
    [int]$WebPort = 5031,
    [int]$ApiPort = 5058,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'Este instalador de inicio automático sólo funciona en Windows.'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputRoot = Join-Path $repoRoot 'out\local-autostart'
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name

$apps = @(
    [pscustomobject]@{
        TaskName = 'NeoSTP API'
        Project = Join-Path $repoRoot 'src\NeoSTP.Api\NeoSTP.Api.csproj'
        SourceLocalSettings = Join-Path $repoRoot 'src\NeoSTP.Api\appsettings.Local.json'
        Output = Join-Path $outputRoot 'api'
        Executable = 'NeoSTP.Api.exe'
        Port = $ApiPort
        HealthPath = '/health'
    },
    [pscustomobject]@{
        TaskName = 'NeoSTP Web'
        Project = Join-Path $repoRoot 'src\NeoSTP.Web\NeoSTP.Web.csproj'
        SourceLocalSettings = Join-Path $repoRoot 'src\NeoSTP.Web\appsettings.Local.json'
        Output = Join-Path $outputRoot 'web'
        Executable = 'NeoSTP.Web.exe'
        Port = $WebPort
        HealthPath = '/health/live'
    }
)

foreach ($app in $apps) {
    $existing = Get-ScheduledTask -TaskName $app.TaskName -ErrorAction SilentlyContinue
    if ($existing) {
        Stop-ScheduledTask -TaskName $app.TaskName -ErrorAction SilentlyContinue
    }
}

if (-not $SkipPublish) {
    foreach ($app in $apps) {
        New-Item -ItemType Directory -Path $app.Output -Force | Out-Null
        & dotnet publish $app.Project -c $Configuration -o $app.Output --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "Falló la publicación de $($app.TaskName)."
        }

        if (Test-Path -LiteralPath $app.SourceLocalSettings) {
            Copy-Item -LiteralPath $app.SourceLocalSettings -Destination (Join-Path $app.Output 'appsettings.Local.json') -Force
        }
    }
}

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 999 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit (New-TimeSpan -Days 3650) `
    -MultipleInstances IgnoreNew

$principal = New-ScheduledTaskPrincipal `
    -UserId $identity `
    -LogonType Interactive `
    -RunLevel Limited

foreach ($app in $apps) {
    $executable = Join-Path $app.Output $app.Executable
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "No existe el ejecutable publicado: $executable"
    }

    $arguments = "--environment $Environment --urls http://127.0.0.1:$($app.Port)"
    $action = New-ScheduledTaskAction `
        -Execute $executable `
        -Argument $arguments `
        -WorkingDirectory $app.Output
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity
    $task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings

    Register-ScheduledTask -TaskName $app.TaskName -InputObject $task -Force | Out-Null
    Start-ScheduledTask -TaskName $app.TaskName
}

$deadline = (Get-Date).AddSeconds(90)
$pending = [System.Collections.Generic.List[object]]::new()
foreach ($app in $apps) { $pending.Add($app) }

while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline) {
    foreach ($app in @($pending)) {
        try {
            $response = Invoke-WebRequest `
                -Uri "http://127.0.0.1:$($app.Port)$($app.HealthPath)" `
                -UseBasicParsing `
                -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                [void]$pending.Remove($app)
            }
        }
        catch { }
    }
    if ($pending.Count -gt 0) { Start-Sleep -Seconds 2 }
}

$result = foreach ($app in $apps) {
    $task = Get-ScheduledTask -TaskName $app.TaskName
    [pscustomobject]@{
        Name = $app.TaskName
        TaskState = $task.State
        Url = "http://127.0.0.1:$($app.Port)"
        Healthy = -not $pending.Contains($app)
        StartsAtLogonAs = $identity
    }
}

$result | Format-Table -AutoSize
if ($pending.Count -gt 0) {
    throw "No respondieron a tiempo: $($pending.TaskName -join ', ')."
}
