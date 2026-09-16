#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Smoke test for ProducerService publishing to local Service Bus emulator.
.DESCRIPTION
    This script publishes 3 sample contact.updated events to the local Service Bus emulator
    with different routing property combinations to verify the producer service works end-to-end.
    
    Prerequisites:
    - Service Bus emulator running (via Docker Compose)
    - ServiceBus__ConnectionString environment variable set
    - .NET 10 SDK installed
.EXAMPLE
    .\producer-smoke-test.ps1
    .\producer-smoke-test.ps1 -Verbose
.NOTES
    Exit codes:
    - 0: Success
    - 1: Configuration error
    - 2: Build error
    - 3: Publish error
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Set working directory to repo root
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$repoRoot/src/ServiceBusPoc.Producer")) {
    Write-Error "Producer project not found at $repoRoot/src/ServiceBusPoc.Producer"
    exit 1
}

# Check for required environment variable
if ([string]::IsNullOrWhiteSpace($env:ServiceBus__ConnectionString)) {
    Write-Error "ServiceBus__ConnectionString environment variable is not set"
    Write-Host "Set it with: `$env:ServiceBus__ConnectionString = 'Endpoint=sb://localhost:5671/;...'"
    exit 1
}

Write-Verbose "Using ServiceBus namespace: $($env:ServiceBus__Namespace -or 'sbemulatorns')"
Write-Verbose "Using topic name: $($env:ServiceBus__TopicName -or 'contact.events')"

# Set default values if not provided
$env:ServiceBus__Namespace ??= 'sbemulatorns'
$env:ServiceBus__TopicName ??= 'contact.events'

# Build the producer project
Write-Host "Building Producer project..." -ForegroundColor Cyan
Push-Location "$repoRoot/src/ServiceBusPoc.Producer"
try {
    $buildOutput = dotnet build --verbosity minimal 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed: $buildOutput"
        exit 2
    }
    Write-Host "✓ Build succeeded" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Test case 1: Contact with insurance only
Write-Host "`nPublishing test message 1: Insurance customer..." -ForegroundColor Cyan
Push-Location "$repoRoot/src/ServiceBusPoc.Producer"
try {
    $output = dotnet run -- `
        --contact-id "CONTACT-001" `
        --first-name "John" `
        --last-name "Insurance" `
        --email "john@insurance.local" `
        --source "crm" `
        --has-insurance true `
        --has-parks-resorts false `
        --has-carwash-product false `
        2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish message 1: $output"
        exit 3
    }
    Write-Host "✓ Message 1 published successfully" -ForegroundColor Green
    if ($VerbosePreference -eq 'Continue') {
        Write-Host $output
    }
}
finally {
    Pop-Location
}

# Test case 2: Contact with Parks & Resorts only
Write-Host "Publishing test message 2: Parks & Resorts customer..." -ForegroundColor Cyan
Push-Location "$repoRoot/src/ServiceBusPoc.Producer"
try {
    $output = dotnet run -- `
        --contact-id "CONTACT-002" `
        --first-name "Jane" `
        --last-name "ParksResorts" `
        --email "jane@parks.local" `
        --source "crm" `
        --has-insurance false `
        --has-parks-resorts true `
        --has-carwash-product false `
        2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish message 2: $output"
        exit 3
    }
    Write-Host "✓ Message 2 published successfully" -ForegroundColor Green
    if ($VerbosePreference -eq 'Continue') {
        Write-Host $output
    }
}
finally {
    Pop-Location
}

# Test case 3: Contact with Carwash product (all attributes)
Write-Host "Publishing test message 3: Multi-product customer..." -ForegroundColor Cyan
Push-Location "$repoRoot/src/ServiceBusPoc.Producer"
try {
    $output = dotnet run -- `
        --contact-id "CONTACT-003" `
        --first-name "Bob" `
        --last-name "AllProducts" `
        --email "bob@customer.local" `
        --source "crm" `
        --has-insurance true `
        --has-parks-resorts true `
        --has-carwash-product true `
        2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish message 3: $output"
        exit 3
    }
    Write-Host "✓ Message 3 published successfully" -ForegroundColor Green
    if ($VerbosePreference -eq 'Continue') {
        Write-Host $output
    }
}
finally {
    Pop-Location
}

Write-Host "`n✓ All smoke tests passed!" -ForegroundColor Green
Write-Host @"
Summary:
- Published 3 contact.updated events to $($env:ServiceBus__TopicName)
- Message 1: Insurance customer (hasInsurance=true)
- Message 2: Parks & Resorts customer (hasParksResorts=true)
- Message 3: Multi-product customer (all attributes=true)

Next steps:
- Monitor the topic subscriptions to verify message delivery
- Run consumers to process the published events
- Run verifier to validate all 8 routing combinations
"@

exit 0
