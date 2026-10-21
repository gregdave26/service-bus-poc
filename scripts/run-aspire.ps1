#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the Service Bus POC emulator and applications through .NET Aspire.

.DESCRIPTION
    This is an alternative local entry point to run-dashboard.ps1. Docker Compose
    continues to own the Service Bus emulator while Aspire owns the application
    processes and dashboard. The existing custom dashboard is not started.

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
$emulatorStartedByScript = $false

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
    docker compose --env-file $envFilePath -f $composePath down
}

function Resolve-AspireCli {
    $command = Get-Command aspire -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $packageRoot = Join-Path $env:USERPROFILE '.nuget\packages\aspire.cli.win-x64'
    $latestPackage = Get-ChildItem -LiteralPath $packageRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [Version]$_.Name } -Descending |
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

    if (-not $NoEmulator) {
        if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
            throw 'Docker was not found.'
        }

        if (-not (Test-Path -LiteralPath $envFilePath -PathType Leaf)) {
            throw "Environment file was not found: $envFilePath. Copy .env.example to .env and set SQL_PASSWORD."
        }

        $composeDirectory = Split-Path -Parent $composePath
        Push-Location $composeDirectory
        try {
            $existingContainers = @(docker compose --env-file $envFilePath -f $composePath ps -q 2>$null)
        }
        finally {
            Pop-Location
        }

        Write-Host 'Starting or reusing Service Bus emulator...' -ForegroundColor Yellow
        docker compose --env-file $envFilePath -f $composePath up -d
        if ($LASTEXITCODE -ne 0) {
            throw "Docker Compose failed with exit code $LASTEXITCODE."
        }

        $emulatorStartedByScript = $existingContainers.Count -eq 0
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

    $aspireArguments = @(
        'run',
        '--apphost', $appHostProject,
        '--non-interactive',
        '--nologo'
    )
    if ($NoBuild) {
        $aspireArguments += '--no-build'
    }

    Write-Host ''
    Write-Host 'Starting Aspire AppHost...' -ForegroundColor Cyan
    Write-Host 'The Aspire dashboard URL will be printed below.' -ForegroundColor Gray
    & $aspireCli @aspireArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Aspire exited with code $LASTEXITCODE."
    }
}
finally {
    Stop-Emulator
}
