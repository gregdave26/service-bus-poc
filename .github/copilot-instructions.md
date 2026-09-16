# Repository Instructions - Service Bus POC

## Design Principles

Apply the user's instruction sets for [Clean Code](C:/Users/dg15938/.copilot/instructions/clean-code.md), [SOLID](C:/Users/dg15938/.copilot/instructions/solid-principles.md), and [GRASP](C:/Users/dg15938/.copilot/instructions/grasp-principles.md).

Prefer self-documenting code over comments. Comment only to explain non-obvious reasoning, business rules, or trade-offs — never to restate what the code does.

## What This Project Is

A proof-of-concept demonstrating Azure Service Bus topic pub/sub with **filtered subscriptions**, fronted by an **APIM API** that validates and forwards events, with **JSON Schema** contracts.

- **Producer** publishes events to a Service Bus topic.
- **Consumers** (Carwash, DigitalChannels, Insurance, ParksResorts) each receive only the events matching their subscription filter.
- **Contracts** in `/contracts/*.schema.json` define event shapes and are the source of truth.
- **Infrastructure** is Bicep; local development uses the Service Bus emulator in containers.

## Key Decisions

- **ADR-004:** Async-first throughout; no blocking calls.
- **ADR-006:** All configuration from environment variables; no secrets in code.
- **ADR-007:** xUnit + Moq, ≥80% coverage.

Full set in `docs/decisions/`.

## Language-Specific Guidance

C#/.NET conventions are in `.github/instructions/dotnet.instructions.md`.
Bicep and Azure naming conventions apply automatically to infrastructure files.
