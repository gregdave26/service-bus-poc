#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests Adyen Checkout session connectivity without starting the application.

.DESCRIPTION
    Loads Adyen settings from the repository-root .env file and sends the same
    minimal Checkout Sessions request used by the Commerce API startup probe.
    The API key and other credential values are never written to the console.

.PARAMETER EnvironmentFile
    Environment file to load. Defaults to the repository-root .env file.

.EXAMPLE
    .\test-scripts\test-adyen-connectivity.ps1

.EXAMPLE
    .\test-scripts\test-adyen-connectivity.ps1 -EnvironmentFile .\.env
#>

[CmdletBinding()]
param(
    [string]$EnvironmentFile = (Join-Path (Split-Path -Parent $PSScriptRoot) '.env')
)

$ErrorActionPreference = 'Stop'

function Read-DotEnv {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Environment file not found: $Path"
    }

    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#') -or -not $trimmed.Contains('=')) {
            continue
        }

        $name, $value = $trimmed.Split('=', 2)
        $name = $name.Trim()
        $value = $value.Trim()
        if (($value.StartsWith("'") -and $value.EndsWith("'")) -or
            ($value.StartsWith('"') -and $value.EndsWith('"'))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $values[$name] = $value
    }

    return $values
}

function Get-RequiredValue {
    param(
        [Parameter(Mandatory)][hashtable]$Values,
        [Parameter(Mandatory)][string]$Name
    )

    if (-not $Values.ContainsKey($Name) -or [string]::IsNullOrWhiteSpace($Values[$Name])) {
        throw "$Name is missing from the environment file."
    }

    return [string]$Values[$Name]
}

$settings = Read-DotEnv -Path (Resolve-Path -LiteralPath $EnvironmentFile)
$environment = if ($settings.ContainsKey('Adyen__Environment')) { $settings['Adyen__Environment'] } else { 'test' }
$merchantAccount = Get-RequiredValue -Values $settings -Name 'Adyen__MerchantAccount'
$checkoutApiUrl = $settings['Adyen__CheckoutApiUrl']
$apiKey = $settings['Adyen__ApiKey']
$subscriptionKey = $settings['Adyen__ApimSubscriptionKey']

if ([string]::IsNullOrWhiteSpace($checkoutApiUrl)) {
    $adyenHost = if ($environment -eq 'live') { 'checkout-live.adyen.com' } else { 'checkout-test.adyen.com' }
    $checkoutApiUrl = "https://$adyenHost/v72"
}

$uri = "$($checkoutApiUrl.TrimEnd('/'))/sessions"
$headers = @{}
if (-not [string]::IsNullOrWhiteSpace($subscriptionKey)) {
    $headers['Ocp-Apim-Subscription-Key'] = $subscriptionKey
    $authentication = 'APIM subscription key'
}
elseif (-not [string]::IsNullOrWhiteSpace($apiKey)) {
    $headers['X-API-Key'] = $apiKey
    $authentication = 'Adyen API key'
}
else {
    throw 'Set Adyen__ApiKey, or set both Adyen__CheckoutApiUrl and Adyen__ApimSubscriptionKey.'
}

$body = @{
    amount = @{
        currency = 'AUD'
        value = 1
    }
    merchantAccount = $merchantAccount
    reference = "RSA-connectivity-$([Guid]::NewGuid().ToString('N'))"
    returnUrl = 'http://localhost:5100/digital-site'
    countryCode = 'AU'
    shopperLocale = 'en-AU'
    channel = 'Web'
} | ConvertTo-Json -Depth 5

Write-Host "Testing Adyen Checkout connectivity"
Write-Host "  Environment: $environment"
Write-Host "  Merchant account: $merchantAccount"
Write-Host "  Endpoint: $uri"
Write-Host "  Authentication: $authentication"

try {
    $response = Invoke-WebRequest `
        -Uri $uri `
        -Method Post `
        -Headers $headers `
        -ContentType 'application/json' `
        -Body $body `
        -UseBasicParsing

    Write-Host "SUCCESS: Adyen returned HTTP $($response.StatusCode)." -ForegroundColor Green
    exit 0
}
catch {
    $webResponse = $_.Exception.Response
    if ($null -eq $webResponse) {
        throw
    }

    $statusCode = [int]$webResponse.StatusCode
    $responseBody = ''
    if ($webResponse.GetResponseStream()) {
        $reader = [System.IO.StreamReader]::new($webResponse.GetResponseStream())
        try {
            $responseBody = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }

    Write-Host "FAILED: Adyen returned HTTP $statusCode." -ForegroundColor Red
    if ($responseBody) {
        Write-Host $responseBody
    }
    exit 1
}
