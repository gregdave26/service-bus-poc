# Azure Service Bus Emulator - Local Topology

Local equivalent of the Azure `contact.events` topic described in [PROJECT_BRIEF.md](../../PROJECT_BRIEF.md) and [ADR-001](../../docs/decisions/001-emulator-first-validation.md). Runs the official Microsoft Service Bus emulator plus its Azure SQL Edge dependency via Compose. Podman is the default runtime; Docker is available as a fallback (see [ADR-016](../../docs/decisions/016-podman-container-runtime.md)).

## Topology

| Subscription | Filter (SQL) | Consumer |
|---|---|---|
| `digital-channels` | none — receives all messages | ServiceBusPoc.DigitalChannels |
| `insurance` | `hasInsurance = true` | ServiceBusPoc.Insurance |
| `parks-resorts` | `hasParksResorts = true` | ServiceBusPoc.ParksResorts |
| `carwash` | `hasCarwashProduct = true` | ServiceBusPoc.Carwash |

Defined declaratively in [config.json](./config.json), following the [official emulator config schema](https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/ServiceBus-Emulator/Config/Config.json). Filter property names (`hasInsurance`, `hasParksResorts`, `hasCarwashProduct`) match the application properties producers must set on each message — see [contracts/attributes.schema.json](../../contracts/attributes.schema.json).

The `verifier-*` subscriptions mirror the four consumer subscriptions with identical filters. The routing verifier receives from these so it does not compete with live consumers for the same messages.

| Verifier subscription | Mirrors |
|---|---|
| `verifier-digital-channels` | `digital-channels` |
| `verifier-insurance` | `insurance` |
| `verifier-parks-resorts` | `parks-resorts` |
| `verifier-carwash` | `carwash` |


## Prerequisites

- Podman (rootless) installed in WSL, with `podman-compose` as the compose provider (default), **or** Docker Engine with the Compose v2 plugin installed in WSL (`CONTAINER_RUNTIME=docker`)
- WSL distribution name in `CONTAINER_WSL_DISTRO` (default `Ubuntu`)
- Accept the [emulator EULA](https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt) — required before the container will start

Both runtimes run inside WSL. The PowerShell scripts call them through `wsl`, using the helper in [scripts/container-runtime.ps1](../../scripts/container-runtime.ps1). Select the runtime with the `CONTAINER_RUNTIME` shell environment variable: `podman` (default) or `docker`. It is read from the shell, not from `.env`. Podman and Docker both bind ports `5672` and `5300`, so only one runtime can run the emulator at a time.

```powershell
$env:CONTAINER_RUNTIME = 'docker'   # current session only; omit to use podman
```

## Running

```powershell
Copy-Item .env.example .env   # from the repository root
# Edit .env: set ACCEPT_EULA=Y and a strong SQL_PASSWORD
```

The start, status, connectivity and debug scripts read the repository-root `.env` and fail with a message if it is missing. `stop-service-bus-poc.ps1` still stops the emulator without it, because teardown does not need the SQL password. Its emulator placeholders (`ACCEPT_EULA`, `SQL_PASSWORD`, `SQL_WAIT_INTERVAL`, `EMULATOR_HTTP_PORT`) are documented in [.env.example](../../.env.example).

The usual entry point is the script, which uses the configured runtime:

```powershell
..\..\scripts\start-servicebus-emulator.ps1
```

To run Compose directly, open a WSL shell in the repository's `infra/servicebus` folder (for example `/mnt/c/path/to/service-bus-poc/infra/servicebus`):

```bash
PODMAN_COMPOSE_PROVIDER=podman-compose podman compose --env-file ../../.env pull
PODMAN_COMPOSE_PROVIDER=podman-compose podman compose --env-file ../../.env up -d
```

Compose reuses existing containers and local images by default. The `pull` step
refreshes the emulator images explicitly before starting. With Docker, run `docker compose` with the same arguments, without the `PODMAN_COMPOSE_PROVIDER` prefix.

The emulator listens on:
- `localhost:5672` — AMQP (used by the .NET apps via the Service Bus SDK)
- `localhost:5300` (configurable) — management/health-check HTTP API

Stop and remove containers:

```bash
PODMAN_COMPOSE_PROVIDER=podman-compose podman compose --env-file ../../.env down
```

Or run `..\..\scripts\stop-service-bus-poc.ps1` from PowerShell to stop the POC processes and the emulator together.

## Connecting from .NET apps

Point `ServiceBusSettings` (bound from the `ServiceBus:*` environment variables, see [ADR-006](../../docs/decisions/006-configuration-env-vars.md)) at the emulator:

```powershell
$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true"
$env:ServiceBus__Namespace = "sbemulatorns"
$env:ServiceBus__TopicName = "contact.events"
$env:ServiceBus__SubscriptionName = "carwash"   # varies per consumer
```

`scripts/run-local-poc.ps1` sets these automatically once producer/consumer messaging (Phase 2.2/2.3) is implemented.

## Topology validation

The emulator does not expose the Azure Service Bus administration API used by
`ServiceBusAdministrationClient`. `scripts/validate-emulator-topology.ps1`
therefore validates the emulator's mounted `config.json` declarative topology.
Azure Service Bus connections continue to use the administration client path.

## Limitations

Per [ADR-001](../../docs/decisions/001-emulator-first-validation.md): the emulator is local/sequential only, has no SLA, uses AMQP over TCP only (no HTTPS/WebSockets), and loses all entities and messages on restart — `config.json` is reapplied fresh each time the container starts.
