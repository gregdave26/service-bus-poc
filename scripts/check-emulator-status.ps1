#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Diagnostic script to check Service Bus emulator status and connectivity.

.DESCRIPTION
    Verifies:
    - Docker is running
    - Emulator container is running
    - Port 5672 is listening
    - AMQP protocol is responding
#>

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    SERVICE BUS EMULATOR DIAGNOSTICS                        ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check 1: Docker is running
Write-Host "CHECK 1: Docker Status" -ForegroundColor Yellow
try {
    $version = docker version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Docker is running"
    } else {
        Write-Host "  ✗ Docker is NOT responding"
        Write-Host "    Action: Start Docker Desktop"
        exit 1
    }
} catch {
    Write-Host "  ✗ Docker command failed: $_"
    exit 1
}

Write-Host ""

# Check 2: Containers running
Write-Host "CHECK 2: Emulator Containers" -ForegroundColor Yellow
$containers = docker-compose -f infra/servicebus/compose.yaml ps --quiet 2>&1
if ($LASTEXITCODE -eq 0 -and $containers) {
    $containerList = docker-compose -f infra/servicebus/compose.yaml ps 2>&1
    Write-Host "  ✓ Containers are running:"
    $containerList | ForEach-Object { Write-Host "    $_" }
} else {
    Write-Host "  ✗ No containers running"
    Write-Host "    Action: Run .\scripts\run-dashboard.ps1"
    exit 1
}

Write-Host ""

# Check 3: Port 5672 is listening
Write-Host "CHECK 3: Port 5672 Listening" -ForegroundColor Yellow
$socket = $null
try {
    $socket = New-Object System.Net.Sockets.TcpClient
    $socket.Connect('localhost', 5672)
    Write-Host "  ✓ Port 5672 is accepting TCP connections"
    $socket.Close()
} catch {
    Write-Host "  ✗ Port 5672 NOT accepting connections: $($_.Exception.Message)"
    Write-Host "    Action: Check emulator logs: docker logs servicebus-emulator"
    exit 1
} finally {
    if ($socket) {
        $socket.Dispose()
    }
}

Write-Host ""

# Check 4: AMQP protocol responding
Write-Host "CHECK 4: AMQP Protocol Response" -ForegroundColor Yellow
try {
    $socket = New-Object System.Net.Sockets.TcpClient
    $socket.ReceiveTimeout = 2000
    $socket.SendTimeout = 2000
    $socket.Connect('localhost', 5672)
    
    $networkStream = $socket.GetStream()
    
    # Send AMQP protocol header
    $amqpHeader = [byte[]]@(0x41, 0x4d, 0x51, 0x50, 0x00, 0x01, 0x00, 0x00)
    $networkStream.Write($amqpHeader, 0, $amqpHeader.Length)
    $networkStream.Flush()
    
    # Try to read response
    $response = New-Object byte[] 8
    $bytesRead = $networkStream.Read($response, 0, 8)
    
    if ($bytesRead -gt 0) {
        Write-Host "  ✓ AMQP protocol is responding"
        Write-Host "    Response bytes: $($response | ForEach-Object { '0x{0:X2}' -f $_ } | Join-String -Separator ' ')"
    } else {
        Write-Host "  ✗ No AMQP response received"
        Write-Host "    Action: Emulator started but AMQP not initialized yet"
        Write-Host "    Tip: Wait 20-30 more seconds and retry"
    }
    
    $socket.Close()
} catch {
    Write-Host "  ✗ AMQP protocol test failed: $($_.Exception.Message)"
    Write-Host "    Action: Try restarting emulator: docker-compose -f infra/servicebus/compose.yaml restart"
    exit 1
}

Write-Host ""

# Check 5: Emulator logs
Write-Host "CHECK 5: Emulator Container Logs (last 20 lines)" -ForegroundColor Yellow
$logs = docker logs servicebus-emulator 2>&1 | Tail -20
if ($logs) {
    Write-Host "  Container logs:"
    $logs | ForEach-Object { Write-Host "    $_" }
} else {
    Write-Host "  ✗ Could not retrieve logs"
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                          ALL CHECKS COMPLETE                               ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
