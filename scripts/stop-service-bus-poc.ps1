#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Stops Service Bus POC processes and the local emulator safely.

.DESCRIPTION
    Finds processes whose command lines reference this repository or one of the
    Service Bus POC applications, stops their child processes, and brings down
    the local Docker Compose stack.

    This script does not kill every dotnet or PowerShell process on the machine.
    An editor PowerShell host is stopped only when its PID is explicitly passed.

.PARAMETER PowerShellHostPid
    Optional PID of a stale PowerShell host that owns an old transcript.

.EXAMPLE
    .\scripts\stop-service-bus-poc.ps1

.EXAMPLE
    .\scripts\stop-service-bus-poc.ps1 -PowerShellHostPid 34576
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [int]$PowerShellHostPid
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$composePath = Join-Path $projectRoot 'infra\servicebus\compose.yaml'

Write-Host "Stopping Service Bus POC processes for: $projectRoot" -ForegroundColor Cyan

# Stop an active transcript in the current PowerShell session if one exists.
Stop-Transcript -ErrorAction SilentlyContinue | Out-Null

$processes = @(Get-CimInstance Win32_Process)
$processById = @{}
foreach ($process in $processes) {
    $processById[[int]$process.ProcessId] = $process
}

$escapedRoot = [regex]::Escape($projectRoot)
$targetIds = [System.Collections.Generic.HashSet[int]]::new()

foreach ($process in $processes) {
    $commandLine = [string]$process.CommandLine
    if ($commandLine -match $escapedRoot -or
        $commandLine -match 'ServiceBusPoc\.(Dashboard|Producer|DigitalChannels|Insurance|ParksResorts|Carwash|Verifier)' -or
        $commandLine -match 'run-(dashboard|local-poc|debug-run|simple)\.ps1') {
        if ([int]$process.ProcessId -ne $PID) {
            $null = $targetIds.Add([int]$process.ProcessId)
        }
    }
}

# Include descendants such as dotnet processes launched by a PowerShell wrapper.
$changed = $true
while ($changed) {
    $changed = $false
    foreach ($process in $processes) {
        $processId = [int]$process.ProcessId
        $parentId = [int]$process.ParentProcessId
        if ($targetIds.Contains($parentId) -and $targetIds.Add($processId)) {
            $changed = $true
        }
    }
}

if ($PowerShellHostPid -gt 0 -and $PowerShellHostPid -ne $PID) {
    $null = $targetIds.Add($PowerShellHostPid)
}

foreach ($processId in ($targetIds | Sort-Object -Descending)) {
    if ($PSCmdlet.ShouldProcess("PID $processId", 'Stop process')) {
        try {
            Stop-Process -Id $processId -Force -ErrorAction Stop
            Write-Host "  Stopped PID $processId" -ForegroundColor Green
        }
        catch [System.ArgumentException] {
            Write-Host "  PID $processId already stopped" -ForegroundColor DarkGray
        }
    }
}

if (Test-Path $composePath) {
    $composeDirectory = Split-Path -Parent $composePath
    Push-Location $composeDirectory
    try {
        if ($PSCmdlet.ShouldProcess('Service Bus emulator compose stack', 'Run docker-compose down')) {
            docker-compose down
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host "Service Bus POC shutdown complete." -ForegroundColor Green
