#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the local emulator when needed and runs the repository SDK connectivity probe.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot '..\scripts\container-runtime.ps1')

$projectRoot = Split-Path -Parent $PSScriptRoot
$composePath = Join-Path $projectRoot 'infra\servicebus\compose.yaml'
$envFilePath = Get-RootEnvFilePath

Write-Host "Starting emulator connectivity test..." -ForegroundColor Cyan

$runtime = Get-ContainerRuntime
if (-not (Test-ContainerRuntimeAvailable)) {
    throw "$runtime is unavailable in WSL distro '$(Get-WslDistro)'; the emulator was not started."
}

$existing = @()
try {
    $existing = @(Invoke-ContainerCompose -ComposePath $composePath -EnvFilePath $envFilePath -Arguments @('ps', '--quiet') |
        Where-Object { $_.Trim() })
}
catch {
    throw "Unable to inspect the emulator containers: $($_.Exception.Message)"
}

if ($existing.Count -eq 0) {
    Write-Host "Starting emulator containers..."
    Invoke-ContainerCompose -ComposePath $composePath -EnvFilePath $envFilePath -Arguments @('up', '-d')
} else {
    Write-Host "Emulator containers are already running."
}

$env:ServiceBus__ConnectionString = $env:ServiceBus__ConnectionString ??
    'Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true'
$env:ServiceBus__Namespace = $env:ServiceBus__Namespace ?? 'sbemulatorns'
$env:ServiceBus__TopicName = $env:ServiceBus__TopicName ?? 'contact.events'

Write-Host "Running the Service Bus SDK connectivity probe..." -ForegroundColor Yellow
& dotnet run --project "$projectRoot/src/ContactEvents/Verifier/ServiceBusPoc.ContactEvents.Verifier.csproj" `
    --configuration Debug --no-restore -- --connectivity-probe
if ($LASTEXITCODE -ne 0) {
    throw "SDK connectivity probe failed with exit code $LASTEXITCODE."
}

Write-Host "SDK connectivity probe passed." -ForegroundColor Green
