#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the complete automated .NET test suite.

.DESCRIPTION
    Runs every test in the repository's xUnit test project. Use test-coverage.ps1
    when coverage collection and the 80 percent threshold are required.

.EXAMPLE
    .\scripts\test-all.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $projectRoot 'tests\ServiceBusPoc.Tests\ServiceBusPoc.Tests.csproj'

if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) {
    throw "Test project was not found: $testProject"
}

Write-Host "Running all automated tests..." -ForegroundColor Cyan
& dotnet test $testProject --configuration Debug

if ($LASTEXITCODE -ne 0) {
    throw "Automated tests failed with exit code $LASTEXITCODE."
}

Write-Host "All automated tests passed." -ForegroundColor Green
