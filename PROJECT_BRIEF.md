# PROJECT_BRIEF.md - Enterprise Contact Events POC

> Last updated: 2026-09-16

## 1. Goal and Users

Proof of concept demonstrating `contact.events` as an Azure Service Bus enterprise messaging backbone. CRM/MDM and product/holding systems publish contact-related changes, while digital channels and business units consume all or filtered events. Carwash independently consumes carwash-product contacts and exposes a verification API that Pulse or mock Pulse calls.

## 2. Current Scope

**In scope**
- An Azure Service Bus namespace and `contact.events` topic for `ContactUpdated` and product/holding-change events.
- Producer integration from CRM/MDM and product/holding systems.
- Consumer routing to digital channels, Insurance, Parks & Resorts, and Carwash.
- Subscription filters for Insurance (`hasInsurance = true`), Parks & Resorts (`hasParksResorts = true`), and Carwash (`hasCarwashProduct = true`).
- Carwash member-verification API at `POST /carwash/v1/verify`, called by Pulse or mock Pulse.
- Bicep IaC for the above, deployable to a real Azure subscription.
- .NET 10 console harness that runs the same topology against the official Azure Service Bus emulator (Docker Compose) to deterministically prove filter routing without cloud credentials.

**Out of scope**
- Production sizing, private networking, custom domains, multi-region, and publisher authentication design.
- Consumer-facing HTTP delivery/webhooks, UI, persistence, sagas, sessions, load testing.

## 3. Stack and Architecture

- Runtime/language: .NET 10 / C# (nullable enabled), Bicep, JSON Schema.
- Frameworks/libraries: Azure Service Bus SDK, Microsoft.Extensions.Hosting/Options, xUnit, Moq.
- Data/services: Azure Service Bus (cloud + emulator).
- Deployment: Bicep via Azure CLI (`az deployment group create`); local emulator via Docker Compose.
- Tests/checks: `dotnet build`, `dotnet test`, `az bicep build`.
- Architecture reference: [`docs/architecture/project-goal.md`](docs/architecture/project-goal.md).

The architecture reference defines the project context:
1. **Producers:** CRM/MDM and product/holding systems publish contact-related events.
2. **Backbone:** Azure Service Bus routes events through the `contact.events` topic.
3. **Consumers:** digital channels receive all events; Insurance, Parks & Resorts, and Carwash receive capability-filtered events.
4. **Carwash paths:** Carwash consumes `hasCarwashProduct=true` events; separately, Pulse or mock Pulse calls Carwash's verification API.

## 4. Key Files

| Area | Path | Purpose |
|---|---|---|
| Event contract | `contracts/` | Canonical `contact.events` schemas used by producers and consumers |
| App | `src/ServiceBusPoc.*/` | Producer, consumers, Carwash verification API, scenario verifier, settings |
| Tests | `tests/ServiceBusPoc.Tests/` | Schema, routing, settings, scenario tests |
| Azure IaC | `infra/main.bicep`, `infra/modules/` | `contact.events` topology, filtered subscriptions, and least-privilege RBAC (planned, not yet implemented) |
| Local infra | `infra/servicebus/compose.yaml`, `config.json` | Emulator topology equivalent to Bicep |
| Scripts | `scripts/` | Local run and Azure deploy |
| Agents | `.github/agents/` | Producer, Dev, QA |
| Instructions | `.github/instructions/`, `.github/copilot-instructions.md` | Stack-specific Copilot guidance |

## 5. How to Work

- Setup: .NET 10 SDK, Docker Desktop, Azure CLI + Bicep, PowerShell 7+ (see prerequisites in README).
- Run (local): `scripts/run-local-poc.ps1`
- Test: `dotnet test`
- Deploy (Azure, optional/credentialed): `scripts/deploy-azure.ps1`
- Repository rules: see `.github/copilot-instructions.md`

## 6. Safety and Constraints

- Never commit subscription keys, connection strings, or other secrets; use secure parameters/environment variables.
- Emulator is local/sequential only, no SLA, no persistence across restarts, AMQP TCP only.

## 7. Current State

**Working**
- Event contracts (`EventEnvelope<TData>`, `ContactUpdatedEvent`, product/holding-change, attributes) and matching JSON Schemas in `contracts/`.
- .NET 10 solution scaffolded: Producer, DigitalChannels, Insurance, ParksResorts, Carwash, and Verifier console apps, plus a shared test project.
- Local emulator configuration exists at `infra/servicebus/compose.yaml` and `infra/servicebus/config.json`.
- Carwash HTTP member-verification API is implemented; its API test files exist. Pulse or mock Pulse calls this API.

**Known issues**
- Bicep IaC for the Azure Service Bus topology (ADR-008) is approved but not yet implemented.
- Producer and consumer messaging services remain stubs; no end-to-end Service Bus message flow is implemented yet.
- No external/APIM ingress exists yet; producers publish directly. Not currently in scope (see `.github/copilot-instructions.md`).

**Next**
- Remaining plan items: emulator topology validation, messaging implementations, routing/schema/settings tests, local-script completion, Bicep infrastructure, and final end-to-end validation.

## 8. Team and Handoff

- Producer (`@ai-team-producer` / Remy): scope, coordination, and merge.
- Dev (`@ai-team-dev` / Nova+Sage+Milo): implementation and verification.
- QA (`@ai-team-qa` / Ivy): optional independent behavioral verification, especially for contact-event filters, routing, and the independent Carwash consumer and verification API paths.

## Where to record decisions

Material decisions are recorded as ADRs under `docs/decisions/`.
