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
    # WSLENV forwards unset variables as empty strings, and an empty value would override the Compose env file.
    $setVariables = $script:WslForwardedVariables | Where-Object { Test-Path -LiteralPath "env:$_" }
    $entries = @($env:WSLENV) + $setVariables
    return ($entries | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ':'
}

function Get-WslRuntimeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Runtime
    )

    # The Docker Compose plugin installed in WSL would otherwise be picked up by
    # `podman compose` and fail against the Docker socket.
    if ($Runtime -eq 'podman') {
        return @('env', 'PODMAN_COMPOSE_PROVIDER=podman-compose', 'podman')
    }

    return @($Runtime)
}

function Get-WslRuntimeArguments {
    param(
        [Parameter(Mandatory)]
        [string]$Runtime,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    return @('-d', (Get-WslDistro), '--') + (Get-WslRuntimeCommand -Runtime $Runtime) + $Arguments
}

function Invoke-ContainerRuntime {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $runtime = Get-ContainerRuntime
    $wslArguments = Get-WslRuntimeArguments -Runtime $runtime -Arguments $Arguments
    $originalWslEnv = $env:WSLENV
    try {
        $env:WSLENV = Get-WslEnvironmentForwarding
        & wsl @wslArguments 2>&1 |
            ForEach-Object { $_.ToString() -replace "$([char]27)\[[0-9;]*m", '' } |
            Where-Object { $_.Trim() -and $_ -notmatch 'Executing external compose provider' }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $env:WSLENV = $originalWslEnv
    }

    if ($exitCode -ne 0) {
        throw "$runtime command failed with exit code ${exitCode}: $($Arguments -join ' ')"
    }
}

function Get-ContainerComposeArguments {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ComposePath,

        [string]$EnvFilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $composeArguments = @('compose')
    if ($EnvFilePath) {
        $composeArguments += @('--env-file', (ConvertTo-WslPath -WindowsPath $EnvFilePath))
    }
    $composeArguments += @('-f', (ConvertTo-WslPath -WindowsPath $ComposePath))
    $composeArguments += $Arguments

    return $composeArguments
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

    Invoke-ContainerRuntime -Arguments (Get-ContainerComposeArguments -ComposePath $ComposePath -EnvFilePath $EnvFilePath -Arguments $Arguments)
}

function Start-ContainerRuntimeProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$StandardOutputPath,

        [Parameter(Mandatory)]
        [string]$StandardErrorPath
    )

    $runtime = Get-ContainerRuntime
    $wslArguments = Get-WslRuntimeArguments -Runtime $runtime -Arguments $Arguments
    $originalWslEnv = $env:WSLENV
    try {
        $env:WSLENV = Get-WslEnvironmentForwarding
        return Start-Process -FilePath 'wsl' -ArgumentList $wslArguments `
            -RedirectStandardOutput $StandardOutputPath `
            -RedirectStandardError $StandardErrorPath `
            -NoNewWindow `
            -PassThru
    }
    finally {
        $env:WSLENV = $originalWslEnv
    }
}

function Stop-ContainerComposeLogStream {
    [CmdletBinding()]
    param(
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory)]
        [string]$ComposePath
    )

    if ($Process -and -not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
    }

    # Killing the wsl.exe wrapper does not stop the compose process running inside WSL.
    $composeFile = [regex]::Escape((ConvertTo-WslPath -WindowsPath $ComposePath))
    & wsl -d (Get-WslDistro) -- pkill -f -- "$composeFile.*logs --follow" 2>$null | Out-Null
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

function Get-RootEnvFilePath {
    $envFilePath = Join-Path (Split-Path -Parent $PSScriptRoot) '.env'
    if (-not (Test-Path -LiteralPath $envFilePath -PathType Leaf)) {
        throw "Root .env was not found at '$envFilePath'. Copy .env.example to .env and set ACCEPT_EULA=Y and SQL_PASSWORD."
    }

    return $envFilePath
}
