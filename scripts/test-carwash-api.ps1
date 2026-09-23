#!/usr/bin/env pwsh

<#
.SYNOPSIS
  Tests the Carwash API verification endpoint.

.DESCRIPTION
  Validates the Carwash POST /carwash/v1/verify endpoint with various test scenarios.
  Requires the Carwash service to be running on http://localhost:5000.

.EXAMPLE
  .\test-carwash-api.ps1

.NOTES
  Author: Service Bus POC Team
  Depends on: curl (comes with Windows 10+) or Invoke-WebRequest
#>

param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$TimeoutSeconds = 5
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# Color codes for output
$colors = @{
    Reset   = "`e[0m"
    Green   = "`e[32m"
    Red     = "`e[31m"
    Yellow  = "`e[33m"
    Cyan    = "`e[36m"
    Blue    = "`e[34m"
}

function Write-Success($msg) { Write-Host "$($colors.Green)✓ $msg$($colors.Reset)" }
function Write-Error-Msg($msg) { Write-Host "$($colors.Red)✗ $msg$($colors.Reset)" }
function Write-Info($msg) { Write-Host "$($colors.Cyan)ℹ $msg$($colors.Reset)" }
function Write-Test($msg) { Write-Host "$($colors.Blue)━━━ $msg$($colors.Reset)" }
function Write-Highlight($msg) { Write-Host "$($colors.Yellow)$msg$($colors.Reset)" }

Write-Host ""
Write-Host "$($colors.Blue)╔════════════════════════════════════════════════════════════════╗"
Write-Host "║              CARWASH API VERIFICATION TEST                      ║"
Write-Host "╚════════════════════════════════════════════════════════════════╝$($colors.Reset)"
Write-Host ""

Write-Info "Base URL: $BaseUrl"
Write-Info "Timeout: ${TimeoutSeconds}s"
Write-Host ""

# Check if Carwash service is accessible
Write-Test "Health Check"
try {
    $healthTest = Invoke-WebRequest -Uri "$BaseUrl/health" -Method Get -TimeoutSec $TimeoutSeconds -ErrorAction SilentlyContinue
    Write-Info "Service is accessible (responded with any status)"
}
catch {
    # Service may not have /health endpoint, that's OK
    Write-Info "Service status unknown (no health endpoint)"
}

# Test 1: Valid member (membership number starting with VALID)
Write-Host ""
Write-Test "Test 1: Valid Member Lookup"
Write-Info "Testing membership number starting with 'VALID' (should return ValidMember=true)"

$payload1 = @{
    membershipNumber = "VALID-12345678"
} | ConvertTo-Json

try {
    $response1 = Invoke-WebRequest `
        -Uri "$BaseUrl/carwash/v1/verify" `
        -Method Post `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body $payload1 `
        -TimeoutSec $TimeoutSeconds

    $result1 = $response1.Content | ConvertFrom-Json
    
    if ($response1.StatusCode -eq 200 -and $result1.ValidMember -eq $true) {
        Write-Success "Valid member verification passed"
        Write-Info "Response: $(($response1.Content | ConvertFrom-Json) | ConvertTo-Json)"
    } else {
        Write-Error-Msg "Unexpected response: $($response1.StatusCode)"
        Write-Info "Content: $($response1.Content)"
    }
}
catch {
    Write-Error-Msg "Test failed: $_"
}

# Test 2: Invalid member (membership number not starting with VALID)
Write-Host ""
Write-Test "Test 2: Invalid Member Lookup"
Write-Info "Testing membership number not starting with 'VALID' (should return ValidMember=false)"

$payload2 = @{
    membershipNumber = "INVALID-87654321"
} | ConvertTo-Json

try {
    $response2 = Invoke-WebRequest `
        -Uri "$BaseUrl/carwash/v1/verify" `
        -Method Post `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body $payload2 `
        -TimeoutSec $TimeoutSeconds

    $result2 = $response2.Content | ConvertFrom-Json
    
    if ($response2.StatusCode -eq 200 -and $result2.ValidMember -eq $false) {
        Write-Success "Invalid member verification passed"
        Write-Info "Response: $(($response2.Content | ConvertFrom-Json) | ConvertTo-Json)"
    } else {
        Write-Error-Msg "Unexpected response: $($response2.StatusCode)"
        Write-Info "Content: $($response2.Content)"
    }
}
catch {
    Write-Error-Msg "Test failed: $_"
}

# Test 3: Empty membership number (should return 400)
Write-Host ""
Write-Test "Test 3: Empty membership number Validation"
Write-Info "Testing empty membership number (should return 400 Bad Request)"

$payload3 = @{
    membershipNumber = ""
} | ConvertTo-Json

try {
    $response3 = Invoke-WebRequest `
        -Uri "$BaseUrl/carwash/v1/verify" `
        -Method Post `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body $payload3 `
        -TimeoutSec $TimeoutSeconds `
        -ErrorAction Stop
    
    Write-Error-Msg "Expected 400, got $($response3.StatusCode)"
}
catch [System.Net.Http.HttpRequestException] {
    $statusCode = $_.Exception.Response.StatusCode
    if ($statusCode -eq 400) {
        Write-Success "Empty membership number correctly rejected with 400"
        try {
            $errorContent = $_.Exception.Response.Content.ReadAsStringAsync().Result
            Write-Info "Error Response: $(($errorContent | ConvertFrom-Json) | ConvertTo-Json)"
        }
        catch {
            Write-Info "Error Response: $errorContent"
        }
    } else {
        Write-Error-Msg "Unexpected status code: $statusCode"
    }
}
catch {
    # Try to extract status from response
    $statusCode = [int]($_.Exception.Response.StatusCode)
    if ($statusCode -eq 400) {
        Write-Success "Empty membership number correctly rejected with 400"
    } else {
        Write-Error-Msg "Test failed: $_"
    }
}

# Test 4: Missing membership number (should return 400)
Write-Host ""
Write-Test "Test 4: Missing membership number Field"
Write-Info "Testing missing membership number field (should return 400 Bad Request)"

$payload4 = @{} | ConvertTo-Json

try {
    $response4 = Invoke-WebRequest `
        -Uri "$BaseUrl/carwash/v1/verify" `
        -Method Post `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body $payload4 `
        -TimeoutSec $TimeoutSeconds `
        -ErrorAction Stop
    
    Write-Error-Msg "Expected 400, got $($response4.StatusCode)"
}
catch [System.Net.Http.HttpRequestException] {
    $statusCode = $_.Exception.Response.StatusCode
    if ($statusCode -eq 400) {
        Write-Success "Missing membership number correctly rejected with 400"
    } else {
        Write-Error-Msg "Unexpected status code: $statusCode"
    }
}
catch {
    $statusCode = [int]($_.Exception.Response.StatusCode)
    if ($statusCode -eq 400) {
        Write-Success "Missing membership number correctly rejected with 400"
    } else {
        Write-Error-Msg "Test failed: $_"
    }
}

# Test 5: Invalid endpoint (should return 404)
Write-Host ""
Write-Test "Test 5: Invalid Endpoint"
Write-Info "Testing non-existent endpoint (should return 404)"

try {
    $response5 = Invoke-WebRequest `
        -Uri "$BaseUrl/invalid/endpoint" `
        -Method Post `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body '{}' `
        -TimeoutSec $TimeoutSeconds `
        -ErrorAction Stop
    
    Write-Error-Msg "Expected 404, got $($response5.StatusCode)"
}
catch [System.Net.Http.HttpRequestException] {
    $statusCode = $_.Exception.Response.StatusCode
    if ($statusCode -eq 404) {
        Write-Success "Invalid endpoint correctly rejected with 404"
    } else {
        Write-Error-Msg "Unexpected status code: $statusCode"
    }
}
catch {
    $statusCode = [int]($_.Exception.Response.StatusCode)
    if ($statusCode -eq 404) {
        Write-Success "Invalid endpoint correctly rejected with 404"
    } else {
        Write-Error-Msg "Test failed: $_"
    }
}

Write-Host ""
Write-Host "$($colors.Blue)════════════════════════════════════════════════════════════════$($colors.Reset)"
Write-Info "Test suite complete"
Write-Host ""
