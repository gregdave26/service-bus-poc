#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Minimal test: Start emulator, Producer, and one Consumer (DigitalChannels).

.DESCRIPTION
    Tests the Service Bus connectivity with minimal complexity:
    - Starts Docker emulator
    - Starts Producer (publishes events)
    - Starts DigitalChannels consumer (receives all events)
    - Monitors console output
    - Stop with Ctrl+C

.EXAMPLE
    .\scripts\test-simple.ps1
#>

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $projectRoot 'src'
$infraPath = Join-Path $projectRoot 'infra'
$logsPath = Join-Path $projectRoot 'logs'

# Ensure logs directory exists
if (-not (Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath | Out-Null
}

Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                  MINIMAL TEST: Producer + One Consumer                     ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Step 1: Start emulator
Write-Host "STEP 1: Starting Service Bus emulator..." -ForegroundColor Yellow
$composePath = Join-Path $infraPath 'servicebus' 'compose.yaml'

Push-Location (Split-Path $composePath)
try {
    $existing = docker-compose ps --quiet 2>$null
    if ($existing) {
        Write-Host "  Stopping existing containers..."
        docker-compose down | Out-Null
    }
    
    Write-Host "  Starting containers..."
    $null = docker-compose up -d
    Write-Host "  ✓ Emulator started"
    Write-Host "  Waiting 30 seconds for initialization..."
    Start-Sleep -Seconds 30
    Write-Host "  ✓ Ready"
}
catch {
    Write-Error "❌ Failed to start emulator: $_"
    exit 1
}
finally {
    Pop-Location
}

Write-Host ""

# Step 2: Build solution
Write-Host "STEP 2: Building solution..." -ForegroundColor Yellow
$solutionPath = Join-Path $srcPath 'ServiceBusPoc.slnx'
try {
    $output = dotnet build $solutionPath --configuration Debug --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Build failed`n$output"
        exit 1
    }
    Write-Host "  ✓ Build successful"
}
catch {
    Write-Error "❌ Build failed: $_"
    exit 1
}

Write-Host ""

# Step 3: Configure environment
Write-Host "STEP 3: Configuring environment..." -ForegroundColor Yellow
$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE"
$env:ServiceBus__Namespace = "localhost"
$env:ServiceBus__TopicName = "contact.events"
$env:Dashboard__Enabled = "false"
$env:DOTNET_Environment = "Development"
Write-Host "  ✓ Environment configured"

Write-Host ""

# Step 4: Start services
Write-Host "STEP 4: Starting services..." -ForegroundColor Yellow
Write-Host ""

$processes = @()

# Start Producer
Write-Host "  Starting Producer..."
$producerTimestamp = Get-Date -Format 'ddMMyyyy-HHmmss'
$producerStdoutLog = Join-Path $logsPath "producer-$producerTimestamp-stdout.log"
$producerStderrLog = Join-Path $logsPath "producer-$producerTimestamp-stderr.log"

$producerProc = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList @('run', '--configuration', 'Debug', '--project', 'src/ServiceBusPoc.Producer/ServiceBusPoc.Producer.csproj') `
    -WorkingDirectory $projectRoot `
    -RedirectStandardOutput $producerStdoutLog `
    -RedirectStandardError $producerStderrLog `
    -PassThru `
    -NoNewWindow

if ($null -ne $producerProc) {
    $processes += $producerProc
    Write-Host "  ✓ Producer started (PID: $($producerProc.Id))"
    Write-Host "    📌 Logs: logs/producer-$producerTimestamp-stdout.log | stderr.log" -ForegroundColor Gray
    Start-Sleep -Seconds 3
}

# Start DigitalChannels Consumer
Write-Host "  Starting DigitalChannels consumer..."
$env:ServiceBus__SubscriptionName = "digital-channels"

$digitalTimestamp = Get-Date -Format 'ddMMyyyy-HHmmss'
$digitalStdoutLog = Join-Path $logsPath "digitalchannels-$digitalTimestamp-stdout.log"
$digitalStderrLog = Join-Path $logsPath "digitalchannels-$digitalTimestamp-stderr.log"

$digitalProc = Start-Process `
    -FilePath 'dotnet' `
    -ArgumentList @('run', '--configuration', 'Debug', '--project', 'src/ServiceBusPoc.DigitalChannels/ServiceBusPoc.DigitalChannels.csproj') `
    -WorkingDirectory $projectRoot `
    -RedirectStandardOutput $digitalStdoutLog `
    -RedirectStandardError $digitalStderrLog `
    -PassThru `
    -NoNewWindow

if ($null -ne $digitalProc) {
    $processes += $digitalProc
    Write-Host "  ✓ DigitalChannels started (PID: $($digitalProc.Id))"
    Write-Host "    📌 Logs: logs/digitalchannels-$digitalTimestamp-stdout.log | stderr.log" -ForegroundColor Gray
    Start-Sleep -Seconds 3
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                      ✓ Services started                                   ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host ""
Write-Host "Monitoring output (Ctrl+C to stop)..." -ForegroundColor Cyan
Write-Host ""

# Setup graceful shutdown
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
    Write-Host ""
    Write-Host "Shutting down services..." -ForegroundColor Yellow
    
    foreach ($process in $processes) {
        if ($null -ne $process -and -not $process.HasExited) {
            try {
                Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
                Write-Host "  ✓ Stopped process $($process.Id)"
            }
            catch {
                # Ignored
            }
        }
    }
    
    Write-Host "  ✓ Services stopped"
    Write-Host ""
}

# Keep running
while ($true) {
    Start-Sleep -Seconds 10
}
