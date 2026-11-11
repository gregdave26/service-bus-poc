# Start-BDD-Tests.ps1
# Starts Aspire, UI dev server, and runs BDD tests
# Run from repo root: .\test-scripts\start-bdd-tests.ps1
# 
# Usage:
#   .\test-scripts\start-bdd-tests.ps1                    # Use cached builds, kill processes if fresh build
#   .\test-scripts\start-bdd-tests.ps1 -FreshBackend      # Clean rebuild .NET backend
#   .\test-scripts\start-bdd-tests.ps1 -FreshUI           # Clean rebuild UI (npm ci, rebuild)
#   .\test-scripts\start-bdd-tests.ps1 -FreshBackend -FreshUI -NoKill  # Full clean build, don't kill processes
#   .\test-scripts\start-bdd-tests.ps1 -NoKill            # Keep existing processes running

param(
  [switch]$FreshBackend = $false,
  [switch]$FreshUI = $false,
  [switch]$NoKill = $false
)

# Get the repo root by going up from test-scripts directory
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)

# Kill existing processes if doing fresh builds (unless -NoKill is specified)
if (($FreshBackend -or $FreshUI) -and -not $NoKill) {
  Write-Host "Stopping existing processes..."
  Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match "AppHost" } | Stop-Process -Force
  Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match "vite" } | Stop-Process -Force
  Start-Sleep -Seconds 2
}

# Fresh backend build if requested
if ($FreshBackend) {
  Write-Host "Cleaning and rebuilding backend..."
  cd $repoRoot
  dotnet clean
  dotnet build
  Write-Host "✓ Backend rebuild complete"
}

# Fresh UI build if requested
if ($FreshUI) {
  Write-Host "Cleaning and rebuilding UI..."
  cd $repoRoot\ui
  Remove-Item -Path "node_modules" -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -Path ".vite" -Recurse -Force -ErrorAction SilentlyContinue
  npm ci  # Clean install from package-lock.json
  Write-Host "✓ UI rebuild complete"
}

# Rebuild step definition files
Write-Host "Rebuilding step definition files..."
cd $repoRoot\ui
$stepFiles = @(Get-ChildItem -Path "test/steps" -Filter "*.steps.js" -ErrorAction SilentlyContinue)

if ($stepFiles.Count -gt 0) {
  foreach ($file in $stepFiles) {
    Write-Host "  • Rebuilding $($file.Name)..."
    # Touch the file to update its modification time (triggers Cucumber reload)
    (Get-Item $file.FullName).LastWriteTime = Get-Date
  }
  Write-Host "✓ Step definition files rebuilt"
} else {
  Write-Host "  ⚠️  No step definition files found in test/steps"
}

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
