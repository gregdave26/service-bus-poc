#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the Service Bus POC emulator and applications through .NET Aspire with dashboard UI.

.DESCRIPTION
    This script manages the complete Service Bus POC development environment:
    1. Starts the Azure Service Bus emulator via container runtime (Podman by default)
    2. Starts Aspire orchestration for .NET services
    3. Starts the Node.js dashboard UI on port 5100
    
    The dashboard UI provides the landing page to view the system status and publish events.
    Access the dashboard at: http://localhost:5100
    Access the Aspire dashboard at: https://localhost:<random-port>

.PARAMETER NoEmulator
    Do not start or stop the emulator; assume it is already running.

.PARAMETER NoBuild
    Skip the Aspire AppHost build and restore.

.PARAMETER NoUI
    Don't start the Node.js dashboard UI.

.PARAMETER NoBrowser
    Don't automatically open the dashboard in a browser.

.EXAMPLE
    .\scripts\run-aspire.ps1

.EXAMPLE
    .\scripts\run-aspire.ps1 -NoEmulator -NoBuild

.EXAMPLE
    .\scripts\run-aspire.ps1 -NoUI

.NOTES
    The Node.js dashboard UI is the primary landing page. It connects to the Aspire-managed services.
    To stop: Press Ctrl+C in this terminal.
#>

[CmdletBinding()]
param(
    [switch]$NoEmulator,
    [switch]$NoBuild,
    [switch]$NoUI,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$composePath = Join-Path $projectRoot 'infra\servicebus\compose.yaml'
$envFilePath = Join-Path $projectRoot '.env'
$appHostProject = Join-Path $projectRoot 'src\ServiceBusPoc.AppHost\ServiceBusPoc.AppHost.csproj'
$logsPath = Join-Path $projectRoot 'logs'
$appHostBuildProcess = $null
$emulatorStartedByScript = $false
. (Join-Path $PSScriptRoot 'container-runtime.ps1')
. (Join-Path $PSScriptRoot 'wait-for-servicebus-emulator.ps1')

function Import-EnvironmentFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmedLine = $line.Trim()
        if ($trimmedLine -and -not $trimmedLine.StartsWith('#') -and $trimmedLine.Contains('=')) {
            $name, $value = $trimmedLine.Split('=', 2)
            $environmentValue = $value.Trim()
            if (($environmentValue.StartsWith("'") -and $environmentValue.EndsWith("'")) -or
                ($environmentValue.StartsWith('"') -and $environmentValue.EndsWith('"'))) {
                $environmentValue = $environmentValue.Substring(1, $environmentValue.Length - 2)
            }

            [Environment]::SetEnvironmentVariable($name.Trim(), $environmentValue)
        }
    }
}

function Stop-Emulator {
    if ($NoEmulator -or -not $emulatorStartedByScript) {
        return
    }

    Write-Host 'Stopping Service Bus emulator...' -ForegroundColor Yellow
    Invoke-ContainerCompose -ComposePath $composePath -EnvFilePath $envFilePath -Arguments @('down')
}

function Resolve-AspireCli {
    $command = Get-Command aspire -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $packageRoot = Join-Path $env:USERPROFILE '.nuget\packages\aspire.cli.win-x64'
    # [Version] rejects prerelease suffixes, so parse the core version and let stable releases rank above prereleases.
    $latestPackage = Get-ChildItem -LiteralPath $packageRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [Version]($_.Name -split '-', 2)[0] }, { $_.Name -notmatch '-' } -Descending |
        Select-Object -First 1
    if ($null -ne $latestPackage) {
        $bundledCli = Join-Path $latestPackage.FullName 'tools\net10.0\win-x64\aspire.exe'
        if (Test-Path -LiteralPath $bundledCli -PathType Leaf) {
            return $bundledCli
        }
    }

    throw "Aspire CLI was not found. Install Aspire CLI or run 'dotnet run --project $appHostProject'."
}

$uiServerProcess = $null

function Start-DashboardUI {
    param([int]$DashboardPort = 5100)
    
    if ($NoUI) {
        Write-Host 'Skipping Node.js dashboard UI (--NoUI specified)' -ForegroundColor Gray
        return $null
    }

    $uiPath = Join-Path $projectRoot 'ui'
    $serverPath = Join-Path $uiPath 'server.js'

    if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
        Write-Host "Warning: Node.js dashboard UI not found at $serverPath" -ForegroundColor Yellow
        return $null
    }

    Write-Host "Starting Node.js dashboard UI on port $DashboardPort..." -ForegroundColor Yellow
    
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        Write-Host "Warning: Node.js not found. Skipping dashboard UI. Install Node.js to enable the dashboard." -ForegroundColor Yellow
        return $null
    }

    $env:PORT = $DashboardPort
    $env:Dashboard__Port = $DashboardPort
    $env:Dashboard__Url = "http://localhost:$DashboardPort"
    $env:Dashboard__Enabled = "true"
    
    $uiLogPath = Join-Path $logsPath "dashboard-ui-$(Get-Date -Format 'ddMMyyyy-HHmmss').log"
    
    $process = Start-Process -FilePath 'node' `
        -ArgumentList @($serverPath) `
        -WorkingDirectory $uiPath `
        -NoNewWindow `
        -PassThru `
        -RedirectStandardOutput $uiLogPath `
        -RedirectStandardError $uiLogPath
    
    Write-Host "Dashboard UI started (PID: $($process.Id)). Log: $uiLogPath" -ForegroundColor Green
    
    return $process
}

function Stop-DashboardUI {
    param([object]$Process)
    
    if ($null -eq $Process -or $Process.HasExited) {
        return
    }
    
    Write-Host "Stopping Node.js dashboard UI (PID: $($Process.Id))..." -ForegroundColor Yellow
    try {
        $Process.CloseMainWindow() | Out-Null
        if (-not $Process.WaitForExit(5000)) {
            Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        }
        Write-Host "Dashboard UI stopped." -ForegroundColor Green
    }
    catch {
        Write-Host "Error stopping dashboard UI: $_" -ForegroundColor Yellow
    }
}

try {
    Import-EnvironmentFile -Path $envFilePath

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET SDK was not found.'
    }

    if (-not (Test-Path -LiteralPath $appHostProject -PathType Leaf)) {
        throw "Aspire AppHost project was not found: $appHostProject"
    }

    if (-not $NoBuild) {
        New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
        $buildTimestamp = Get-Date -Format 'ddMMyyyy-HHmmss'
        $appHostBuildProcess = Start-Process -FilePath 'dotnet' `
            -ArgumentList @('build', $appHostProject, '--configuration', 'Debug', '--verbosity', 'quiet') `
            -NoNewWindow `
            -PassThru `
            -RedirectStandardOutput (Join-Path $logsPath "apphost-build-$buildTimestamp.log") `
            -RedirectStandardError (Join-Path $logsPath "apphost-build-error-$buildTimestamp.log")
        Write-Host "Building Aspire AppHost in background (PID: $($appHostBuildProcess.Id))..." -ForegroundColor Gray
    }

    if (-not $NoEmulator) {
        $runtime = Get-ContainerRuntime
        if (-not (Test-ContainerRuntimeAvailable)) {
            throw "$runtime is unavailable in WSL distro '$(Get-WslDistro)'. Start the runtime or set CONTAINER_WSL_DISTRO."
        }

        if (-not (Test-Path -LiteralPath $envFilePath -PathType Leaf)) {
            throw "Environment file was not found: $envFilePath. Copy .env.example to .env and set SQL_PASSWORD."
        }

        if ([string]::IsNullOrWhiteSpace($env:SQL_PASSWORD)) {
            throw "SQL_PASSWORD is required. Set it in $envFilePath or the current environment."
        }

        $existingContainers = @(Invoke-ContainerCompose -ComposePath $composePath -EnvFilePath $envFilePath -Arguments @('ps', '--quiet'))

        Write-Host "Starting or reusing Service Bus emulator with $runtime..." -ForegroundColor Yellow
        Invoke-ContainerCompose -ComposePath $composePath -EnvFilePath $envFilePath -Arguments @('up', '-d')

        $emulatorStartedByScript = $existingContainers.Count -eq 0
        Wait-ServiceBusEmulatorReady -ComposePath $composePath
    }

    $env:ServiceBus__ConnectionString = if ($env:ServiceBus__ConnectionString) {
        $env:ServiceBus__ConnectionString
    }
    else {
        'Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true'
    }
    $env:ServiceBus__Namespace = if ($env:ServiceBus__Namespace) { $env:ServiceBus__Namespace } else { 'sbemulatorns' }
    $env:ServiceBus__TopicName = if ($env:ServiceBus__TopicName) { $env:ServiceBus__TopicName } else { 'contact.events' }

    $aspireCli = Resolve-AspireCli
    Write-Host 'Ensuring .NET HTTPS development certificate is trusted...' -ForegroundColor Yellow
    & dotnet dev-certs https --trust
    if ($LASTEXITCODE -ne 0) {
        throw "The .NET HTTPS development certificate could not be trusted. Run 'dotnet dev-certs https --trust' manually."
    }

    Write-Host 'Ensuring Aspire HTTPS development certificate is trusted...' -ForegroundColor Yellow
    & $aspireCli certs trust --non-interactive --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Aspire certificate trust failed with exit code $LASTEXITCODE."
    }

    if ($null -ne $appHostBuildProcess) {
        $appHostBuildProcess.WaitForExit()
        if ($appHostBuildProcess.ExitCode -ne 0) {
            throw "Aspire AppHost build failed (exit code $($appHostBuildProcess.ExitCode)). See logs\apphost-build-*.log"
        }
        Write-Host 'Aspire AppHost build completed.' -ForegroundColor Green
    }

    # Start the Node.js dashboard UI
    $uiServerProcess = Start-DashboardUI -DashboardPort 5100
    
    # Give the UI a moment to start before opening browser
    if ($null -ne $uiServerProcess) {
        Start-Sleep -Milliseconds 2000
        
        if (-not $NoBrowser) {
            Write-Host "Opening dashboard in browser: http://localhost:5100" -ForegroundColor Cyan
            Start-Process "http://localhost:5100"
        }
        Write-Host "📊 Dashboard UI: http://localhost:5100" -ForegroundColor Green
    }

    $aspireArguments = @(
        'run',
        '--apphost', $appHostProject,
        '--non-interactive',
        '--nologo',
        '--no-build'
    )

    Write-Host ''
    Write-Host 'Starting Aspire AppHost...' -ForegroundColor Cyan
    Write-Host 'The Aspire dashboard URL will be printed below.' -ForegroundColor Gray
    & $aspireCli @aspireArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Aspire exited with code $LASTEXITCODE."
    }
}
finally {
    if ($null -ne $appHostBuildProcess -and -not $appHostBuildProcess.HasExited) {
        Stop-Process -Id $appHostBuildProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Stop-DashboardUI -Process $uiServerProcess
    Stop-Emulator
}
