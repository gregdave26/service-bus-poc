#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the Service Bus POC dashboard and all services for interactive development.

.DESCRIPTION
    This script launches the complete Service Bus POC system with a browser dashboard:
    
    1. Starts Azure Service Bus emulator via Docker Compose
    2. Configures the emulator topology (topic, subscriptions, filters)
    3. Starts the Dashboard application (HTTP status + event publishing)
    4. Starts the Producer (publishes sample events every 3 seconds)
    5. Starts all 4 Consumers (listen and filter)
    6. Opens the dashboard in your default browser
    7. Keeps all processes running until Ctrl+C is pressed
    
    Use this for:
    - Interactive development and testing
    - Demonstrating the system to stakeholders
    - Testing subscription filters with real events
    - Verifying consumer message routing
    
    All output is displayed in real-time (not redirected to files).

.PARAMETER DashboardPort
    Port for the dashboard HTTP server. Default: 5100

.PARAMETER NoBrowser
    Don't automatically open the dashboard in a browser. Default: $false

.PARAMETER NoEmulator
    Don't start the emulator (assumes it's already running). Default: $false

.PARAMETER Terminals
    Launch each service in a separate terminal window. Default: $true

.EXAMPLE
    # Start everything with separate terminals (default)
    .\scripts\run-dashboard.ps1

.EXAMPLE
    # Start with inline output in single terminal
    .\scripts\run-dashboard.ps1 -Terminals $false

.EXAMPLE
    # Start without opening browser
    .\scripts\run-dashboard.ps1 -NoBrowser $true

.NOTES
    Author: Service Bus POC Team
    Requires: PowerShell 7+, .NET 10 SDK, Docker Desktop
    Environment: Local development only
    
    To stop: Press Ctrl+C in the terminal running this script.
    This will gracefully shut down all processes and containers.
#>

[CmdletBinding()]
param(
    [int]$DashboardPort = 5100,
    [bool]$NoBrowser = $false,
    [bool]$NoEmulator = $false,
    [bool]$Terminals = $true
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $projectRoot 'src'
$infraPath = Join-Path $projectRoot 'infra'
$logsPath = Join-Path $projectRoot 'logs'

# Ensure logs directory exists
if (-not (Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath | Out-Null
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║          SERVICE BUS POC - DASHBOARD + SERVICES (Interactive)              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

Write-Host "Configuration:" -ForegroundColor Green
Write-Host "  Dashboard port: $DashboardPort"
Write-Host "  Dashboard URL: http://localhost:$DashboardPort"
Write-Host "  Auto-open browser: $(if ($NoBrowser) { 'No' } else { 'Yes' })"
Write-Host "  Start emulator: $(if ($NoEmulator) { 'No (assumed running)' } else { 'Yes' })"
Write-Host "  Launch mode: $(if ($Terminals) { 'Separate terminals' } else { 'Inline (single console)' })"
Write-Host ""

# Prerequisites check
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
try {
    $null = dotnet --version
    Write-Host "  ✓ .NET SDK available"
}
catch {
    Write-Error "❌ .NET SDK not found"
    exit 1
}

if (-not $NoEmulator) {
    try {
        $null = docker --version
        Write-Host "  ✓ Docker available"
    }
    catch {
        Write-Error "❌ Docker not found or not running"
        exit 1
    }
}

$solutionPath = Join-Path $srcPath 'ServiceBusPoc.slnx'
if (-not (Test-Path $solutionPath)) {
    Write-Error "❌ Solution file not found: $solutionPath"
    exit 1
}

Write-Host ""

# Step 1: Start emulator (optional)
if (-not $NoEmulator) {
    Write-Host "STEP 1: Starting emulator topology..." -ForegroundColor Yellow
    
    $composePath = Join-Path $infraPath 'servicebus' 'compose.yaml'
    if (-not (Test-Path $composePath)) {
        Write-Error "❌ Docker Compose file not found: $composePath"
        exit 1
    }
    
    Push-Location (Split-Path $composePath)
    try {
        # Check for existing containers
        $existing = docker-compose ps --quiet 2>$null
        if ($existing) {
            Write-Host "  Stopping existing containers..." -ForegroundColor Gray
            docker-compose down | Out-Null
        }
        
        Write-Host "  Starting containers..."
        $null = docker-compose up -d
        Write-Host "  ✓ Emulator started"
        
        # Give SQL Server and Service Bus a moment to start listening
        # (Much shorter than before - the Producer will verify actual AMQP readiness)
        Write-Host "  Waiting 15 seconds for minimal initialization..."
        Start-Sleep -Seconds 15
        Write-Host "  ✓ Ready to proceed (Producer will verify AMQP readiness)"
    }
    catch {
        Write-Error "❌ Failed to start emulator: $_"
        exit 1
    }
    finally {
        Pop-Location
    }
    
    Write-Host ""
}

# Step 2: Build solution
Write-Host "STEP 2: Building solution..." -ForegroundColor Yellow
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

# Step 3: Set environment variables
Write-Host "STEP 3: Configuring environment..." -ForegroundColor Yellow

$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE"
$env:ServiceBus__Namespace = "localhost"
$env:ServiceBus__TopicName = "contact.events"
$env:Dashboard__Port = $DashboardPort
$env:Dashboard__Enabled = "true"
$env:Dashboard__Url = "http://localhost:$DashboardPort"
$env:Dashboard__HeartbeatIntervalSeconds = "5"
$env:Dashboard__RequestTimeoutSeconds = "5"
$env:Dashboard__OfflineAfterSeconds = "15"
$env:DOTNET_Environment = "Development"

# Enable console logging for all services
$env:LOGGING__CONSOLE__INCLUDEEXCEPTION = "true"
$env:LOGGING__CONSOLE__INCLUDESCOPES = "true"
$env:LOGGING__LOGGERENABLED = "true"

Write-Host "  ✓ Environment configured"
Write-Host ""

# Step 4: Start applications
Write-Host "STEP 4: Starting applications..." -ForegroundColor Yellow
Write-Host ""

$processes = @()

# Define the applications to start in order
$apps = @(
    @{ Name = 'Dashboard'; Project = 'ServiceBusPoc.Dashboard'; Description = 'Status & Event Publishing' }
    @{ Name = 'Producer'; Project = 'ServiceBusPoc.Producer'; Description = 'Event Publisher'; SubscriptionName = '' }
    @{ Name = 'DigitalChannels'; Project = 'ServiceBusPoc.DigitalChannels'; Description = 'Receives all events'; SubscriptionName = 'digital-channels' }
    @{ Name = 'Insurance'; Project = 'ServiceBusPoc.Insurance'; Description = 'Receives hasInsurance=true'; SubscriptionName = 'insurance' }
    @{ Name = 'ParksResorts'; Project = 'ServiceBusPoc.ParksResorts'; Description = 'Receives hasParksResorts=true'; SubscriptionName = 'parks-resorts' }
    @{ Name = 'Carwash'; Project = 'ServiceBusPoc.Carwash'; Description = 'Receives hasCarwashProduct=true'; SubscriptionName = 'carwash' }
)

function Start-AppWithLogging {
    param(
        [string]$Name,
        [string]$ProjectPath,
        [string]$ProjectFile,
        [string]$Description,
        [string]$SubscriptionName = ''
    )
    
    # Set subscription name if provided (for consumers)
    if ($SubscriptionName) {
        $env:ServiceBus__SubscriptionName = $SubscriptionName
    }
    
    try {
        if ($Terminals) {
            # Launch in separate terminal window
            $process = Start-Process `
                -FilePath 'dotnet' `
                -ArgumentList @('run', '--configuration', 'Debug', '--project', $ProjectFile) `
                -WorkingDirectory $projectRoot `
                -PassThru
        } else {
            # Launch inline with output redirected to console
            $process = Start-Process `
                -FilePath 'dotnet' `
                -ArgumentList @('run', '--configuration', 'Debug', '--project', $ProjectFile) `
                -WorkingDirectory $projectRoot `
                -PassThru `
                -NoNewWindow `
                -RedirectStandardOutput (Join-Path $logsPath "$Name-stdout.log") `
                -RedirectStandardError (Join-Path $logsPath "$Name-stderr.log")
            
            # Tail the log files in the console
            Write-Host "  ℹ Output redirected to logs/$Name-stdout.log and logs/$Name-stderr.log"
        }
        
        Write-Host "  ✓ $Name ($Description)" -ForegroundColor Green
        return $process
    }
    catch {
        Write-Host "  ✗ $Name failed to start: $_" -ForegroundColor Red
        return $null
    }
}

# Store the set of all started processes for cleanup
$allProcesses = @()

# Start each application
foreach ($app in $apps) {
    $projectPath = Join-Path $srcPath $app.Project
    if (Test-Path $projectPath) {
        $projectFile = Join-Path $projectPath "$($app.Project).csproj"
        $process = Start-AppWithLogging -Name $app.Name -ProjectPath $projectPath -ProjectFile $projectFile -Description $app.Description -SubscriptionName $app.SubscriptionName
        
        if ($null -ne $process) {
            $allProcesses += $process
            
            # Give each service time to start and initialize (stagger startup to avoid emulator connection contention)
            Start-Sleep -Seconds 2
        }
    }
    else {
        Write-Host "  ✗ $($app.Name) - project not found" -ForegroundColor Red
    }
}

Write-Host ""

# Step 5: Wait for dashboard to be ready
Write-Host "STEP 5: Waiting for dashboard to be ready..." -ForegroundColor Yellow
$dashboardReady = $false
$maxAttempts = 30
$attempt = 0

while (-not $dashboardReady -and $attempt -lt $maxAttempts) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$DashboardPort" -TimeoutSec 1 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $dashboardReady = $true
            Write-Host "  ✓ Dashboard is ready"
        }
    }
    catch {
        $attempt++
        Start-Sleep -Milliseconds 500
    }
}

if (-not $dashboardReady) {
    Write-Warning "  ⚠️ Dashboard did not respond (may still be starting)"
}

Write-Host ""

# Step 6: Open browser
if (-not $NoBrowser) {
    Write-Host "STEP 6: Opening dashboard in browser..." -ForegroundColor Yellow
    try {
        Start-Process "http://localhost:$DashboardPort"
        Write-Host "  ✓ Browser opened"
    }
    catch {
        Write-Warning "  ⚠️ Could not open browser automatically"
        Write-Host "     Open manually: http://localhost:$DashboardPort"
    }
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                            ✓ All services running                         ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "Dashboard:" -ForegroundColor Cyan
Write-Host "  📊 http://localhost:$DashboardPort"
Write-Host ""
Write-Host "Services:" -ForegroundColor Cyan
Write-Host "  🔵 Producer      - Publishing sample events every 3 seconds"
Write-Host "  🟢 Digital Channels - Receives all events (no filter)"
Write-Host "  🟢 Insurance      - Receives hasInsurance=true"
Write-Host "  🟢 Parks & Resorts - Receives hasParksResorts=true"
Write-Host "  🟢 Carwash        - Receives hasCarwashProduct=true"
Write-Host ""
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  1. Open http://localhost:$DashboardPort in your browser"
Write-Host "  2. Click 'Publish Event' and toggle capability flags"
Write-Host "  3. Watch the consumer consoles log filtered messages"
Write-Host "  4. See dashboard status update in real-time"
Write-Host "  5. Stop services: Press Ctrl+C here"
Write-Host ""

# Set up Ctrl+C handler for graceful shutdown
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
    Write-Host ""
    Write-Host "Shutting down services..." -ForegroundColor Yellow
    
    foreach ($process in $allProcesses) {
        if ($null -ne $process -and -not $process.HasExited) {
            try {
                Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
                Write-Host "  ✓ Stopped process $($process.Id)"
            }
            catch {
                Write-Host "  ⚠️ Could not stop process $($process.Id)"
            }
        }
    }
    
    Write-Host "  ✓ Services stopped"
    Write-Host ""
}

# Keep the script running
Write-Host "Press Ctrl+C to stop all services..."
Write-Host ""

# Monitor processes and keep script alive
while ($true) {
    Start-Sleep -Seconds 10
}
