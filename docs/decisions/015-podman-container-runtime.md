# ADR 015: Podman as the Local Container Runtime via WSL

**Status:** PROPOSED (awaiting Product Owner sign-off)

**Supersedes:** the Docker Desktop and `docker compose` parts of [ADR-001](001-emulator-first-validation.md). The emulator topology and compose file in ADR-001 still apply.

## Context

The local Azure Service Bus emulator and its Azure SQL Edge dependency run from [`infra/servicebus/compose.yaml`](../../infra/servicebus/compose.yaml). Until now the PowerShell scripts called `docker` and `docker-compose` on the Windows host, which required Docker Desktop.

Docker Desktop for Windows is not endorsed by RAC and must not be used. Podman for Windows is not available for download through approved channels. The developer workstation runs Podman 5.7 rootless and Docker Engine inside WSL Ubuntu, so the local emulator can run without either Windows product.

## Problem Statement

How do we run the local emulator without Docker Desktop while keeping the compose file, the topology, and the PowerShell entry points unchanged, and still allowing Docker where it is already in use?

## Options Considered

1. **Keep Docker Desktop for Windows.** Rejected. RAC does not endorse Docker Desktop.
2. **Install Podman for Windows (Podman machine).** Rejected. The Windows build is not available for download, so it cannot be installed as an approved tool.
3. **Call the Podman and Docker CLIs in the existing WSL distribution from the PowerShell scripts (selected).** Uses runtimes that are available inside WSL, needs no Windows container product, and keeps the compose file as the single source of truth.

## Decision

- Add `scripts/container-runtime.ps1`. It is the only place that knows how to reach the runtime. Every script calls it through `Invoke-ContainerCompose`, `Invoke-ContainerRuntime`, `Start-ContainerRuntimeProcess`, and `Test-ContainerRuntimeAvailable`.
- Select the runtime with the `CONTAINER_RUNTIME` shell environment variable: `podman` (default) or `docker`. Any other value fails fast.
- Select the WSL distribution with `CONTAINER_WSL_DISTRO` (default `Ubuntu`).
- Pin the Podman compose provider to `podman-compose` (`PODMAN_COMPOSE_PROVIDER`). A Docker Compose plugin installed in the same distribution would otherwise be picked up.
- Forward only the emulator variables that are set (`SQL_PASSWORD`, `ACCEPT_EULA`, `SQL_WAIT_INTERVAL`, `EMULATOR_HTTP_PORT`) into WSL through `WSLENV`. An unset variable forwarded as an empty string overrides the env file, which left the SQL password empty during testing.
- Keep `infra/servicebus/compose.yaml` unchanged.

## Consequences

**Positive**
- No Docker Desktop for Windows is needed for the local emulator, which keeps the setup within RAC's endorsed tooling.
- One compose file and one set of script entry points serve both runtimes.
- Docker remains available as a fallback with `CONTAINER_RUNTIME=docker`.

**Negative**
- Scripts depend on WSL and a working `wsl` on the PATH. Windows-only use of Podman is not supported.
- Stopping the log stream needs an extra step inside WSL, because killing the Windows `wsl.exe` process does not end the compose process it started.
- `podman-compose` does not support every `docker compose` flag (for example `logs --no-color`), so the helper omits such flags.

## Risks

- The Podman and Docker emulator stacks both bind host ports `5672` and `5300`. Only one runtime can run the emulator at a time.
- `podman-compose` is a third-party compose provider. Its behaviour differs from Docker Compose v2 and may change between versions.
- The emulator and SQL Edge images use the `latest` tag (as in ADR-001), so the images that run locally depend on the last pull.

## Trade-offs

- We accept a WSL dependency and a runtime-specific shim, because neither Docker Desktop nor Podman for Windows is an available option.
- We keep one compose file rather than maintaining Podman-specific and Docker-specific files.

## Orchestration Lifetime

`podman-compose` and `docker compose` are temporary orchestration for this worktree only. The team intends to move to .NET Aspire and then remove both compose providers. This ADR does not select Aspire. A separate ADR is required before that migration starts. Until then, the compose file and the runtime helper remain the only local orchestration path.

## Implementation Notes

- Helper: [`scripts/container-runtime.ps1`](../../scripts/container-runtime.ps1).
- Configuration: `CONTAINER_RUNTIME` and `CONTAINER_WSL_DISTRO` are documented in [`.env.example`](../../.env.example).
- Scripts migrated: `start-servicebus-emulator.ps1`, `wait-for-servicebus-emulator.ps1`, `stop-service-bus-poc.ps1`, `check-emulator-status.ps1`, `test-emulator-connectivity.ps1`, `run-local-poc.ps1`, `run-dashboard.ps1`, `debug-run.ps1`.

## Related Decisions

- [ADR-001](001-emulator-first-validation.md) — Emulator-first validation; the Docker-specific parts are superseded here.
- [ADR-006](006-configuration-env-vars.md) — Runtime and WSL settings come from environment variables, not code.

## Sign-off

| Role | Decision | Date | Sign-Off |
|------|----------|------|----------|
| Product Owner | ADR-015 | TBD | ⏳ Pending |
