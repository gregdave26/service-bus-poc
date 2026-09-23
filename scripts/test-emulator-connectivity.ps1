#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the local emulator when needed and runs the repository SDK connectivity probe.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$composePath = Join-Path $projectRoot 'infra\servicebus\compose.yaml'

Write-Host "Starting emulator connectivity test..." -ForegroundColor Cyan

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI is unavailable; the emulator was not started."
}
& docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Docker is unavailable; the emulator was not started."
}

$composeDirectory = Split-Path -Parent $composePath
Push-Location $composeDirectory
try {
    $existing = & docker compose -f $composePath ps --quiet 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect the emulator containers."
    }

    if (-not $existing) {
        Write-Host "Starting emulator containers..."
        & docker compose -f $composePath up -d
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose up failed with exit code $LASTEXITCODE."
        }
    } else {
        Write-Host "Emulator containers are already running."
    }
}
finally {
    Pop-Location
}

$env:ServiceBus__ConnectionString = $env:ServiceBus__ConnectionString ??
    'Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true'
$env:ServiceBus__Namespace = $env:ServiceBus__Namespace ?? 'sbemulatorns'
$env:ServiceBus__TopicName = $env:ServiceBus__TopicName ?? 'contact.events'

Write-Host "Running the Service Bus SDK connectivity probe..." -ForegroundColor Yellow
& dotnet run --project "$projectRoot/src/ServiceBusPoc.Verifier/ServiceBusPoc.Verifier.csproj" `
    --configuration Debug --no-restore -- --connectivity-probe
if ($LASTEXITCODE -ne 0) {
    throw "SDK connectivity probe failed with exit code $LASTEXITCODE."
}

Write-Host "SDK connectivity probe passed." -ForegroundColor Green
