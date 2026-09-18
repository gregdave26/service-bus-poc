#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the Service Bus POC application in debug mode with all consumers and producer.

.DESCRIPTION
    This script orchestrates starting the entire Service Bus POC application locally
    in debug mode. It:
    
    1. Validates prerequisites (.NET 10, Docker Desktop)
    2. Starts Azure Service Bus emulator via Docker Compose
    3. Applies emulator topology configuration
    4. Starts all 7 console applications as background jobs
    5. Displays unified logging output
    6. Handles graceful shutdown (Ctrl+C)
    
    Each application runs as a separate PowerShell job for independent management.
    All applications read configuration from environment variables.

.PARAMETER Role
    Start only specific role(s). Options: producer, digitalchannels, insurance, 
    parksresorts, carwash, verifier, all. Default: 'all'

.PARAMETER LogPath
    Path to save combined logs. Default: './logs/debug-run-{timestamp}.log'

.PARAMETER Emulator
    Start Azure Service Bus emulator. Default: $true

.PARAMETER WaitSeconds
    Seconds to wait for emulator to be ready. Default: 15

.EXAMPLE
    # Start full POC in debug mode
    .\scripts\debug-run.ps1

.EXAMPLE
    # Start only insurance consumer and digital channels for focused testing
    .\scripts\debug-run.ps1 -Role insurance,digitalchannels

.EXAMPLE
    # Start without emulator (assume external Service Bus)
    .\scripts\debug-run.ps1 -Emulator $false

.NOTES
    Author: Service Bus POC Team
    Requires: PowerShell 7+, .NET 10 SDK, Docker Desktop
    Environment: Local development only (uses emulator)
#>

[CmdletBinding()]
param(
    [ValidateSet('producer', 'digitalchannels', 'insurance', 'parksresorts', 'carwash', 'verifier', 'all')]
    [string[]]$Role = @('all'),
    
    [string]$LogPath,
    
    [bool]$Emulator = $true,
    
    [int]$WaitSeconds = 15
)

# Configuration
$ErrorActionPreference = 'Stop'
$VerbosePreference = 'Continue'

$projectRoot = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $projectRoot 'src'
$logsPath = Join-Path $projectRoot 'logs'
. (Join-Path $PSScriptRoot 'wait-for-servicebus-emulator.ps1')

# Default log path if not provided
if (-not $LogPath) {
    $timestamp = Get-Date -Format 'ddMMyyyy-HHmmss'
    $LogPath = Join-Path $logsPath "debug-run-$timestamp.log"
}

# Ensure logs directory exists
if (-not (Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath | Out-Null
}

# Expand 'all' role
if ($Role -contains 'all') {
    $Role = @('digitalchannels', 'insurance', 'parksresorts', 'carwash', 'producer', 'verifier')
}

Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                 SERVICE BUS POC - DEBUG START                              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Green
Write-Host "  Roles to start: $($Role -join ', ')"
Write-Host "  Emulator: $Emulator"
Write-Host "  Log file: $LogPath"
Write-Host "  Project root: $projectRoot"
Write-Host ""

# Prerequisites check
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check .NET 10 SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "  ✓ .NET SDK: $dotnetVersion"
}
catch {
    Write-Error "❌ .NET 10 SDK not found. Install from https://dotnet.microsoft.com/download"
    exit 1
}

# Check Docker (if emulator needed)
if ($Emulator) {
    try {
        $dockerVersion = docker --version
        Write-Host "  ✓ Docker: $dockerVersion"
    }
    catch {
        Write-Error "❌ Docker Desktop not found or not running. Install from https://www.docker.com/products/docker-desktop"
        exit 1
    }
}

# Check solution exists
$solutionPath = Join-Path $srcPath 'ServiceBusPoc.slnx'
if (-not (Test-Path $solutionPath)) {
    Write-Error "❌ Solution file not found: $solutionPath"
    exit 1
}

Write-Host ""

# Start emulator if requested
$emulatorJobName = 'ServiceBusEmulator'
if ($Emulator) {
    Write-Host "Starting Azure Service Bus emulator..." -ForegroundColor Yellow
    
    $composePath = Join-Path $projectRoot 'infra' 'servicebus' 'compose.yaml'
    if (-not (Test-Path $composePath)) {
        Write-Warning "⚠️ Docker Compose file not found: $composePath"
        Write-Warning "   Skipping emulator startup. Ensure Service Bus is accessible."
    }
    else {
        # Check if containers already running
        $runningContainers = docker-compose -f $composePath ps --quiet 2>$null
        if ($runningContainers) {
            Write-Host "  ℹ Containers already running. Restarting..." -ForegroundColor Cyan
            docker-compose -f $composePath down | Out-Null
        }
        
        # Start emulator
        Push-Location (Split-Path $composePath)
        try {
            $null = docker-compose up -d
            Write-Host "  ✓ Emulator containers started"
            
            Wait-ServiceBusEmulatorReady -ComposePath $composePath -InitialDelaySeconds $WaitSeconds
        }
        catch {
            Write-Error "❌ Failed to start emulator: $_"
            exit 1
        }
        finally {
            Pop-Location
        }
    }
}

# Build solution
Write-Host ""
Write-Host "Building solution..." -ForegroundColor Yellow
try {
    dotnet build $solutionPath --configuration Debug --verbosity quiet
    Write-Host "  ✓ Build successful"
}
catch {
    Write-Error "❌ Build failed: $_"
    exit 1
}

# Set environment variables for local emulator
Write-Host ""
Write-Host "Setting environment variables..." -ForegroundColor Yellow

# Service Bus Configuration
# These values should match your emulator setup
$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true"
$env:ServiceBus__Namespace = "localhost"
$env:ServiceBus__TopicName = "contact.events"
$env:DOTNET_Environment = "Development"
$env:DOTNET_LOG_LEVEL = "Information"

Write-Host "  ✓ Environment variables configured"
Write-Host ""

# Helper function to start an app without PowerShell remoting jobs.
# Start-Job is unavailable in constrained-language sessions.
# -NoNewWindow keeps everything in this console; output is tailed below instead
# of being left to sit unlabeled in a separate window.
function Start-App {
    param(
        [string]$AppName,
        [string]$ProjectName
    )
    
    Write-Host "  ➜ $AppName" -ForegroundColor Cyan

    $projectPath = Join-Path $srcPath $ProjectName
    $projectFile = Join-Path $projectPath "$ProjectName.csproj"
    $appNameLower = $AppName.ToLower()
    
    $serviceTimestamp = Get-Date -Format 'ddMMyyyy-HHmmss'
    $stdoutLog = Join-Path $logsPath "$appNameLower-$serviceTimestamp-stdout.log"
    $stderrLog = Join-Path $logsPath "$appNameLower-$serviceTimestamp-stderr.log"

    $process = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList @('run', '--configuration', 'Debug', '--project', $projectFile) `
        -WorkingDirectory $projectRoot `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog `
        -NoNewWindow `
        -PassThru
    
    $pidNumber = $process.Id
    Write-Host "      📌 Logs: logs/$appNameLower-$serviceTimestamp-stdout.log | stderr.log" -ForegroundColor Gray
    
    return $process
}

# Color palette so each app's tailed output is visually distinguishable.
$appColors = @('Cyan', 'Magenta', 'Yellow', 'Green', 'Blue', 'DarkCyan')

# Tracks the byte offset already read from each log file so the tail loop
# only prints newly appended lines.
$tailState = @{}

function Initialize-Tail {
    param([string]$AppName, [string]$OutputPath, [string]$ErrorPath, [string]$Color)

    $tailState[$AppName] = [PSCustomObject]@{
        OutputPath  = $OutputPath
        ErrorPath   = $ErrorPath
        Color       = $Color
        OutPosition = 0
        ErrPosition = 0
    }
}

function Write-TailedOutput {
    foreach ($appName in $tailState.Keys) {
        $state = $tailState[$appName]

        foreach ($stream in @(
                @{ Path = $state.OutputPath; PosProp = 'OutPosition'; Label = '' }
                @{ Path = $state.ErrorPath; PosProp = 'ErrPosition'; Label = 'ERR ' }
            )) {
            if (-not (Test-Path $stream.Path)) { continue }

            $fileLength = (Get-Item $stream.Path).Length
            $currentPos = $state.($stream.PosProp)
            if ($fileLength -le $currentPos) { continue }

            $streamReader = [System.IO.File]::Open($stream.Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
            try {
                $streamReader.Seek($currentPos, [System.IO.SeekOrigin]::Begin) | Out-Null
                $reader = New-Object System.IO.StreamReader($streamReader)
                while (-not $reader.EndOfStream) {
                    $line = $reader.ReadLine()
                    Write-Host "[$appName] $($stream.Label)$line" -ForegroundColor $state.Color
                }
                $state.($stream.PosProp) = $streamReader.Position
            }
            finally {
                $streamReader.Dispose()
            }
        }
    }
}

# Start applications
Write-Host "Starting applications in debug mode..." -ForegroundColor Yellow
Write-Host ""

$processes = @()
$appProjects = @{
    'DigitalChannels'  = 'ServiceBusPoc.DigitalChannels'
    'Insurance'        = 'ServiceBusPoc.Insurance'
    'ParksResorts'     = 'ServiceBusPoc.ParksResorts'
    'Carwash'          = 'ServiceBusPoc.Carwash'
    'Producer'         = 'ServiceBusPoc.Producer'
    'Verifier'         = 'ServiceBusPoc.Verifier'
}
$appSubscriptions = @{
    'DigitalChannels' = 'digital-channels'
    'Insurance'       = 'insurance'
    'ParksResorts'    = 'parks-resorts'
    'Carwash'         = 'carwash'
}

foreach ($appName in $Role) {
    $projectName = $appProjects[$appName]
    if (-not $projectName) {
        Write-Warning "  ⚠ Unknown role: $appName"
        continue
    }
    
    $projectPath = Join-Path $srcPath $projectName
    if (-not (Test-Path $projectPath)) {
        Write-Warning "  ⚠ Project not found: $projectPath"
        continue
    }
    
    $outputPath = Join-Path $logsPath "$appName-$timestamp.stdout.log"
    $errorPath = Join-Path $logsPath "$appName-$timestamp.stderr.log"
    $color = $appColors[$processes.Count % $appColors.Count]
    Initialize-Tail -AppName $appName -OutputPath $outputPath -ErrorPath $errorPath -Color $color

    $env:ServiceBus__SubscriptionName = $appSubscriptions[$appName]
    $process = Start-App -AppName $appName -ProjectName $projectName
    $env:ServiceBus__SubscriptionName = $null
    $processes += $process
    Start-Sleep -Milliseconds 500 # Stagger startup
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                    ALL APPLICATIONS STARTED                                 ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host ""
Write-Host "Running processes:" -ForegroundColor Yellow
$processes | Select-Object Id, ProcessName | Format-Table

Write-Host ""
Write-Host "Log file: $LogPath" -ForegroundColor Cyan
Write-Host "Live output below is tagged per app: [AppName] message" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop all applications" -ForegroundColor Yellow
Write-Host ""

Add-Content -Path $LogPath -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Debug session started"
Add-Content -Path $LogPath -Value "Application output is saved as *.stdout.log and *.stderr.log in $logsPath."

try {
    while ($true) {
        $runningProcesses = @($processes | Where-Object { -not $_.HasExited })
        
        if ($runningProcesses.Count -eq 0) {
            Write-Host ""
            Write-Warning "No application processes are running. Debug session ended."
            break
        }

        Write-TailedOutput
        Start-Sleep -Milliseconds 500
    }
}
catch [System.OperationCanceledException] {
    # Ctrl+C pressed
}
finally {
    Write-TailedOutput  # flush any output written just before shutdown
    Write-Host ""
    Write-Host "Shutting down..." -ForegroundColor Yellow
    
    foreach ($process in $processes) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id
        }
    }
    
    # Stop emulator if we started it
    if ($Emulator) {
        Write-Host "Stopping emulator..." -ForegroundColor Yellow
        $composePath = Join-Path $projectRoot 'infra' 'servicebus' 'compose.yaml'
        if (Test-Path $composePath) {
            Push-Location (Split-Path $composePath)
            try {
                docker-compose down | Out-Null
                Write-Host "  ✓ Emulator stopped"
            }
            catch {
                Write-Warning "  ⚠ Failed to stop emulator: $_"
            }
            finally {
                Pop-Location
            }
        }
    }
    
    Write-Host ""
    Write-Host "Debug session complete." -ForegroundColor Green
    Write-Host "Logs saved to: $LogPath" -ForegroundColor Green
}
