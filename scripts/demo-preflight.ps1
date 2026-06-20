[CmdletBinding()]
param(
    [ValidateSet("Demo", "Release")]
    [string]$Profile = "Demo",

    [switch]$StaticOnly,
    [switch]$Restore,
    [switch]$AllowDirtyWorktree,
    [switch]$RequireServices,

    [string]$ApiBaseUrl = "",
    [string]$WebBaseUrl = "",
    [string]$EvidencePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$startedAt = [DateTime]::UtcNow
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$checks = New-Object System.Collections.Generic.List[object]
$hasFailures = $false

function Add-CheckResult {
    param(
        [string]$Name,
        [ValidateSet("PASS", "WARN", "FAIL")]
        [string]$Status,
        [string]$Detail,
        [long]$DurationMs = 0
    )

    if ($Status -eq "FAIL") { $script:hasFailures = $true }
    $script:checks.Add([pscustomobject]@{
        name = $Name
        status = $Status
        detail = $Detail
        durationMs = $DurationMs
    })

    $color = switch ($Status) {
        "PASS" { "Green" }
        "WARN" { "Yellow" }
        default { "Red" }
    }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Name, $Detail) -ForegroundColor $color
}

function Invoke-Check {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $detail = & $Action
        $watch.Stop()
        if ($null -eq $detail -or [string]::IsNullOrWhiteSpace([string]$detail)) { $detail = "OK" }
        Add-CheckResult -Name $Name -Status "PASS" -Detail ([string]$detail) -DurationMs $watch.ElapsedMilliseconds
    }
    catch {
        $watch.Stop()
        Add-CheckResult -Name $Name -Status "FAIL" -Detail $_.Exception.Message -DurationMs $watch.ElapsedMilliseconds
    }
}

function Invoke-ExternalCheck {
    param(
        [string]$Name,
        [string]$Command,
        [string[]]$Arguments
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $previousErrorAction = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = & $Command @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
        $watch.Stop()
    }

    if ($exitCode -eq 0) {
        Add-CheckResult -Name $Name -Status "PASS" -Detail "Comando completado." -DurationMs $watch.ElapsedMilliseconds
        return $true
    }

    $summary = (($output | Select-Object -Last 20) -join [Environment]::NewLine).Trim()
    if ($summary.Length -gt 3000) { $summary = $summary.Substring($summary.Length - 3000) }
    Add-CheckResult -Name $Name -Status "FAIL" -Detail $summary -DurationMs $watch.ElapsedMilliseconds
    return $false
}

function Get-JsonValue {
    param(
        [object]$Root,
        [string]$Path
    )

    $current = $Root
    foreach ($segment in $Path.Split('.')) {
        if ($null -eq $current) { return $null }
        $property = $current.PSObject.Properties[$segment]
        if ($null -eq $property) { return $null }
        $current = $property.Value
    }
    return $current
}

function Get-EffectiveConfigValue {
    param([string]$Path)

    $localValue = Get-JsonValue -Root $script:localConfig -Path $Path
    if ($null -ne $localValue -and -not [string]::IsNullOrWhiteSpace([string]$localValue)) {
        return $localValue
    }
    return Get-JsonValue -Root $script:baseConfig -Path $Path
}

function Test-HttpEndpoint {
    param(
        [string]$Name,
        [string]$Uri
    )

    Invoke-Check -Name $Name -Action {
        $response = Invoke-WebRequest -Uri $Uri -Method Get -UseBasicParsing -TimeoutSec 20
        if ([int]$response.StatusCode -ne 200) {
            throw "HTTP $($response.StatusCode) en $Uri"
        }
        "HTTP 200"
    }
}

Push-Location $repoRoot
try {
    Invoke-Check -Name "repo.solution" -Action {
        if (-not (Test-Path "NeoSTP.slnx" -PathType Leaf)) { throw "NeoSTP.slnx no encontrado." }
        "Raiz NeoSTP valida."
    }

    Invoke-Check -Name "runtime.dotnet" -Action {
        $version = (& dotnet --version).Trim()
        if ($LASTEXITCODE -ne 0 -or $version -notmatch '^10\.') {
            throw "Se requiere .NET SDK 10.x; detectado '$version'."
        }
        ".NET SDK $version"
    }

    Invoke-Check -Name "git.metadata" -Action {
        $script:branch = (& git rev-parse --abbrev-ref HEAD).Trim()
        $script:commit = (& git rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { throw "No se pudo leer metadata Git." }
        "branch=$($script:branch); commit=$($script:commit.Substring(0, 12))"
    }

    $dirty = @(& git status --porcelain)
    if ($dirty.Count -eq 0) {
        Add-CheckResult -Name "git.worktree" -Status "PASS" -Detail "Arbol limpio."
    }
    elseif ($AllowDirtyWorktree) {
        Add-CheckResult -Name "git.worktree" -Status "WARN" -Detail "Arbol con $($dirty.Count) cambio(s); permitido por parametro."
    }
    else {
        Add-CheckResult -Name "git.worktree" -Status "FAIL" -Detail "Arbol con $($dirty.Count) cambio(s). Use -AllowDirtyWorktree solo para ensayos locales."
    }

    Invoke-Check -Name "security.tracked-secrets" -Action {
        $tracked = @(& git ls-files)
        $sensitive = @($tracked | Where-Object {
            $_ -match '(^|/)appsettings\.Local\.json$' -or
            $_ -match '(^|/)appsettings\.[^/]+\.Local\.json$' -or
            $_ -match '(^|/)secrets\.json$' -or
            $_ -match '\.(pfx|p12|pem|key)$'
        })
        if ($sensitive.Count -gt 0) {
            throw "Archivos sensibles tracked: $($sensitive -join ', ')"
        }
        "Sin appsettings.Local, certificados ni llaves privadas tracked."
    }

    Invoke-Check -Name "config.parse" -Action {
        $script:baseConfig = Get-Content "src\NeoSTP.Api\appsettings.json" -Raw | ConvertFrom-Json
        $localPath = "src\NeoSTP.Api\appsettings.Local.json"
        $script:localConfig = if (Test-Path $localPath) {
            Get-Content $localPath -Raw | ConvertFrom-Json
        }
        else {
            [pscustomobject]@{}
        }
        "Configuracion API parseable; secretos no impresos."
    }

    $providerSpecs = @(
        [pscustomobject]@{ Name = "Email"; Path = "Email.Provider"; Allowed = @("Mock", "Smtp") },
        [pscustomobject]@{ Name = "Hacienda"; Path = "Hacienda.Client"; Allowed = @("Mock", "Http") },
        [pscustomobject]@{ Name = "DteSigner"; Path = "Dte.Signer"; Allowed = @("Mock", "Pkcs12", "HaciendaCert") },
        [pscustomobject]@{ Name = "Scan"; Path = "Scan.Provider"; Allowed = @("Mock", "Gemini") },
        [pscustomobject]@{ Name = "ScanStorage"; Path = "Scan.Storage.Provider"; Allowed = @("Database", "FileSystem") },
        [pscustomobject]@{ Name = "Push"; Path = "Push.Provider"; Allowed = @("Mock", "Fcm") },
        [pscustomobject]@{ Name = "WhatsApp"; Path = "WhatsApp.Provider"; Allowed = @("Mock", "Meta") },
        [pscustomobject]@{ Name = "Cache"; Path = "Cache.Provider"; Allowed = @("Memory", "Redis") },
        [pscustomobject]@{ Name = "Billing"; Path = "Billing.Provider"; Allowed = @("Mock", "Wompi", "PayPal", "Transferencia", "Stripe", "MercadoPago") }
    )

    $script:providers = [ordered]@{}
    foreach ($spec in $providerSpecs) {
        $value = [string](Get-EffectiveConfigValue -Path $spec.Path)
        if ([string]::IsNullOrWhiteSpace($value)) {
            $script:providers[$spec.Name] = "Default"
            Add-CheckResult -Name "provider.$($spec.Name)" -Status "WARN" -Detail "No declarado; se usa default de codigo."
        }
        elseif ($spec.Allowed -contains $value) {
            $script:providers[$spec.Name] = $value
            Add-CheckResult -Name "provider.$($spec.Name)" -Status "PASS" -Detail $value
        }
        else {
            $script:providers[$spec.Name] = "Invalid"
            Add-CheckResult -Name "provider.$($spec.Name)" -Status "FAIL" -Detail "Valor '$value' no soportado."
        }
    }

    $jwtKey = [string](Get-EffectiveConfigValue -Path "Jwt.Key")
    $jwtStrong = $jwtKey.Length -ge 32 -and $jwtKey -notmatch 'replace-me|change-me|changeme|placeholder'
    if ($jwtStrong) {
        Add-CheckResult -Name "security.jwt" -Status "PASS" -Detail "Jwt:Key configurada con longitud minima; valor oculto."
    }
    elseif ($Profile -eq "Release") {
        Add-CheckResult -Name "security.jwt" -Status "FAIL" -Detail "Jwt:Key ausente, corta o placeholder."
    }
    else {
        Add-CheckResult -Name "security.jwt" -Status "WARN" -Detail "Jwt:Key de demo es ausente, corta o placeholder; no usar en release."
    }

    $demoEnabled = [bool](Get-EffectiveConfigValue -Path "EmpresaPrueba.Enabled")
    $mobileDemoEnabled = [bool](Get-EffectiveConfigValue -Path "EmpresaPrueba.MobileDemo.Enabled")
    if ($Profile -eq "Demo") {
        if ($demoEnabled -and $mobileDemoEnabled) {
            Add-CheckResult -Name "demo.seed" -Status "PASS" -Detail "EmpresaPrueba y MobileDemo habilitados."
        }
        else {
            Add-CheckResult -Name "demo.seed" -Status "WARN" -Detail "EmpresaPrueba/MobileDemo no estan ambos habilitados; validar datos manuales."
        }
    }
    elseif ($demoEnabled -or $mobileDemoEnabled) {
        Add-CheckResult -Name "release.demo-seed" -Status "FAIL" -Detail "El seed demo debe estar deshabilitado en release."
    }
    else {
        Add-CheckResult -Name "release.demo-seed" -Status "PASS" -Detail "Seed demo deshabilitado."
    }

    if ($Profile -eq "Release") {
        foreach ($required in @(
            [pscustomobject]@{ Name = "Hacienda"; Value = $script:providers["Hacienda"]; Expected = "Http" },
            [pscustomobject]@{ Name = "DteSigner"; Value = $script:providers["DteSigner"]; Expected = "HaciendaCert" }
        )) {
            if ($required.Value -eq $required.Expected) {
                Add-CheckResult -Name "release.$($required.Name)" -Status "PASS" -Detail $required.Expected
            }
            else {
                Add-CheckResult -Name "release.$($required.Name)" -Status "FAIL" -Detail "Se requiere $($required.Expected); detectado $($required.Value)."
            }
        }
    }

    if (-not $StaticOnly) {
        $restoreOk = $true
        if ($Restore) {
            $restoreOk = Invoke-ExternalCheck -Name "restore.solution" -Command "dotnet" -Arguments @("restore", "NeoSTP.slnx", "--nologo", "--verbosity", "minimal")
        }
        else {
            Add-CheckResult -Name "restore.solution" -Status "WARN" -Detail "Omitido; use -Restore en un checkout sin assets restaurados."
        }

        if ($restoreOk) {
            $buildOk = Invoke-ExternalCheck -Name "build.solution" -Command "dotnet" -Arguments @("build", "NeoSTP.slnx", "--nologo", "--verbosity", "minimal", "--no-restore")
        }
        else {
            $buildOk = $false
            Add-CheckResult -Name "build.solution" -Status "FAIL" -Detail "Omitido porque restore.solution fallo."
        }

        if ($buildOk) {
            $null = Invoke-ExternalCheck -Name "test.solution" -Command "dotnet" -Arguments @("test", "NeoSTP.slnx", "--nologo", "--verbosity", "minimal", "--no-build", "--no-restore")
        }
        else {
            Add-CheckResult -Name "test.solution" -Status "FAIL" -Detail "Omitido porque build.solution fallo."
        }
    }
    else {
        Add-CheckResult -Name "restore.solution" -Status "WARN" -Detail "Omitido por -StaticOnly."
        Add-CheckResult -Name "build.solution" -Status "WARN" -Detail "Omitido por -StaticOnly."
        Add-CheckResult -Name "test.solution" -Status "WARN" -Detail "Omitido por -StaticOnly."
    }

    if (-not [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
        $api = $ApiBaseUrl.TrimEnd('/')
        Test-HttpEndpoint -Name "api.health-live" -Uri "$api/health/live"
        Test-HttpEndpoint -Name "api.health-ready" -Uri "$api/health/ready"
        Test-HttpEndpoint -Name "api.openapi" -Uri "$api/openapi/v1.json"
    }
    elseif ($RequireServices) {
        Add-CheckResult -Name "api.services" -Status "FAIL" -Detail "Falta -ApiBaseUrl con -RequireServices."
    }
    else {
        Add-CheckResult -Name "api.services" -Status "WARN" -Detail "Smoke HTTP omitido; no se proporciono -ApiBaseUrl."
    }

    if (-not [string]::IsNullOrWhiteSpace($WebBaseUrl)) {
        $web = $WebBaseUrl.TrimEnd('/')
        Test-HttpEndpoint -Name "web.health-live" -Uri "$web/health/live"
        Test-HttpEndpoint -Name "web.health-ready" -Uri "$web/health/ready"
    }
    elseif ($RequireServices) {
        Add-CheckResult -Name "web.services" -Status "FAIL" -Detail "Falta -WebBaseUrl con -RequireServices."
    }
    else {
        Add-CheckResult -Name "web.services" -Status "WARN" -Detail "Smoke HTTP omitido; no se proporciono -WebBaseUrl."
    }

    $warnings = @($checks | Where-Object status -eq "WARN").Count
    $decision = if ($hasFailures) {
        "NO_APTO"
    }
    elseif ($warnings -gt 0) {
        "APTO_CON_ADVERTENCIAS"
    }
    elseif ($Profile -eq "Release") {
        "APTO_RELEASE"
    }
    else {
        "APTO_DEMO"
    }

    $checkArray = @($checks | ForEach-Object { $_ })
    $evidence = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString("o")
        profile = $Profile
        decision = $decision
        branch = $branch
        commit = $commit
        staticOnly = [bool]$StaticOnly
        providers = $providers
        checks = $checkArray
        summary = [ordered]@{
            passed = @($checks | Where-Object status -eq "PASS").Count
            warnings = $warnings
            failed = @($checks | Where-Object status -eq "FAIL").Count
            durationMs = [long]([DateTime]::UtcNow - $startedAt).TotalMilliseconds
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
        $absoluteEvidencePath = if ([System.IO.Path]::IsPathRooted($EvidencePath)) {
            $EvidencePath
        }
        else {
            Join-Path $repoRoot $EvidencePath
        }
        $evidenceDir = Split-Path -Parent $absoluteEvidencePath
        if (-not [string]::IsNullOrWhiteSpace($evidenceDir)) {
            New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
        }
        $evidence | ConvertTo-Json -Depth 8 | Set-Content -Path $absoluteEvidencePath -Encoding UTF8
        Write-Host "Evidencia: $absoluteEvidencePath" -ForegroundColor Cyan
    }

    Write-Host "Decision: $decision" -ForegroundColor $(if ($hasFailures) { "Red" } else { "Cyan" })
    if ($hasFailures) { exit 1 }
}
finally {
    Pop-Location
}
