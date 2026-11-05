#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the local Service Bus emulator and waits for AMQP readiness.

.DESCRIPTION
    Starts the Service Bus emulator and its SQL Edge dependency using the
    container runtime selected by CONTAINER_RUNTIME (Podman by default). The
    command is safe to rerun: existing containers are reused and only stopped
    containers are started again.

    The repository-root .env is required and supplies ACCEPT_EULA and
    SQL_PASSWORD. The script fails when it is missing. It is never generated or
    written by this script.

.PARAMETER ReadinessTimeoutSeconds
    Maximum number of seconds to wait for the AMQP endpoint. Default: 90.

.EXAMPLE
    .\scripts\start-servicebus-emulator.ps1
#>

[CmdletBinding()]
param(
    [ValidateRange(10, 600)]
    [int]$ReadinessTimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$composePath = Join-Path $projectRoot 'infra\servicebus\compose.yaml'
$waitScriptPath = Join-Path $PSScriptRoot 'wait-for-servicebus-emulator.ps1'
$runtimeHelperPath = Join-Path $PSScriptRoot 'container-runtime.ps1'

if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    throw "Compose file was not found: $composePath"
}

if (-not (Test-Path -LiteralPath $waitScriptPath -PathType Leaf)) {
    throw "Readiness helper was not found: $waitScriptPath"
}

. $runtimeHelperPath

$composeEnvFilePath = Get-RootEnvFilePath

$runtime = Get-ContainerRuntime
if (-not (Test-ContainerRuntimeAvailable)) {
    throw "$runtime is unavailable in WSL distro '$(Get-WslDistro)'. Start the runtime or set CONTAINER_WSL_DISTRO."
}

Write-Host "Starting Service Bus emulator containers with $runtime..." -ForegroundColor Cyan
Invoke-ContainerCompose -ComposePath $composePath -EnvFilePath $composeEnvFilePath -Arguments @('up', '-d')

. $waitScriptPath
Wait-ServiceBusEmulatorReady `
    -ComposePath $composePath `
    -EnvFilePath $composeEnvFilePath `
    -PollingSeconds $ReadinessTimeoutSeconds

Write-Host "Service Bus emulator is ready on localhost:5672." -ForegroundColor Green
