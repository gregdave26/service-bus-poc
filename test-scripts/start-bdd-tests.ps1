# Start-BDD-Tests.ps1
# Starts Aspire, UI dev server, and runs BDD tests
# Run from repo root: .\test-scripts\start-bdd-tests.ps1

# Get the repo root by going up from test-scripts directory
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)

Write-Host "Starting Aspire..."
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$repoRoot'; .\scripts\run-aspire.ps1 -NoBrowser"

Start-Sleep -Seconds 5

Write-Host "Building and starting UI..."
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$repoRoot\ui'; npm run dev"

Start-Sleep -Seconds 10

Write-Host "Running BDD tests..."
cd $repoRoot\ui
npm run test:bdd test/features/card-validity-and-terms-gating.feature
