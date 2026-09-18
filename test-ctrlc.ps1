#!/usr/bin/env pwsh
# Quick test to verify Ctrl+C handling works correctly

Write-Host "=== Ctrl+C Handling Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "This test verifies that Ctrl+C properly triggers cleanup"
Write-Host ""

$shutdownInProgress = $false

function Test-Cleanup {
    if ($shutdownInProgress) { return }
    $shutdownInProgress = $true
    
    Write-Host ""
    Write-Host "✅ CLEANUP CALLED SUCCESSFULLY!" -ForegroundColor Green
    Write-Host ""
    Write-Host "This means:" -ForegroundColor Green
    Write-Host "  1. Trap caught the Ctrl+C interrupt" -ForegroundColor Green
    Write-Host "  2. Invoke-Cleanup would now run" -ForegroundColor Green
    Write-Host "  3. All services would be killed" -ForegroundColor Green
    Write-Host "  4. Docker containers would be cleaned up" -ForegroundColor Green
    Write-Host "  5. Script would exit cleanly" -ForegroundColor Green
    Write-Host ""
}

# Define function FIRST
Write-Host "Defining Test-Cleanup function..." -ForegroundColor Yellow
Write-Host ""

# Now set up trap (after function is defined)
Write-Host "Setting up trap handler..." -ForegroundColor Yellow
trap {
    if (-not $shutdownInProgress) {
        Test-Cleanup
    }
    exit 0
}
Write-Host ""

Write-Host "Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Press Ctrl+C now to test the cleanup handler" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

try {
    while ($true) {
        Write-Host "Waiting... (Press Ctrl+C)" -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}
catch {
    Write-Host "Exception caught: $_" -ForegroundColor Red
    if (-not $shutdownInProgress) {
        Test-Cleanup
    }
    exit 1
}
finally {
    if (-not $shutdownInProgress) {
        Write-Host "Finally block executed" -ForegroundColor Yellow
        Test-Cleanup
    }
}
