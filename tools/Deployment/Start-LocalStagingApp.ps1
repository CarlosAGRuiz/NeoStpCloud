[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Api', 'Web', 'Worker')]
    [string]$App,

    [Parameter(Mandatory)]
    [string]$DataRoot
)

$ErrorActionPreference = 'Stop'
$deploymentPath = Join-Path $DataRoot 'deployment.json'
$secretsPath = Join-Path $DataRoot 'secrets\staging.secrets.json'
$deployment = Get-Content -LiteralPath $deploymentPath -Raw | ConvertFrom-Json
$secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json

function Unprotect-Text([string]$cipherText) {
    $secure = ConvertTo-SecureString $cipherText
    $credential = [System.Net.NetworkCredential]::new('', $secure)
    $credential.Password
}

$env:ASPNETCORE_ENVIRONMENT = 'Staging'
$env:DOTNET_ENVIRONMENT = 'Staging'
$database = [Data.SqlClient.SqlConnectionStringBuilder]::new()
$database['Data Source'] = $deployment.SqlServer
$database['Initial Catalog'] = $deployment.Database
$database['User ID'] = $deployment.SqlLogin
$database['Password'] = Unprotect-Text $secrets.DatabasePassword
$database['Encrypt'] = $true
$database['TrustServerCertificate'] = $true
$database['MultipleActiveResultSets'] = $true
$env:ConnectionStrings__NeoStpDb = $database.ConnectionString
$env:Deployment__DataRoot = $DataRoot
$env:DataProtection__KeyRingPath = (Join-Path $DataRoot 'DataProtection')
$env:DataProtection__CertificateThumbprint = $deployment.DataProtectionThumbprint
$env:DataProtection__StoreName = 'My'
$env:DataProtection__StoreLocation = 'CurrentUser'
$env:Jwt__Key = Unprotect-Text $secrets.JwtKey
$env:Serilog__WriteTo__1__Args__path = Join-Path $DataRoot "logs\neostp-$($App.ToLowerInvariant())-.log"
$env:Ops__Database__ApplyMigrationsOnStartup = 'false'
$env:Ops__Database__SeedOnStartup = 'false'
$env:SuperAdmin__BootstrapEnabled = 'false'
$env:EmpresaPrueba__Enabled = 'false'
$env:DemoComercial__Enabled = 'false'

if ($App -eq 'Worker') {
    $env:Worker__Enabled = $deployment.WorkerEnabled.ToString().ToLowerInvariant()
    $env:Hardening__Backup__LocalPath = Join-Path $DataRoot 'backups'
} else {
    $port = if ($App -eq 'Api') { $deployment.ApiPort } else { $deployment.WebPort }
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$port"
}

$executable = Join-Path $deployment.ReleaseRoot "$($App.ToLowerInvariant())\NeoSTP.$App.exe"
if (-not (Test-Path -LiteralPath $executable)) {
    throw "No existe el ejecutable publicado para $App."
}

try {
    Set-Location -LiteralPath (Split-Path -Parent $executable)
    & $executable
    exit $LASTEXITCODE
}
finally {
    Remove-Item Env:\Jwt__Key -ErrorAction SilentlyContinue
}
