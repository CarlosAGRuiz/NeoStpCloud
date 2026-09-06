param([ValidateRange(1,10)][int]$Count=5)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$binary=Join-Path $PSScriptRoot 'bin/Release/net10.0/ClientCertificationBatch.dll'
$hash='6F87264293D852FDCE6EAEE69E192F86CF298D2786A9BCB86F0564BA28A65809'
$plan='FD21C5AC3D9BC27A03693299DE54D083D25CCFCA68C646D069C77680D14C160E'
if((Get-Location).Path-cne$repo){throw 'BATCH_WORKSPACE_REJECTED'}
for($index=0;$index-lt$Count;$index++){
 if((Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash-cne$hash){throw 'REVIEWED_BATCH_BINARY_CHANGED'}
 $output=@(& dotnet $binary --run-next)
 $code=$LASTEXITCODE
 $report=(($output|Where-Object{-not$_.StartsWith('Evidence: ')})-join[Environment]::NewLine)|ConvertFrom-Json
 $summary=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('O');Passed=$report.Passed;DocumentId=$report.Reservation.DocumentId;Case=$report.Reservation.CaseKey;Accepted=$report.After.Accepted;Total=275;ReceptionAttempts=$report.HaciendaReceptionAttempts;Code=$report.Code}
 $summary|ConvertTo-Json -Compress|Write-Output
 if($code-ne0-or$report.Passed-ne$true-or$report.PlanHash-cne$plan){throw 'BATCH_STOPPED_REVIEW_RESULT'}
 if($report.All275LocallyAccepted-eq$true-and$report.After.Accepted-eq275-and$report.HaciendaReceptionAttempts-eq0){break}
 if($report.OneCaseAccepted-ne$true-or$report.HaciendaReceptionAttempts-ne1-or$report.After.Stopped-eq$true-or$null-ne$report.After.Blocker-or$report.After.Accepted-ne($report.Before.Accepted+1)-or$report.After.Accepted-ne$report.After.Reserved){throw 'BATCH_PROGRESS_REQUIRES_REVIEW'}
 if($report.After.Accepted-eq275){break}
}
