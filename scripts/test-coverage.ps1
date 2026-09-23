$ErrorActionPreference = 'Stop'

$resultsDirectory = Join-Path $PSScriptRoot '..\TestResults\Coverage'
if (Test-Path $resultsDirectory) {
    Remove-Item $resultsDirectory -Recurse -Force
}

dotnet test "$PSScriptRoot\..\tests\ServiceBusPoc.Tests\ServiceBusPoc.Tests.csproj" `
    --no-restore `
    -m:1 `
    --settings "$PSScriptRoot\..\coverage.runsettings" `
    --collect:"XPlat Code Coverage" `
    --results-directory $resultsDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE. Coverage output is not valid for a failed test run."
}

$coverageFile = Get-ChildItem $resultsDirectory -Recurse -Filter coverage.cobertura.xml |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $coverageFile) {
    throw "Coverage report was not produced."
}

[xml]$coverage = Get-Content $coverageFile.FullName
$lineRate = [double]$coverage.coverage.'line-rate'
$percentage = $lineRate * 100
Write-Host ("Meaningful unit line coverage: {0:N2}% (threshold: 80.00%)" -f $percentage)

if ($lineRate -lt 0.80) {
    throw ("Meaningful unit line coverage is below the required 80% threshold: {0:N2}%" -f $percentage)
}
