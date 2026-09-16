# Repository Instructions - Service Bus POC

## Design Principles

Apply the user's instruction sets for [Clean Code](C:/Users/dg15938/.copilot/instructions/clean-code.md), [SOLID](C:/Users/dg15938/.copilot/instructions/solid-principles.md), and [GRASP](C:/Users/dg15938/.copilot/instructions/grasp-principles.md).

Prefer self-documenting code over comments. Comment only to explain non-obvious reasoning, business rules, or trade-offs — never to restate what the code does.

## What This Project Is

A proof-of-concept demonstrating Azure Service Bus as an enterprise messaging backbone for `contact.events`, with **filtered subscriptions** and **JSON Schema** contracts.

- **Producer** publishes `EventEnvelope`-wrapped events (e.g. `ContactUpdated`) to the `contact.events` topic.
- **Consumers** (Carwash, DigitalChannels, Insurance, ParksResorts) each receive only the events matching their subscription filter; Carwash also integrates with the Pulse Contact CRUD API.
- **Contracts** in `/contracts/*.schema.json` define event shapes and are the source of truth.
- **Infrastructure**: Bicep for the Service Bus topology (topic, filtered subscriptions, RBAC) — planned, not yet implemented; local development uses the Service Bus emulator in containers.

There is currently no external/APIM ingress in this project's scope — publishing is done directly by producer apps. If an externally accessible ingestion API becomes required, it should be captured as a new ADR before implementation.

## Key Decisions

- **ADR-004:** Async-first throughout; no blocking calls.
- **ADR-006:** All configuration from environment variables; no secrets in code.
- **ADR-007:** xUnit + Moq, ≥80% coverage.

Full set in `docs/decisions/`.

## Language-Specific Guidance

C#/.NET conventions are in `.github/instructions/dotnet.instructions.md`.
Bicep and Azure naming conventions apply automatically to infrastructure files.
