# Service Bus POC - Enterprise Contact Events

A proof-of-concept demonstrating Azure Service Bus as an enterprise messaging backbone for contact events.

## Overview

This project implements a contact event distribution system where CRM/MDM systems publish contact updates to an Azure Service Bus `contact.events` topic. Multiple consumers (digital channels, insurance, parks & resorts, and carwash) subscribe to this topic with capability-based filtering:

- **Digital Channels** receives all contact events
- **Insurance** receives only contacts with `hasInsurance = true`
- **Parks & Resorts** receives only contacts with `hasParksResorts = true`
- **Carwash** receives only contacts with `hasCarwashProduct = true` and provides an API to verify membership

## Project Structure

```
service-bus-poc/
├── src/
│   ├── ServiceBusPoc.Core/                 # Shared contracts, config, utilities
│   ├── ServiceBusPoc.Producer/             # Event producer console app
│   ├── ServiceBusPoc.DigitalChannels/      # Consumer app (all events)
│   ├── ServiceBusPoc.Insurance/            # Consumer app (insurance filter)
│   ├── ServiceBusPoc.ParksResorts/         # Consumer app (parks & resorts filter)
│   ├── ServiceBusPoc.Carwash/              # Consumer app (carwash filter + Pulse integration)
│   └── ServiceBusPoc.Verifier/             # Scenario verification app
├── tests/
│   └── ServiceBusPoc.Tests/                # Shared test project
├── contracts/                               # JSON Schema event contracts
├── docs/
│   ├── architecture/                       # Architecture documentation
│   └── decisions/                          # ADRs (Architecture Decision Records)
└── ServiceBusPoc.slnx                      # Solution file (XML format)
```

## Prerequisites

- **Runtime:** .NET 10.0 SDK or later ([download](https://dotnet.microsoft.com/download))
- **Container runtime:** Docker Desktop (for Service Bus emulator)
- **CLI tools:** Azure CLI 2.65+, Bicep, PowerShell 7+
- **Editor:** VS Code with C# Dev Kit recommended

## Quick Start

### Build

```bash
cd src
dotnet build
```

### Verify No Secrets in Code

```bash
# Search for connection strings and keys
grep -r "Endpoint=sb://" src/ tests/ || echo "✓ No secrets found"
grep -r "SharedAccessKey=" src/ tests/ || echo "✓ No secrets found"
```

### Project Configuration

Configuration is loaded from environment variables only (see [ADR-006](docs/decisions/ADR-006-environment-configuration.md)).

Example environment setup:

```powershell
$env:ServiceBus__ConnectionString = "Endpoint=sb://..."
$env:ServiceBus__Namespace = "my-namespace"
$env:ServiceBus__TopicName = "contact.events"
$env:ServiceBus__SubscriptionName = "insurance"
$env:Carwash__ApiUrl = "https://api.carwash.example.com"
$env:Carwash__MockMode = "true"
```

Each console app reads these variables and configures itself via `IOptions<T>` and dependency injection.

## Architecture

See [docs/architecture/project-goal.md](docs/architecture/project-goal.md) for detailed architecture, system diagram, and topology overview.

## Technology Stack

| Layer | Technology |
|-------|------------|
| **Runtime** | .NET 10 / C# 12 (nullable reference types enabled) |
| **Messaging** | Azure Service Bus SDK 7.18.0 |
| **Configuration** | Microsoft.Extensions.Configuration (environment variables) |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection |
| **Logging** | Microsoft.Extensions.Logging + Console |
| **Testing** | xUnit 2.9.3, Moq (Phase 3+) |
| **Infrastructure** | Bicep (Phase 4+) |
| **Local Dev** | Docker Compose + Service Bus Emulator (Phase 2+) |

## Event Contracts

Canonical event schemas are defined in the `contracts/` folder as JSON Schema draft-7:

- **[contact-updated-v1.schema.json](contracts/contact-updated-v1.schema.json)** — Contact data (contactId, firstName, lastName, email, phone, attributes)
- **[product-holding-change-v1.schema.json](contracts/product-holding-change-v1.schema.json)** — Product/holding lifecycle (placeholder for Phase 2)
- **[envelope.schema.json](contracts/envelope.schema.json)** — Event wrapper (id, type, source, timestamp, dataVersion, correlationId)
- **[attributes.schema.json](contracts/attributes.schema.json)** — Contact capability flags (hasInsurance, hasParksResorts, hasCarwashProduct)

C# DTOs for these schemas live in `src/ServiceBusPoc.Core/Contracts/` with data annotations for validation.

## Development Guidelines

See [.github/copilot-instructions.md](.github/copilot-instructions.md) for stack-specific conventions, async patterns, DI setup, and testing approach.

## Timeline & Phases

| Phase | Scope | Status |
|-------|-------|--------|
| **1** | Project structure, event contracts, .NET scaffolds | ✅ Complete |
| **2** | Producer/consumer implementation, emulator setup | Planned |
| **3** | Unit/integration tests, coverage ≥80% | Planned |
| **4** | Bicep infrastructure, Carwash API integration | Planned |
| **5** | Azure deployment, performance validation | Planned |

## Team

- **Producer/Coordinator** (`@ai-team-producer`) — Scope, planning, merge decisions
- **Dev Team** (`@ai-team-dev`) — Implementation (Nova, Sage, Milo)
- **QA** (`@ai-team-qa`) — Optional behavioral testing and release validation

## References

- [PROJECT_BRIEF.md](PROJECT_BRIEF.md) — Business context and requirements
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) — Full phase breakdown and remaining work
- [docs/decisions/](docs/decisions/) — ADRs for technical decisions
- [docs/archive/](docs/archive/) — Historical status reports and completed-phase writeups, superseded by this README, the ADRs, and IMPLEMENTATION_PLAN.md

## License

Internal use only.
