# Start-BDD-Tests.ps1
# Starts Aspire, UI dev server, and runs BDD tests
# Run from repo root: .\test-scripts\start-bdd-tests.ps1
# Avoids starting duplicate processes if already running

# Get the repo root by going up from test-scripts directory
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)

# Check if Aspire is already running
$aspireProcess = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match "AppHost" }
if (-not $aspireProcess) {
  Write-Host "Starting Aspire..."
  Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$repoRoot'; .\scripts\run-aspire.ps1 -NoBrowser"
  Start-Sleep -Seconds 5
} else {
  Write-Host "✓ Aspire is already running"
}

# Check if Vite dev server is already running (Node process with vite)
$viteProcess = Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match "vite" }
if (-not $viteProcess) {
  Write-Host "Building and starting UI (Vite)..."
  Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$repoRoot\ui'; npm run dev"
  Start-Sleep -Seconds 10
} else {
  Write-Host "✓ Vite dev server is already running"
}

Write-Host "Running BDD tests..."
cd $repoRoot\ui
npm run test:bdd
