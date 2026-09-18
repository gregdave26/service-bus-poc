#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the complete Service Bus POC scenario locally with emulator and verification.

.DESCRIPTION
    This script runs the entire Service Bus POC end-to-end:
    
    1. Starts Azure Service Bus emulator via Docker Compose
    2. Configures the emulator topology (topic, subscriptions, filters)
    3. Starts all 7 applications as background processes
    4. Runs the Verifier to validate all scenarios
    5. Collects and reports results
    6. Cleans up (stops emulator, kills processes)
    
    Use this for:
    - CI/CD validation
    - Local testing before PR
    - Demonstration of complete system
    - Troubleshooting end-to-end flow

.PARAMETER RunTime
    How long to let applications run before verification (seconds). Default: 30

.PARAMETER NoCleanup
    Don't stop emulator or kill processes after completion. Default: $false

.PARAMETER Verbose
    Show detailed output from each application. Default: $false

.EXAMPLE
    # Run full POC with verification
    .\scripts\run-local-poc.ps1

.EXAMPLE
    # Run with extra time for slow machines, keep containers running
    .\scripts\run-local-poc.ps1 -RunTime 60 -NoCleanup

.NOTES
    Author: Service Bus POC Team
    Requires: PowerShell 7+, .NET 10 SDK, Docker Desktop
    Environment: Local development only
#>

[CmdletBinding()]
param(
    [int]$RunTime = 30,
    
    [bool]$NoCleanup = $false,
    
    [bool]$Verbose = $false
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $projectRoot 'src'
$infraPath = Join-Path $projectRoot 'infra'
$logsPath = Join-Path $projectRoot 'logs'
. (Join-Path $PSScriptRoot 'wait-for-servicebus-emulator.ps1')

# Ensure logs directory exists
if (-not (Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath | Out-Null
}

$timestamp = Get-Date -Format 'ddMMyyyy-HHmmss'
$logFile = Join-Path $logsPath "poc-run-$timestamp.log"

Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                SERVICE BUS POC - LOCAL RUN WITH VERIFICATION               ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Green
Write-Host "  Run time: $RunTime seconds"
Write-Host "  Cleanup: $(if ($NoCleanup) { 'No' } else { 'Yes' })"
Write-Host "  Verbose: $(if ($Verbose) { 'Yes' } else { 'No' })"
Write-Host "  Log file: $logFile"
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

try {
    $null = docker --version
    Write-Host "  ✓ Docker available"
}
catch {
    Write-Error "❌ Docker not found or not running"
    exit 1
}

$solutionPath = Join-Path $srcPath 'ServiceBusPoc.slnx'
if (-not (Test-Path $solutionPath)) {
    Write-Error "❌ Solution file not found: $solutionPath"
    exit 1
}

Write-Host ""

# Step 1: Start emulator
Write-Host "STEP 1: Starting emulator topology..." -ForegroundColor Yellow

$composePath = Join-Path $infraPath 'servicebus' 'compose.yaml'
if (-not (Test-Path $composePath)) {
    Write-Error "❌ Docker Compose file not found: $composePath"
    exit 1
}

$composeLogs = Join-Path $logsPath "emulator-$timestamp.log"

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
    
    Wait-ServiceBusEmulatorReady -ComposePath $composePath
}
catch {
    Write-Error "❌ Failed to start emulator: $_"
    exit 1
}
finally {
    Pop-Location
}

Write-Host ""

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

$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true"
$env:ServiceBus__TopicName = "contact.events"
$env:DOTNET_Environment = "Development"
$env:DOTNET_LOG_LEVEL = if ($Verbose) { "Debug" } else { "Information" }

Write-Host "  ✓ Environment configured"

Write-Host ""

# Step 4: Start all consumer applications
Write-Host "STEP 4: Starting consumer applications..." -ForegroundColor Yellow

$processes = @()
$apps = @(
    @{ Name = 'DigitalChannels'; Project = 'ServiceBusPoc.DigitalChannels' }
    @{ Name = 'Insurance'; Project = 'ServiceBusPoc.Insurance' }
    @{ Name = 'ParksResorts'; Project = 'ServiceBusPoc.ParksResorts' }
    @{ Name = 'Carwash'; Project = 'ServiceBusPoc.Carwash' }
)

foreach ($app in $apps) {
    $projectPath = Join-Path $srcPath $app.Project
    if (Test-Path $projectPath) {
        $projectFile = Join-Path $projectPath "$($app.Project).csproj"
        $appNameLower = $app.Name.ToLower()
        
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

        $processes += $process
        Write-Host "  ➜ $($app.Name) [PID: $pidNumber]"
        Write-Host "    📌 Logs: logs/$appNameLower-$serviceTimestamp-stdout.log | stderr.log" -ForegroundColor Gray
        Start-Sleep -Milliseconds 500
    }
}

if ($processes.Count -eq 0) {
    Write-Error "❌ No consumer applications found"
    exit 1
}

Write-Host ""

# Step 5: Run verification/scenarios
Write-Host "STEP 5: Running verification scenarios..." -ForegroundColor Yellow
Write-Host "  Waiting $RunTime seconds for setup..."
Start-Sleep -Seconds $RunTime

$verifierPath = Join-Path $srcPath 'ServiceBusPoc.Verifier'
if (Test-Path $verifierPath) {
    Write-Host "  Starting Verifier..."
    Push-Location $verifierPath
    try {
        $verifierOutput = & dotnet run --configuration Debug 2>&1
        Add-Content -Path $logFile -Value $verifierOutput
        
        if ($Verbose) {
            Write-Host $verifierOutput
        }
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Verification passed"
        }
        else {
            Write-Host "  ⚠️ Verification had issues (see logs)"
        }
    }
    catch {
        Write-Warning "  ⚠️ Verifier failed: $_"
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Warning "  ⚠️ Verifier project not found"
}

Write-Host ""

# Step 6: Collect results
Write-Host "STEP 6: Collecting results..." -ForegroundColor Yellow

$reportFile = Join-Path $logsPath "poc-report-$timestamp.txt"
$report = @"
═══════════════════════════════════════════════════════════════════════════
SERVICE BUS POC - EXECUTION REPORT
═══════════════════════════════════════════════════════════════════════════

Execution Time: $(Get-Date)
Run Duration: $RunTime seconds
Log File: $logFile
Project Root: $projectRoot

SERVICES STARTED:
"@

foreach ($process in $processes) {
    $state = if (-not $process.HasExited) { 'Running' } else { "Exited ($($process.ExitCode))" }
    $report += "`n  $state : PID $($process.Id)"
}

$report += "`n`nDETAILS:`n"
$report += "  See full output in: $logFile`n"
$report += "═══════════════════════════════════════════════════════════════════════════`n"

$report | Out-File -FilePath $reportFile

Write-Host $report

Write-Host ""

# Cleanup if requested
if (-not $NoCleanup) {
    Write-Host "CLEANUP: Stopping services..." -ForegroundColor Yellow
    
    foreach ($process in $processes) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id
        }
    }
    Write-Host "  ✓ Consumer processes stopped"
    
    # Stop emulator
    Push-Location (Split-Path $composePath)
    try {
        docker-compose down | Out-Null
        Write-Host "  ✓ Emulator stopped"
    }
    catch {
        Write-Warning "  ⚠️ Failed to stop emulator"
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "CLEANUP: Skipped (applications still running)" -ForegroundColor Yellow
    Write-Host "To stop services manually, run:"
    Write-Host "  Stop-Process -Id <process-id>"
    Write-Host "  docker-compose -f $composePath down"
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "POC EXECUTION COMPLETE" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "Logs: $logFile"
Write-Host "Report: $reportFile"
Write-Host ""
