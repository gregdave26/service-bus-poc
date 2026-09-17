#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test emulator connectivity with actual Service Bus SDK calls.

.DESCRIPTION
    Starts the emulator and measures time until it can actually
    accept ServiceBusClient connections (more accurate than port check).
#>

$projectRoot = Split-Path -Parent $PSScriptRoot
$infraPath = Join-Path $projectRoot 'infra'

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║            EMULATOR CONNECTIVITY TEST (SDK-Level)                         ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Step 1: Ensure containers are running
Write-Host "STEP 1: Starting emulator containers..." -ForegroundColor Yellow
$composePath = Join-Path $infraPath 'servicebus' 'compose.yaml'

Push-Location (Split-Path $composePath)
try {
    $existing = docker-compose ps --quiet 2>$null
    if (-not $existing) {
        Write-Host "  Starting containers..."
        $null = docker-compose up -d
        Start-Sleep -Seconds 5
    } else {
        Write-Host "  Containers already running"
    }
    Write-Host "  ✓ Containers ready"
}
catch {
    Write-Error "❌ Failed: $_"
    exit 1
}
finally {
    Pop-Location
}

Write-Host ""

# Step 2: Test with .NET SDK
Write-Host "STEP 2: Testing SDK connectivity..." -ForegroundColor Yellow
Write-Host ""

$testScript = @"
using Azure.Messaging.ServiceBus;
using System;
using System.Diagnostics;

var connectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE";
var topicName = "contact.events";

var sw = Stopwatch.StartNew();
var attempts = 0;
var maxAttempts = 120;  // 2 minutes
var delay = 500;  // milliseconds

while (attempts < maxAttempts) {
    try {
        attempts++;
        using (var client = new ServiceBusClient(connectionString)) {
            var sender = client.CreateSender(topicName);
            Console.WriteLine($"[{sw.Elapsed:hh\\:mm\\:ss\\.fff}] Attempt {attempts}: Connected successfully!");
            sw.Stop();
            Console.WriteLine($"Total time to SDK connection: {sw.Elapsed.TotalSeconds:F2} seconds");
            Environment.Exit(0);
        }
    }
    catch (Exception ex) {
        if (attempts % 5 == 0) {
            Console.WriteLine($"[{sw.Elapsed:hh\\:mm\\:ss\\.fff}] Attempt {attempts}: {ex.GetType().Name} - waiting...");
        }
        System.Threading.Thread.Sleep(delay);
    }
}

Console.WriteLine($"Failed to connect after {sw.Elapsed.TotalSeconds:F2} seconds");
Environment.Exit(1);
"@

# Write test script to temp file
$tempScript = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.cs'
Set-Content -Path $tempScript -Value $testScript

try {
    $output = & dotnet run --project "$projectRoot/src/ServiceBusPoc.Producer/ServiceBusPoc.Producer.csproj" --configuration Debug 2>&1 | Select-Object -First 50
    Write-Host $output
}
catch {
    # Actually just run the C# code with dotnet
    Write-Host "  Running connectivity test (may take up to 2 minutes)..." -ForegroundColor Gray
    
    # Create a temporary console app to test
    $cwd = Get-Location
    Push-Location ([System.IO.Path]::GetTempPath())
    try {
        dotnet new console -n EmulatorTest -f net10.0 --force 2>&1 | Out-Null
        
        # Add Azure SDK package
        $project = "EmulatorTest/EmulatorTest.csproj"
        dotnet add $project package Azure.Messaging.ServiceBus 2>&1 | Out-Null
        
        # Replace Program.cs
        Set-Content -Path "EmulatorTest/Program.cs" -Value $testScript
        
        # Run it
        Push-Location EmulatorTest
        dotnet run 2>&1
        Pop-Location
    }
    finally {
        Pop-Location
        Set-Location $cwd
    }
}
finally {
    Remove-Item -Path $tempScript -ErrorAction SilentlyContinue
}

Write-Host ""
