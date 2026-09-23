#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test emulator startup time in isolation.

.DESCRIPTION
    Starts the Service Bus emulator and measures how long it takes
    to become ready (listening on AMQP port 5672).

.EXAMPLE
    .\scripts\test-emulator-startup.ps1
#>

$projectRoot = Split-Path -Parent $PSScriptRoot
$infraPath = Join-Path $projectRoot 'infra'

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                  EMULATOR STARTUP TIME TEST                                ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop any existing containers
Write-Host "STEP 1: Cleaning up existing containers..." -ForegroundColor Yellow
$composePath = Join-Path $infraPath 'servicebus' 'compose.yaml'

Push-Location (Split-Path $composePath)
try {
    $existing = docker-compose ps --quiet 2>$null
    if ($existing) {
        Write-Host "  Stopping existing containers..."
        docker-compose down | Out-Null
        Start-Sleep -Seconds 5
    }
    Write-Host "  ✓ Cleanup complete"
}
catch {
    Write-Error "❌ Cleanup failed: $_"
    exit 1
}
finally {
    Pop-Location
}

Write-Host ""

# Step 2: Start emulator and time it
Write-Host "STEP 2: Starting emulator and measuring startup time..." -ForegroundColor Yellow
Write-Host ""

$startTime = Get-Date
$maxWait = 300  # 5 minutes max
$checkInterval = 1  # Check every 1 second
$isReady = $false
$elapsedSeconds = 0

Push-Location (Split-Path $composePath)
try {
    Write-Host "  Starting containers at: $(Get-Date -Format 'HH:mm:ss.fff')"
    $null = docker-compose up -d
    
    Write-Host "  Waiting for AMQP port 5672 to respond..."
    Write-Host ""
    
    while ($elapsedSeconds -lt $maxWait -and -not $isReady) {
        try {
            # Try to connect to AMQP port
            $socket = New-Object System.Net.Sockets.TcpClient
            $asyncConnect = $socket.BeginConnect('localhost', 5672, $null, $null)
            
            if ($asyncConnect.AsyncWaitHandle.WaitOne(1000)) {
                try {
                    $socket.EndConnect($asyncConnect)
                    $isReady = $true
                    $socket.Close()
                }
                catch {
                    $socket.Close()
                }
            }
            else {
                $socket.Close()
            }
        }
        catch {
            # Connection failed, keep waiting
        }
        
        if (-not $isReady) {
            $elapsedSeconds += $checkInterval
            Write-Host "  [$($elapsedSeconds)s] Port 5672 not responding yet..." -ForegroundColor Gray
            Start-Sleep -Seconds $checkInterval
        }
    }
    
    if ($isReady) {
        Write-Host ""
        Write-Host "  ✓ Port 5672 is responding!" -ForegroundColor Green
        Write-Host ""
        Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
        Write-Host "║                           EMULATOR READY                                  ║" -ForegroundColor Green
        Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
        Write-Host ""
        Write-Host "  Total startup time: $elapsedSeconds seconds" -ForegroundColor Cyan
        Write-Host "  Ready at: $(Get-Date -Format 'HH:mm:ss.fff')" -ForegroundColor Cyan
        Write-Host ""
    }
    else {
        Write-Host ""
        Write-Host "  ✗ Port 5672 did not respond within $maxWait seconds" -ForegroundColor Red
        Write-Host ""
        Write-Host "Checking container status:" -ForegroundColor Yellow
        docker-compose ps
        Write-Host ""
        Write-Host "Container logs (last 50 lines):" -ForegroundColor Yellow
        docker-compose logs --tail=50
    }
}
catch {
    Write-Error "❌ Failed to start emulator: $_"
    exit 1
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  If startup took more than 30 seconds, we need to increase the wait time in run-dashboard.ps1" -ForegroundColor Gray
Write-Host "═══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
