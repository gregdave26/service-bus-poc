#Requires -Version 7.0
<#
.SYNOPSIS
    Shared container runtime helpers for the local Service Bus emulator stack.

.DESCRIPTION
    CONTAINER_RUNTIME selects the runtime: 'podman' (default) or 'docker'.
    Both runtimes are installed inside WSL, so their commands run through `wsl`.
    CONTAINER_WSL_DISTRO selects the WSL distribution (default: Ubuntu).
    Both variables are read from the process environment, not from .env files.
#>

$script:WslForwardedVariables = @('SQL_PASSWORD', 'ACCEPT_EULA', 'SQL_WAIT_INTERVAL', 'EMULATOR_HTTP_PORT')

function Get-ContainerRuntime {
    $configured = $env:CONTAINER_RUNTIME
    if ([string]::IsNullOrWhiteSpace($configured)) {
        return 'podman'
    }

    $runtime = $configured.Trim().ToLowerInvariant()
    if ($runtime -notin @('podman', 'docker')) {
        throw "CONTAINER_RUNTIME must be 'podman' or 'docker'. Actual value: '$configured'."
    }

    return $runtime
}

function Get-WslDistro {
    if ([string]::IsNullOrWhiteSpace($env:CONTAINER_WSL_DISTRO)) {
        return 'Ubuntu'
    }

    return $env:CONTAINER_WSL_DISTRO.Trim()
}

function ConvertTo-WslPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$WindowsPath
    )

    $fullPath = [System.IO.Path]::GetFullPath($WindowsPath)
    if ($fullPath -notmatch '^(?<drive>[A-Za-z]):\\(?<rest>.*)$') {
        throw "Only drive-letter Windows paths can be converted to WSL paths: $WindowsPath"
    }

    $drive = $Matches['drive'].ToLowerInvariant()
    $rest = $Matches['rest'] -replace '\\', '/'
    return "/mnt/$drive/$rest"
}

function Get-WslEnvironmentForwarding {
    $entries = @($env:WSLENV) + $script:WslForwardedVariables
    return ($entries | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ':'
}

function Invoke-ContainerRuntime {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $runtime = Get-ContainerRuntime
    $originalWslEnv = $env:WSLENV
    try {
        $env:WSLENV = Get-WslEnvironmentForwarding
        & wsl -d (Get-WslDistro) -- $runtime @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        $env:WSLENV = $originalWslEnv
    }

    if ($exitCode -ne 0) {
        throw "$runtime command failed with exit code ${exitCode}: $($Arguments -join ' ')"
    }
}

function Invoke-ContainerCompose {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ComposePath,

        [string]$EnvFilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $composeFile = ConvertTo-WslPath -WindowsPath $ComposePath
    $envFile = $null
    if ($EnvFilePath) {
        $envFile = ConvertTo-WslPath -WindowsPath $EnvFilePath
    }

    $composeArguments = @('compose')
    if ($envFile) {
        $composeArguments += @('--env-file', $envFile)
    }
    $composeArguments += @('-f', $composeFile)
    $composeArguments += $Arguments

    Invoke-ContainerRuntime -Arguments $composeArguments
}

function Test-ContainerRuntimeAvailable {
    try {
        Invoke-ContainerRuntime -Arguments @('version') | Out-Null
        return $true
    }
    catch {
        return $false
    }
}
