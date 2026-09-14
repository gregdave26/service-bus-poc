# PROJECT_BRIEF.md - Service Bus POC

> Last updated: 2026-09-14

## 1. Goal and Users

Proof of concept demonstrating an externally accessible, schema-validated event ingress into Azure Service Bus with filtered pub/sub consumers. Serves as a reference for teams evaluating Azure Service Bus topic/subscription filtering fronted by a validating API Management gateway.

## 2. Current Scope

**In scope**
- Azure Service Bus topic with a SQL-filtered `high-priority` subscription and a correlation-filtered `australia` subscription.
- APIM (Developer SKU, classic gateway) public API that validates a CloudEvents 1.0 JSON envelope against a canonical JSON Schema, then publishes directly to the topic via system-assigned managed identity (least-scope `Azure Service Bus Data Sender` role at the topic).
- Bicep IaC for the above, deployable to a real Azure subscription.
- .NET 10 console harness that runs the same topology against the official Azure Service Bus emulator (Docker Compose) to deterministically prove filter routing without cloud credentials.
- Subscription-key authentication for external publishers.

**Out of scope**
- Production sizing, private networking, custom domains, multi-region, Entra ID auth for publishers.
- Custom ingestion API/Azure Function between APIM and Service Bus.
- Consumer-facing HTTP delivery/webhooks, UI, persistence, sagas, sessions, load testing.

## 3. Stack and Architecture

- Runtime/language: .NET 10 / C# (nullable enabled), Bicep, APIM policy XML, JSON Schema.
- Frameworks/libraries: Azure Service Bus SDK, Microsoft.Extensions.Hosting/Options, xUnit, Moq.
- Data/services: Azure Service Bus (cloud + emulator), Azure API Management.
- Deployment: Bicep via Azure CLI (`az deployment group create`); local emulator via Docker Compose.
- Tests/checks: `dotnet build`, `dotnet test`, `az bicep build`.

Two parallel paths share one canonical event contract (`contracts/order-created-v1.schema.json`):
1. **Azure path**: Publisher -> APIM (subscription key -> validate-content -> send-service-bus-message via managed identity) -> Service Bus topic -> filtered subscriptions.
2. **Local path**: .NET console publisher -> Service Bus emulator topic -> filtered subscriptions -> .NET console consumer verifies exact routing.

## 4. Key Files

| Area | Path | Purpose |
|---|---|---|
| Event contract | `contracts/order-created-v1.schema.json` | Canonical CloudEvents + order data schema used by APIM and .NET |
| App | `src/ServiceBus.Poc/` | Publisher, consumer, scenario verifier, settings |
| Tests | `tests/ServiceBus.Poc.Tests/` | Schema, routing, settings, scenario tests |
| Azure IaC | `infra/main.bicep`, `infra/modules/` | Service Bus, APIM, schema, policy, RBAC |
| Local infra | `infra/servicebus/compose.yaml`, `config.json` | Emulator topology equivalent to Bicep |
| Scripts | `scripts/` | Local run, Azure deploy, API smoke test |
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
- APIM must validate before publishing (`ignore-error="false"`); invalid content must never reach Service Bus.
- Managed identity role assignment must be scoped to the topic, not the namespace.
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
- QA (`@ai-team-qa` / Ivy): optional independent behavioral verification, especially for APIM validation and filter routing.

## Where to record decisions

Material decisions are recorded as ADRs under `docs/decisions/`.
