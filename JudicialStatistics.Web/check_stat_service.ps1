$path = "..\JudicialStatistics.Infrastructure\Services\StatisticsService.cs"
$lines = Get-Content $path
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match 'EXPERT_RETURN|WAIT_REPORT|CaseTypeDeferralRule|ReturnIncoming|Postponement|DeferralReason') {
        Write-Output (($i+1).ToString() + ": " + $lines[$i].Trim())
    }
}
