#!/usr/bin/env pwsh

Write-Host "Testing Ctrl+C handling..."
Write-Host "Press Ctrl+C in THIS window (the main PowerShell window)"
Write-Host ""

$processes = @()

# Start a dummy process
Write-Host "Starting a test process in a separate window..."
$proc = Start-Process -FilePath 'pwsh' -ArgumentList @('-NoExit', '-Command', 'Write-Host "Test process running. You can close this window."; while($true) { Start-Sleep -Seconds 1 }') -PassThru
$processes += $proc
Write-Host "Process started (PID: $($proc.Id))"
Write-Host ""

# Now try to catch Ctrl+C
Write-Host "Waiting for Ctrl+C in the MAIN window..."
Write-Host ""

$shutdownInProgress = $false

function Cleanup {
    if ($shutdownInProgress) { return }
    $shutdownInProgress = $true
    
    Write-Host ""
    Write-Host "CLEANUP CALLED!"
    Write-Host "Killing process $($proc.Id)..."
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "Done!"
    exit 0
}

# Method 1: Try trap
trap {
    Write-Host "TRAP FIRED!"
    Cleanup
}

# Method 2: Try catching exception
try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
catch {
    Write-Host "CATCH FIRED: $_"
    Cleanup
}
finally {
    Write-Host "FINALLY FIRED"
}
