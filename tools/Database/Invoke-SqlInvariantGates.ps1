[CmdletBinding()]
param(
    [string]$OutputRoot = 'artifacts/sql-invariants',
    [switch]$AllowLocalDb
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$configuredRoot = [Environment]::GetEnvironmentVariable('NEOSTP_SQLSERVER_TEST_CONNECTION')
if ([string]::IsNullOrWhiteSpace($configuredRoot) -and -not $AllowLocalDb) {
    throw 'NEOSTP_SQLSERVER_TEST_CONNECTION is required. Use -AllowLocalDb only for an authorized local verification instance.'
}

$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
$null = New-Item -ItemType Directory -Force -Path $resolvedOutputRoot

$gates = @(
    [pscustomobject]@{
        Name = 'DTE and tenant concurrency'
        Project = 'tools/DteSqlVerification/DteSqlVerification.csproj'
        EvidenceVariable = 'NEOSTP_DTE_SQL_EVIDENCE'
        EvidenceFile = 'dte.json'
        Arguments = @('--migration-chain')
    },
    [pscustomobject]@{
        Name = 'Payment application idempotency'
        Project = 'tools/PaymentApplicationSqlVerification/PaymentApplicationSqlVerification.csproj'
        EvidenceVariable = 'NEOSTP_PAYMENT_APPLICATION_SQL_EVIDENCE'
        EvidenceFile = 'payment-application.json'
        Arguments = @()
    },
    [pscustomobject]@{
        Name = 'Wompi webhook deduplication'
        Project = 'tools/WompiWebhookSqlVerification/WompiWebhookSqlVerification.csproj'
        EvidenceVariable = 'NEOSTP_WOMPI_WEBHOOK_SQL_EVIDENCE'
        EvidenceFile = 'wompi-webhook.json'
        Arguments = @()
    }
)

Push-Location $repoRoot
try {
    foreach ($gate in $gates) {
        $evidencePath = Join-Path $resolvedOutputRoot $gate.EvidenceFile
        [Environment]::SetEnvironmentVariable($gate.EvidenceVariable, $evidencePath)

        Write-Output "Running SQL invariant gate: $($gate.Name)"
        $arguments = @(
            'run',
            '--project', $gate.Project,
            '--configuration', 'Release',
            '--no-launch-profile'
        )
        if ($gate.Arguments.Count -gt 0) {
            $arguments += '--'
            $arguments += $gate.Arguments
        }

        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "SQL invariant gate failed: $($gate.Name)"
        }
        if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
            throw "SQL invariant gate did not produce evidence: $($gate.Name)"
        }

        $evidence = Get-Content -Raw -LiteralPath $evidencePath | ConvertFrom-Json
        if ([int]$evidence.failed -ne 0 -or [int]$evidence.passed -le 0 -or -not $evidence.syntheticDatabaseDeleted) {
            throw "SQL invariant evidence is incomplete: $($gate.Name)"
        }
    }

    $totalChecks = ($gates | ForEach-Object {
        $path = Join-Path $resolvedOutputRoot $_.EvidenceFile
        [int](Get-Content -Raw -LiteralPath $path | ConvertFrom-Json).passed
    } | Measure-Object -Sum).Sum
    Write-Output "SQL invariant gates passed: $($gates.Count) gates, $totalChecks checks."
    Write-Output "Sanitized evidence: $resolvedOutputRoot"
}
finally {
    foreach ($gate in $gates) {
        [Environment]::SetEnvironmentVariable($gate.EvidenceVariable, $null)
    }
    Pop-Location
}
