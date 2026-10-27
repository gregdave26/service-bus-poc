#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the Service Bus POC emulator and applications through .NET Aspire.

.DESCRIPTION
    This is an alternative local entry point to run-dashboard.ps1. The container
    runtime (Podman by default, see CONTAINER_RUNTIME) continues to own the Service
    Bus emulator while Aspire owns the application processes and dashboard. The
    existing custom dashboard is not started.

.PARAMETER NoEmulator
    Do not start or stop the emulator; assume it is already running.

.PARAMETER NoBuild
    Skip the Aspire AppHost build and restore.

.EXAMPLE
    .\scripts\run-aspire.ps1

.EXAMPLE
    .\scripts\run-aspire.ps1 -NoEmulator -NoBuild
#>

[CmdletBinding()]
param(
    [switch]$NoEmulator,
    [switch]$NoBuild
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
    Stop-Emulator
}
