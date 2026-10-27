#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the local Service Bus emulator and waits for AMQP readiness.

.DESCRIPTION
    Starts the Service Bus emulator and its SQL Edge dependency using the
    container runtime selected by CONTAINER_RUNTIME (Podman by default). The
    command is safe to rerun: existing containers are reused and only stopped
    containers are started again.

    SQL_PASSWORD must be supplied through the repository's ignored .env file or
    the current process environment. It is never generated or written by this
    script.

.PARAMETER ReadinessTimeoutSeconds
    Maximum number of seconds to wait for the AMQP endpoint. Default: 90.

.PARAMETER EnvFilePath
    Optional path to a Compose environment file. Defaults to the repository-root .env.

.EXAMPLE
    .\scripts\start-servicebus-emulator.ps1

.EXAMPLE
    $env:SQL_PASSWORD = 'LocalOnlyStrongPassword123!'
    .\scripts\start-servicebus-emulator.ps1
#>

[CmdletBinding()]
param(
    [ValidateRange(10, 600)]
    [int]$ReadinessTimeoutSeconds = 90,

    [string]$EnvFilePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$composePath = Join-Path $projectRoot 'infra\servicebus\compose.yaml'
$waitScriptPath = Join-Path $PSScriptRoot 'wait-for-servicebus-emulator.ps1'
$runtimeHelperPath = Join-Path $PSScriptRoot 'container-runtime.ps1'

if ([string]::IsNullOrWhiteSpace($EnvFilePath)) {
    $EnvFilePath = Join-Path $projectRoot '.env'
}

if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    throw "Compose file was not found: $composePath"
}

if (-not (Test-Path -LiteralPath $waitScriptPath -PathType Leaf)) {
    throw "Readiness helper was not found: $waitScriptPath"
}

. $runtimeHelperPath

$runtime = Get-ContainerRuntime
if (-not (Test-ContainerRuntimeAvailable)) {
    throw "$runtime is unavailable in WSL distro '$(Get-WslDistro)'. Start the runtime or set CONTAINER_WSL_DISTRO."
}

$composeEnvFilePath = $null
if (Test-Path -LiteralPath $EnvFilePath -PathType Leaf) {
    $composeEnvFilePath = $EnvFilePath
}
elseif ([string]::IsNullOrWhiteSpace($env:SQL_PASSWORD)) {
    throw "SQL_PASSWORD is required. Add it to the ignored .env file or set it in the current environment."
}

Write-Host "Starting Service Bus emulator containers with $runtime..." -ForegroundColor Cyan
Invoke-ContainerCompose -ComposePath $composePath -EnvFilePath $composeEnvFilePath -Arguments @('up', '-d')

. $waitScriptPath
Wait-ServiceBusEmulatorReady `
    -ComposePath $composePath `
    -EnvFilePath $composeEnvFilePath `
    -PollingSeconds $ReadinessTimeoutSeconds

Write-Host "Service Bus emulator is ready on localhost:5672." -ForegroundColor Green
