# Start-BDD-Tests.ps1
# Runs the Cucumber BDD suite against the dashboard UI on http://localhost:5100.
# Run from the repo root: .\test-scripts\start-bdd-tests.ps1
#
# DEFAULT (no flags)
#   - No rebuild. Reuses the existing UI build (ui\public\dist) and backend build.
#   - Starts the backend (scripts\run-aspire.ps1) only if it is not already running.
#   - Starts the UI server only if the backend is already running and the UI is not.
#   - Starts the Vite dev server (ui, port 5173) only if it is not already running.
#   - Waits until the UI (5100) and Vite (5173) respond before running the tests.
#   - Builds the UI once only if ui\public\dist is missing.
#
# -Fresh backend | ui | all
#   - backend: stops the backend (Aspire AppHost), clean-builds it, and restarts it.
#   - ui:      stops the UI server and Vite, runs npm ci and a UI build, and restarts them.
#   - all:     both of the above.
#
# EXAMPLES
#   .\test-scripts\start-bdd-tests.ps1
#   .\test-scripts\start-bdd-tests.ps1 -Fresh ui
#   .\test-scripts\start-bdd-tests.ps1 -Fresh backend
#   .\test-scripts\start-bdd-tests.ps1 -Fresh all
#   .\test-scripts\start-bdd-tests.ps1 -ReadyTimeoutSeconds 600

param(
  [ValidateSet('backend', 'ui', 'all')]
  [string]$Fresh,
  [ValidateRange(30, 3600)]
  [int]$ReadyTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

$freshBackend = $Fresh -in @('backend', 'all')
$freshUi = $Fresh -in @('ui', 'all')

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$uiRoot = Join-Path $repoRoot 'ui'
$uiIndexPath = Join-Path $uiRoot 'public\dist\index.html'
$logsPath = Join-Path $repoRoot 'logs'
$uiTestUrl = 'http://localhost:5100/?tab=digitalSite'
$viteUrl = 'http://localhost:5173/'

function Get-BackendProcesses {
  Get-CimInstance -ClassName Win32_Process | Where-Object {
    $commandLine = $_.CommandLine
    $commandLine -and
    $commandLine -like "*$repoRoot*" -and
    $commandLine -like '*ServiceBusPoc.AppHost*' -and
    ($_.Name -in ('dotnet.exe', 'aspire.exe', 'ServiceBusPoc.AppHost.exe'))
  }
}

function Get-UiServerProcesses {
  Get-CimInstance -ClassName Win32_Process -Filter "Name = 'node.exe'" | Where-Object {
    $_.CommandLine -like "*$uiRoot*server.js*"
  }
}

function Get-RunAspireProcesses {
  Get-CimInstance -ClassName Win32_Process -Filter "Name = 'pwsh.exe'" | Where-Object {
    $_.CommandLine -like "*$repoRoot*" -and $_.CommandLine -like '*run-aspire.ps1*'
  }
}

function Stop-Processes {
  param([object[]]$Processes, [string]$Label)

  foreach ($process in $Processes) {
    Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
  }
  Write-Host "Stopped $($Processes.Count) $Label process(es)."
}
function Get-ViteProcesses {
  Get-CimInstance -ClassName Win32_Process -Filter "Name = 'node.exe'" | Where-Object {
    $_.CommandLine -like "*$uiRoot*vite.js*"
  }
}

function Test-Url {
  param([string]$Url)

  try {
    $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
    return $response.StatusCode -eq 200
  }
  catch {
    return $false
  }
}

function Get-CucumberRunProcesses {
  param([object[]]$All)

  $All | Where-Object {
    $_.CommandLine -and (
      ($_.Name -eq 'node.exe' -and $_.CommandLine -match 'cucumber') -or
      ($_.Name -eq 'cmd.exe' -and $_.CommandLine -match 'cucumber-js')
    )
  }
}

function Stop-CucumberProcesses {
  $all = @(Get-CimInstance -ClassName Win32_Process)
  $targets = @(Get-CucumberRunProcesses -All $all)
  Stop-Processes -Processes $targets -Label 'leftover Cucumber'
}

function Build-Backend {
  Write-Host 'Clean building backend...' -ForegroundColor Yellow
  Push-Location $repoRoot
  try {
    dotnet clean
    if ($LASTEXITCODE -ne 0) { throw 'dotnet clean failed.' }
    dotnet build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
  }
  finally {
    Pop-Location
  }
}

function Build-Ui {
  param([switch]$Clean)

  Write-Host 'Building UI...' -ForegroundColor Yellow
  Push-Location $uiRoot
  try {
    if ($Clean -or -not (Test-Path -LiteralPath 'node_modules')) {
      npm ci
      if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
    }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'UI build failed.' }
  }
  finally {
    Pop-Location
  }
}

function Start-Backend {
  Write-Host 'Starting backend (Aspire AppHost and dashboard UI)...' -ForegroundColor Yellow
  $command = "Set-Location -LiteralPath '$repoRoot'; .\scripts\run-aspire.ps1 -NoBrowser -NoBuild"
  Start-Process -FilePath 'pwsh' -ArgumentList '-NoExit', '-Command', $command | Out-Null
}

function Start-UiServer {
  Write-Host "Starting UI server on http://localhost:5100..." -ForegroundColor Yellow
  New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
  $env:PORT = '5100'
  Start-Process -FilePath 'node' `
    -ArgumentList 'server.js' `
    -WorkingDirectory $uiRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $logsPath 'bdd-ui-server.log') `
    -RedirectStandardError (Join-Path $logsPath 'bdd-ui-server-error.log') | Out-Null
}

function Start-ViteServer {
  Write-Host 'Starting Vite dev server on http://localhost:5173...' -ForegroundColor Yellow
  New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
  Start-Process -FilePath 'node' `
    -ArgumentList 'node_modules\vite\bin\vite.js', '--port', '5173', '--strictPort' `
    -WorkingDirectory $uiRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $logsPath 'bdd-vite.log') `
    -RedirectStandardError (Join-Path $logsPath 'bdd-vite-error.log') | Out-Null
}

function Wait-ForUrl {
  param([string]$Url, [string]$Name)

  Write-Host "Waiting for $Name at $Url (timeout ${ReadyTimeoutSeconds}s)..."
  $deadline = (Get-Date).AddSeconds($ReadyTimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    if (Test-Url -Url $Url) {
      Write-Host "$Name is ready." -ForegroundColor Green
      return
    }
    Start-Sleep -Seconds 3
  }
  throw "$Name did not respond at $Url within $ReadyTimeoutSeconds seconds. Check the backend window and logs\."
}

if ($freshBackend) {
  Stop-Processes -Processes @(@(Get-BackendProcesses) + @(Get-RunAspireProcesses)) -Label 'backend'
  Build-Backend
}
if ($freshUi) {
  Stop-Processes -Processes @(@(Get-UiServerProcesses) + @(Get-ViteProcesses)) -Label 'UI/Vite'
  Build-Ui -Clean
}
elseif (-not (Test-Path -LiteralPath $uiIndexPath)) {
  Write-Host 'UI build not found; building once...' -ForegroundColor Yellow
  Build-Ui
}

$backendStarted = $false
if (Get-BackendProcesses) {
  Write-Host 'Backend already running.' -ForegroundColor Green
}
else {
  Start-Backend
  $backendStarted = $true
}

if (-not $backendStarted -and -not (Test-Url -Url $uiTestUrl) -and -not (Get-UiServerProcesses)) {
  Start-UiServer
}

if (Get-ViteProcesses) {
  Write-Host 'Vite dev server already running.' -ForegroundColor Green
}
else {
  Start-ViteServer
}

Wait-ForUrl -Url $uiTestUrl -Name 'UI'
Wait-ForUrl -Url $viteUrl -Name 'Vite dev server'

Stop-CucumberProcesses

Write-Host ''
Write-Host '════════════════════════════════════════════'
Write-Host 'Running BDD tests...'
Write-Host '════════════════════════════════════════════'
Push-Location $uiRoot
try {
  npm run test:bdd
  exit $LASTEXITCODE
}
finally {
  Pop-Location
}
