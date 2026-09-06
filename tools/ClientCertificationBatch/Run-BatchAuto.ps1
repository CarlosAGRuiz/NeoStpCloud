<#
.SYNOPSIS
  Orquestador de continuacion automatica del lote de certificacion del cliente 23.

.DESCRIPTION
  Reutiliza el ejecutor CONGELADO ClientCertificationBatch.dll (no lo recompila ni
  cambia su huella) invocando --run-next en bucle. Cada invocacion es atomica: reserva
  durable, una autenticacion y una recepcion a Hacienda PRUEBAS, con STOP durable ante
  cualquier rechazo/incertidumbre. Este script solo automatiza las invocaciones y se
  DETIENE de forma conservadora ante:
    - exit code != 0 (Passed=false)
    - After.Stopped=true o After.Blocker != null
    - un resultado que no sea "un caso aceptado" ni "todos aceptados"
    - falta de progreso monotonico (After.Accepted no incrementa)
  No amplia presupuestos, no crea campanas, no toca binarios ni el plan.

.PARAMETER MaxCases
  Tope de seguridad de iteraciones (nunca bucle infinito). Default 235 (> 229 restantes).
#>
[CmdletBinding()]
param(
    [int]$MaxCases = 235,
    [string]$LogPath = "tmp/client-certification-batch/orchestrator-$(Get-Date -Format 'yyyyMMddTHHmmssZ').log"
)

$ErrorActionPreference = 'Stop'
$dll = 'tools/ClientCertificationBatch/bin/Release/net10.0/ClientCertificationBatch.dll'

function Log([string]$m) {
    $line = '[{0:yyyy-MM-ddTHH:mm:ssZ}] {1}' -f (Get-Date).ToUniversalTime(), $m
    Write-Host $line
    Add-Content -Path $LogPath -Value $line -Encoding utf8
}

if (-not (Test-Path $dll)) { throw "No existe el ejecutor congelado: $dll" }
Log "INICIO orquestador. MaxCases=$MaxCases  DLL=$dll"

$acceptedThisRun = 0
$prevAccepted = $null

for ($i = 1; $i -le $MaxCases; $i++) {
    $out = & dotnet $dll --run-next 2>&1
    $code = $LASTEXITCODE

    $path = $null
    foreach ($ln in $out) {
        if ($ln -match 'Evidence:\s*(.*\.json)') { $path = $matches[1].Trim() }
    }
    if (-not $path -or -not (Test-Path $path)) {
        Log "ABORT: no se encontro el reporte de evidencia (exit=$code). Deteniendo."
        break
    }

    $j = Get-Content $path -Raw | ConvertFrom-Json

    if ($code -ne 0 -or $j.Passed -ne $true) {
        Log "STOP: exit=$code Passed=$($j.Passed) FailureType=$($j.FailureType) PipelineCode=$($j.PipelineCode). Evidencia: $path"
        Log "Revision humana requerida. No se continua."
        break
    }
    if ($j.All275LocallyAccepted -eq $true) {
        Log "COMPLETO: no quedan casos pendientes (All275LocallyAccepted). Evidencia: $path"
        break
    }
    if ($j.OneCaseAccepted -ne $true) {
        Log "ABORT conservador: resultado ni 'caso aceptado' ni 'completo'. Evidencia: $path"
        break
    }
    if ($j.After.Stopped -eq $true -or $null -ne $j.After.Blocker) {
        Log "ABORT: After.Stopped=$($j.After.Stopped) Blocker=$($j.After.Blocker). Evidencia: $path"
        break
    }
    # Progreso monotonico
    $acc = [int]$j.After.Accepted
    if ($null -ne $prevAccepted -and $acc -ne ($prevAccepted + 1)) {
        Log "ABORT: progreso no monotonico (prev=$prevAccepted actual=$acc). Evidencia: $path"
        break
    }
    $prevAccepted = $acc
    $acceptedThisRun++
    Log ("OK  doc={0} case={1}  After.Accepted={2}/275  next={3}" -f $j.Reservation.DocumentId, $j.Reservation.CaseKey, $acc, $j.After.NextCase)
}

Log "FIN. Casos aceptados en esta corrida: $acceptedThisRun. Log: $LogPath"
