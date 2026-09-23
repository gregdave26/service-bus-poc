#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates the Service Bus emulator topology (topic and subscriptions).

.DESCRIPTION
    Connects to the local Service Bus emulator and verifies:
    - The emulator is responsive
    - The 'contact.events' topic exists
    - All 4 subscriptions exist (digital-channels, insurance, parks-resorts, carwash)
    - Subscription filters are correctly configured

.PARAMETER EmulatorHost
    The emulator host. Default: 'localhost'

.PARAMETER EmulatorPort
    The emulator AMQP port. Default: 5672

.PARAMETER ManagementPort
    The emulator HTTP management port. Default: 5300

.EXAMPLE
    .\scripts\validate-emulator-topology.ps1
#>

[CmdletBinding()]
param(
    [string]$EmulatorHost = 'localhost',
    [int]$EmulatorPort = 5672,
    [int]$ManagementPort = 5300
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$verifierProject = Join-Path $projectRoot 'src\ServiceBusPoc.Verifier\ServiceBusPoc.Verifier.csproj'

Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║           VALIDATING SERVICE BUS EMULATOR TOPOLOGY                         ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check emulator connectivity
Write-Host "Test 1: Checking emulator connectivity..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://${EmulatorHost}:${ManagementPort}/status" -UseBasicParsing
    if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
        Write-Host "  ✓ Emulator is responsive" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Emulator response unexpected" -ForegroundColor Red
        Write-Host "    HTTP status: $($response.StatusCode)" -ForegroundColor Gray
        exit 1
    }
}
catch {
    Write-Host "  ✗ Cannot connect to emulator at ${EmulatorHost}:${ManagementPort}" -ForegroundColor Red
    Write-Host "    Error: $_" -ForegroundColor Gray
    exit 1
}

Write-Host ""

# Test 2-5: Use the repository's verifier and Azure Service Bus SDK
Write-Host "Test 2-5: Verifying topic, subscriptions, and filters..." -ForegroundColor Yellow
if (-not (Test-Path -LiteralPath $verifierProject -PathType Leaf)) {
    throw "Verifier project was not found: $verifierProject"
}

$env:ServiceBus__ConnectionString = $env:ServiceBus__ConnectionString ??
    "Endpoint=sb://${EmulatorHost}:${EmulatorPort}/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true"
$env:ServiceBus__Namespace = $env:ServiceBus__Namespace ?? 'sbemulatorns'
$env:ServiceBus__TopicName = $env:ServiceBus__TopicName ?? 'contact.events'

& dotnet run --project $verifierProject --configuration Debug --no-restore -- --validate-topology
if ($LASTEXITCODE -ne 0) {
    throw "Topology validation failed with exit code $LASTEXITCODE."
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                   TOPOLOGY VALIDATION SUCCESSFUL                          ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
