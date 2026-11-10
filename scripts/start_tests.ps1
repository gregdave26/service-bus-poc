# Start-Tests.ps1
Write-Host "Starting Aspire..."
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd 'C:\Users\dg15938\development\service-bus-poc.worktrees\digital-site-tab-adyen-component'; .\scripts\run-aspire.ps1 -NoBrowser"

Start-Sleep -Seconds 5

Write-Host "Building and starting UI..."
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd 'C:\Users\dg15938\development\service-bus-poc.worktrees\digital-site-tab-adyen-component\ui'; npm run dev"

Start-Sleep -Seconds 10

Write-Host "Running BDD tests..."
cd C:\Users\dg15938\development\service-bus-poc.worktrees\digital-site-tab-adyen-component\ui
npm run test:bdd test/features/card-validity-and-terms-gating.feature