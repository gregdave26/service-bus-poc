# PROJECT_BRIEF.md - Enterprise Contact Events POC

> Last updated: 2026-09-14

## 1. Goal and Users

Proof of concept demonstrating `contact.events` as an Azure Service Bus enterprise messaging backbone. CRM/MDM and product/holding systems publish contact-related changes, while digital channels and business units consume all or filtered events. Carwash consumes carwash-product contacts and integrates them with the Pulse Contact CRUD API.

## 2. Current Scope

**In scope**
- An Azure Service Bus namespace and `contact.events` topic for `ContactUpdated` and product/holding-change events.
- Producer integration from CRM/MDM and product/holding systems.
- Consumer routing to digital channels, Insurance, Parks & Resorts, and Carwash.
- Subscription filters for Insurance (`hasInsurance = true`), Parks & Resorts (`hasParksResorts = true`), and Carwash (`hasCarwashProduct = true`).
- Carwash integration with the Pulse Contact CRUD API for matching contact events.
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
4. **Integration:** Carwash applies matching events through the Pulse Contact CRUD API.

## 4. Key Files

| Area | Path | Purpose |
|---|---|---|
| Event contract | `contracts/` | Canonical `contact.events` schemas used by producers and consumers |
| App | `src/ServiceBus.Poc/` | Producer, consumer, Carwash-to-Pulse integration, scenario verifier, settings |
| Tests | `tests/ServiceBus.Poc.Tests/` | Schema, routing, settings, scenario tests |
| Azure IaC | `infra/main.bicep`, `infra/modules/` | `contact.events` topology, filtered subscriptions, and least-privilege RBAC |
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
- (to be updated as implementation proceeds)

**Known issues**
- None yet - implementation not started.

**Next**
- Follow the approved plan: agent/instruction setup -> event contract -> .NET scaffold -> Bicep -> emulator infra -> implementation -> tests -> scripts -> docs -> validation.

## 8. Team and Handoff

- Producer (`@ai-team-producer` / Remy): scope, coordination, and merge.
- Dev (`@ai-team-dev` / Nova+Sage+Milo): implementation and verification.
- QA (`@ai-team-qa` / Ivy): optional independent behavioral verification, especially for contact-event filters, routing, and the Carwash-to-Pulse integration.

## Where to record decisions

Material decisions are recorded as ADRs under `docs/decisions/`.
