# Azure Service Bus Emulator - Local Topology

Local equivalent of the Azure `contact.events` topic described in [PROJECT_BRIEF.md](../../PROJECT_BRIEF.md) and [ADR-001](../../docs/decisions/001-emulator-first-validation.md). Runs the official Microsoft Service Bus emulator plus its Azure SQL Edge dependency via Docker Compose.

## Topology

| Subscription | Filter (SQL) | Consumer |
|---|---|---|
| `digital-channels` | none — receives all messages | ServiceBusPoc.DigitalChannels |
| `insurance` | `hasInsurance = true` | ServiceBusPoc.Insurance |
| `parks-resorts` | `hasParksResorts = true` | ServiceBusPoc.ParksResorts |
| `carwash` | `hasCarwashProduct = true` | ServiceBusPoc.Carwash |

Defined declaratively in [config.json](./config.json), following the [official emulator config schema](https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/ServiceBus-Emulator/Config/Config.json). Filter property names (`hasInsurance`, `hasParksResorts`, `hasCarwashProduct`) match the application properties producers must set on each message — see [contracts/attributes.schema.json](../../contracts/attributes.schema.json).

## Prerequisites

- Docker Desktop (Linux containers)
- Accept the [emulator EULA](https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt) — required before the container will start

## Running

```powershell
cd infra/servicebus
Copy-Item .env.example .env
# Edit .env: set ACCEPT_EULA=Y and a strong SQL_PASSWORD
docker compose up -d
```

The emulator listens on:
- `localhost:5672` — AMQP (used by the .NET apps via the Service Bus SDK)
- `localhost:5300` (configurable) — management/health-check HTTP API

Stop and remove containers:

```powershell
docker compose down
```

## Connecting from .NET apps

Point `ServiceBusSettings` (bound from the `ServiceBus:*` environment variables, see [ADR-006](../../docs/decisions/006-configuration-env-vars.md)) at the emulator:

```powershell
$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true"
$env:ServiceBus__Namespace = "sbemulatorns"
$env:ServiceBus__TopicName = "contact.events"
$env:ServiceBus__SubscriptionName = "carwash"   # varies per consumer
```

`scripts/run-local-poc.ps1` sets these automatically once producer/consumer messaging (Phase 2.2/2.3) is implemented.

## Limitations

Per [ADR-001](../../docs/decisions/001-emulator-first-validation.md): the emulator is local/sequential only, has no SLA, uses AMQP over TCP only (no HTTPS/WebSockets), and loses all entities and messages on restart — `config.json` is reapplied fresh each time the container starts.
