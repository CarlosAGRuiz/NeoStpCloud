[CmdletBinding()]
param(
    [string]$DataRoot = (Join-Path $env:LOCALAPPDATA 'NeoSTP\STAGING')
)

$ErrorActionPreference = 'Stop'
$secretsPath = Join-Path ([IO.Path]::GetFullPath($DataRoot)) 'secrets\staging.secrets.json'
$secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
$password = ConvertTo-SecureString $secrets.AdminPassword
[PSCredential]::new($secrets.Username, $password)