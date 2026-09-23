#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Measure actual time until emulator AMQP port is ready for SDK connections.

.DESCRIPTION
    Starts emulator fresh and times how long it takes for a real ServiceBusClient
    to successfully connect (not just port response).
#>

$projectRoot = Split-Path -Parent $PSScriptRoot
$infraPath = Join-Path $projectRoot 'infra' 'servicebus'

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║            EMULATOR AMQP READINESS TEST                                   ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Stop any existing containers
Write-Host "STEP 1: Cleaning up existing containers..." -ForegroundColor Yellow
Push-Location $infraPath
docker-compose down -v 2>&1 | Out-Null
Start-Sleep -Seconds 3

# Start fresh
Write-Host "STEP 2: Starting emulator..." -ForegroundColor Yellow
$overallStart = Get-Date
docker-compose up -d 2>&1 | Where-Object { -not ($_ -match 'warning') } | Out-Null
Write-Host "  Containers launched at $(Get-Date -Format 'HH:mm:ss.fff')"
Write-Host ""

# Now run Producer to measure real connection time
Write-Host "STEP 3: Testing Producer connection with instrumentation..." -ForegroundColor Yellow
Write-Host "  (Looking for '✓ Service Bus is ready!' message)" -ForegroundColor Gray
Write-Host ""

# Set up environment
$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE"
$env:ServiceBus__TopicName = "contact.events"
$env:Dashboard__Enabled = "false"
$env:DOTNET_Environment = "Development"
$env:LOGGING__LOGLEVEL__DEFAULT = "Information"

# Run Producer and capture output
$outputFile = [System.IO.Path]::GetTempFileName()
$process = Start-Process -FilePath "dotnet" `
  -ArgumentList "run --configuration Debug --project $projectRoot/src/ServiceBusPoc.Producer/ServiceBusPoc.Producer.csproj" `
  -RedirectStandardOutput $outputFile `
  -NoNewWindow -PassThru

# Wait up to 180 seconds for success or failure
$elapsed = 0
$maxWait = 180
$found = $false

while ($elapsed -lt $maxWait -and -not $process.HasExited) {
    $output = Get-Content $outputFile -ErrorAction SilentlyContinue | Select-String "✓ Service Bus is ready"
    if ($output) {
        $found = $true
        break
    }
    Start-Sleep -Milliseconds 500
    $elapsed += 0.5
}

# Show output
$totalOutput = Get-Content $outputFile -ErrorAction SilentlyContinue
Write-Host $totalOutput

# Kill process if still running
if (-not $process.HasExited) {
    $process.Kill()
    $process.WaitForExit()
}

Pop-Location

Write-Host ""
if ($found) {
    Write-Host "✓ SUCCESS: Connection established after $elapsed seconds" -ForegroundColor Green
    Write-Host "  Update run-dashboard.ps1 wait time to: $([Math]::Ceiling($elapsed + 10))s" -ForegroundColor Cyan
} else {
    Write-Host "✗ FAILED: Connection not established within $maxWait seconds" -ForegroundColor Red
    Write-Host "  Check emulator logs for startup issues" -ForegroundColor Yellow
}

Write-Host ""
Remove-Item $outputFile -ErrorAction SilentlyContinue
