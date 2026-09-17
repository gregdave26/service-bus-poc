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
    
    All output is captured in the logs/ folder while services continue running.

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

# Store all processes for cleanup on exit
$script:allProcesses = @()
$script:shutdownInProgress = $false

# Helper function to perform cleanup
function Invoke-Cleanup {
    if ($script:shutdownInProgress) { return }
    $script:shutdownInProgress = $true
    
    Write-Host ""
    Write-Host "⚠️  Shutting down services gracefully..." -ForegroundColor Yellow
    
    # Kill all tracked .NET processes and close their windows
    Write-Host ""
    Write-Host "Stopping .NET services:" -ForegroundColor Cyan
    foreach ($process in $script:allProcesses) {
        if ($null -ne $process -and -not $process.HasExited) {
            try {
                # Try to close window gracefully first
                $window = $process.MainWindowHandle
                if ($window -ne 0) {
                    $process.CloseMainWindow() | Out-Null
                    Start-Sleep -Milliseconds 500
                }
                
                # If still running, force kill
                if (-not $process.HasExited) {
                    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                }
                
                Write-Host "  ✓ Stopped $($process.Name) (PID: $($process.Id))"
            }
            catch {
                Write-Host "  ⚠️  Could not stop process $($process.Id)"
            }
        }
    }
    
    # Force-kill any remaining dotnet processes
    Write-Host ""
    Write-Host "Cleaning up remaining processes:" -ForegroundColor Cyan
    try {
        $remaining = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
        if ($remaining) {
            # Try graceful close first
            $remaining | ForEach-Object {
                if ($_.MainWindowHandle -ne 0) {
                    $_.CloseMainWindow() | Out-Null
                }
            }
            Start-Sleep -Milliseconds 500
            
            # Force kill what didn't close
            $remaining | Where-Object { -not $_.HasExited } | Stop-Process -Force -ErrorAction SilentlyContinue
            Write-Host "  ✓ Killed remaining dotnet processes and closed windows"
        }
    } catch {
        # No remaining processes
    }
    
    # Stop and remove Docker containers
    Write-Host ""
    Write-Host "Stopping Docker containers:" -ForegroundColor Cyan
    try {
        $infraPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent (Get-PSCallStack)[-1].ScriptName }
        $projectRoot = Split-Path -Parent $infraPath
        $composePath = Join-Path $projectRoot 'infra' 'servicebus'
        
        if (Test-Path $composePath) {
            Push-Location $composePath
            docker-compose down 2>&1 | ForEach-Object { Write-Host "    $_" }
            Pop-Location
            Write-Host "  ✓ Docker containers stopped and removed"
        }
    } catch {
        Write-Host "  ⚠️  Docker cleanup failed (Docker may not be running)"
    }
    
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                    ✓ Cleanup complete. Goodbye!                           ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    
}

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
        
        # Wait for AMQP port - SIMPLE AND ROBUST approach
        # The emulator needs: SQL Edge (15-30s) + Service Bus (10-20s) + AMQP ready (5-10s) = 45-60s minimum
        Write-Host "  Waiting for emulator AMQP port (5672) to become available..."
        
        # Start with a guaranteed fixed wait for initial startup
        Write-Host "    Phase 1: Initial startup (30 seconds guaranteed)..."
        Start-Sleep -Seconds 30
        
        # Then actively poll with timeout
        Write-Host "    Phase 2: Polling for AMQP readiness (up to 90 more seconds)..."
        $maxPollingSeconds = 90
        $found = $false
        
        for ($i = 1; $i -le $maxPollingSeconds; $i++) {
            $socket = $null
            try {
                $socket = New-Object System.Net.Sockets.TcpClient
                $socket.ConnectAsync('localhost', 5672) | Wait-Job -Timeout 2 | Out-Null
                
                if ($socket.Connected) {
                    Write-Host "  ✓ AMQP port 5672 is ready (after $((30 + $i))s total)" -ForegroundColor Green
                    $found = $true
                    $socket.Close()
                    break
                }
            } catch {
                # Connection attempt failed - expected for first 20-40 seconds
            } finally {
                if ($socket) {
                    $socket.Dispose()
                }
            }
            
            # Show progress every 10 seconds
            if (-not $found -and $i % 10 -eq 0) {
                Write-Host "    [$($i)/$maxPollingSeconds seconds of polling] Still waiting for port to respond..."
            }
            
            Start-Sleep -Seconds 1
        }
        
        # If polling succeeded, great! If not, we've still waited 60 seconds minimum
        if ($found) {
            Write-Host "  ✓ Port confirmed ready"
        } else {
            Write-Host "  ⚠️  Port not responding after polling, but proceeding anyway (emulator may still be initializing)"
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
        $appNameLower = $Name.ToLower()
        $timestamp = Get-Date -Format 'ddMMyyyy-HHmmss'
        
        # Use a timestamp in the log name so each service run is easy to identify.
        $launchArguments = @(
            '-NoProfile',
            '-Command',
            "& dotnet run --configuration Debug --project '$ProjectFile' 1> '$logsPath\$appNameLower-$timestamp-stdout.log' 2> '$logsPath\$appNameLower-$timestamp-stderr.log'"
        )

        $startOptions = @{
            FilePath = 'powershell'
            ArgumentList = $launchArguments
            WorkingDirectory = $projectRoot
            PassThru = $true
        }
        if (-not $Terminals) {
            $startOptions.NoNewWindow = $true
        }
        $process = Start-Process @startOptions

        $pidNumber = $process.Id
        $finalStdoutLog = Join-Path $logsPath "$appNameLower-$timestamp-stdout.log"
        $finalStderrLog = Join-Path $logsPath "$appNameLower-$timestamp-stderr.log"
        
        Write-Host "  ✓ $Name ($Description) [PID: $pidNumber]" -ForegroundColor Green
        Write-Host "    📌 Logs: logs/$appNameLower-$timestamp-stdout.log | stderr.log" -ForegroundColor Gray
        
        # Store the log file paths on the process object for later reference
        $process | Add-Member -NotePropertyName LogFiles -NotePropertyValue @{
            StdOut = $finalStdoutLog
            StdErr = $finalStderrLog
            Name = $appNameLower
        } -Force
        
        return $process
    }
    catch {
        Write-Host "  ✗ $Name failed to start: $_" -ForegroundColor Red
        return $null
    }
}

# Store the set of all started processes for cleanup (uses $script:allProcesses from trap)
$script:allProcesses = @()

# Start each application
foreach ($app in $apps) {
    $projectPath = Join-Path $srcPath $app.Project
    if (Test-Path $projectPath) {
        $projectFile = Join-Path $projectPath "$($app.Project).csproj"
        $process = Start-AppWithLogging -Name $app.Name -ProjectPath $projectPath -ProjectFile $projectFile -Description $app.Description -SubscriptionName $app.SubscriptionName
        
        if ($null -ne $process) {
            $script:allProcesses += $process
            
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

# Keep the script running and listen for Ctrl+C
Write-Host "Press Ctrl+C to stop all services..."
Write-Host ""

# Main monitoring loop.
# Treat Ctrl+C as a console key so PowerShell does not terminate the script
# before the application cleanup runs.
$previousTreatControlCAsInput = [Console]::TreatControlCAsInput
[Console]::TreatControlCAsInput = $true

try {
    while ($true) {
        if ($Host.UI.RawUI.KeyAvailable) {
            $key = $Host.UI.RawUI.ReadKey('AllowCtrlC,NoEcho,IncludeKeyDown')
            if ([int]$key.Character -eq 3) {
                Write-Host ""
                Write-Host "Ctrl+C detected. Stopping all services..." -ForegroundColor Yellow
                Invoke-Cleanup
                break
            }
        }

        # Check if any process has exited unexpectedly
        $running = $script:allProcesses | Where-Object { $null -ne $_ -and -not $_.HasExited }
        if (-not $running) {
            Write-Host ""
            Write-Host "All services have stopped." -ForegroundColor Yellow
            
            # Call cleanup before exiting
            if (-not $script:shutdownInProgress) {
                Invoke-Cleanup
            }
            break
        }
        
        Start-Sleep -Milliseconds 100
    }
}
catch {
    Write-Host ""
    Write-Host "Unexpected shutdown signal received. Stopping all services..." -ForegroundColor Yellow
    if (-not $script:shutdownInProgress) {
        Invoke-Cleanup
    }
}
finally {
    [Console]::TreatControlCAsInput = $previousTreatControlCAsInput
}
