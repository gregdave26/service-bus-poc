#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the local Service Bus emulator and waits for AMQP readiness.

.DESCRIPTION
    Starts the Service Bus emulator and its SQL Edge dependency using Docker
    Compose. The command is safe to rerun: existing containers are reused and
    only stopped containers are started again.

    SQL_PASSWORD must be supplied through the repository's ignored .env file or
    the current process environment. It is never generated or written by this
    script.

.PARAMETER ReadinessTimeoutSeconds
    Maximum number of seconds to wait for the AMQP endpoint. Default: 90.

.PARAMETER EnvFilePath
    Optional path to a Docker Compose environment file. Defaults to .env.

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

if ([string]::IsNullOrWhiteSpace($EnvFilePath)) {
    $EnvFilePath = Join-Path $projectRoot 'infra\servicebus\.env'
}

if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    throw "Docker Compose file was not found: $composePath"
}

if (-not (Test-Path -LiteralPath $waitScriptPath -PathType Leaf)) {
    throw "Readiness helper was not found: $waitScriptPath"
}

try {
    docker version | Out-Null
}
catch {
    throw "Docker is unavailable or Docker Desktop is not running."
}

$composeArguments = @()
if (Test-Path -LiteralPath $EnvFilePath -PathType Leaf) {
    $composeArguments += @('--env-file', $EnvFilePath)
}
elseif ([string]::IsNullOrWhiteSpace($env:SQL_PASSWORD)) {
    throw "SQL_PASSWORD is required. Add it to the ignored .env file or set it in the current environment."
}

$composeDirectory = Split-Path -Parent $composePath
Push-Location $composeDirectory
try {
    Write-Host "Starting Service Bus emulator containers..." -ForegroundColor Cyan
    & docker-compose @composeArguments -f $composePath up -d
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed to start the Service Bus emulator."
    }
}
finally {
    Pop-Location
}

. $waitScriptPath
Wait-ServiceBusEmulatorReady `
    -ComposePath $composePath `
    -PollingSeconds $ReadinessTimeoutSeconds

Write-Host "Service Bus emulator is ready on localhost:5672." -ForegroundColor Green
