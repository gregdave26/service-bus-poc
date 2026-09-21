# Implementation Plan & MVP - Enterprise Contact Events POC

## Executive Summary

This document proposes a phased implementation approach for the Azure Service Bus contact events POC. The plan prioritizes validating the core messaging backbone, filter routing, and Carwash integration in the fastest, leanest way possible. The MVP delivers end-to-end event flow with local emulator testing before moving to cloud deployment.

**Current status (2026-09-21):** The solution builds with 0 warnings/errors and `dotnet test` passes 100 tests. Local emulator files exist under `infra/servicebus/`. ProducerService, ServiceBusMessagePublisher, ContactEventPublisher, the shared `IServiceBusSender`/`IServiceBusReceiver` adapters, SubscriptionConsumerRunner, all four consumer wrappers, dashboard reporting, and TopologyValidator are implemented. VerifierService remains a stub with a TODO and currently performs topology validation only. Emulator routing has not been claimed as end-to-end proven.

---

## Phase 1: Foundation & Setup (MVP Prep)

**Goal:** Establish project structure, event contracts, and scaffold .NET application.

### 1.1 Project Structure & Tooling
- [x] Repository initialized with .github/ directories for agents and instructions
- [x] Create `contracts/` folder with JSON Schema definitions for canonical event types
- [x] Create `src/ServiceBusPoc.*/` .NET console project scaffolds (Producer, DigitalChannels, Insurance, ParksResorts, Carwash, Verifier)
- [x] Create `tests/ServiceBusPoc.Tests/` xUnit test project
- [x] Create `infra/servicebus/` local-emulator configuration
- [ ] Create Bicep module structure
- [x] Create `scripts/` folder for local and Azure deployment scripts
- [x] Create `docs/decisions/` folder for ADRs

**Deliverable:** Folder structure matching the key-files table in the brief; project files are scaffolded.

### 1.2 Event Contracts (JSON Schema)
Define canonical event schemas in `contracts/` as JSON Schema:

**Status:** ✅ Done — `contracts/envelope.schema.json`, `contracts/contact-updated-v1.schema.json`, `contracts/product-holding-change-v1.schema.json`, and `contracts/attributes.schema.json` exist, with matching C# DTOs in `src/ServiceBusPoc.Core/Contracts/` (`EventEnvelope<TData>`, `ContactUpdatedEvent`, etc.).

**Files:**
- `contacts/contact-updated-v1.schema.json` — Core contract for contact property changes
- `products/product-holding-change-v1.schema.json` — Product/holding system events
- `metadata/envelope.schema.json` — Event envelope (source, timestamp, correlation ID, etc.)

**Event Structure:**
```json
{
  "id": "event-id",
  "type": "contact.updated",
  "source": "crm-mdm",
  "timestamp": "ISO-8601",
  "dataVersion": "1.0",
  "correlationId": "trace-id",
  "data": {
    "contactId": "string",
    "firstName": "string",
    "lastName": "string",
    "email": "string",
    "phone": "string",
    "attributes": {
      "hasInsurance": "boolean",
      "hasParksResorts": "boolean",
      "hasCarwashProduct": "boolean"
    }
  }
}
```

**Deliverable:** JSON Schema files with matching C# DTOs. Dedicated schema tests remain outstanding.

---

## Phase 2: MVP Core Implementation (Local Emulator)

**Goal:** Implement end-to-end producer → Service Bus → consumer routing with local Azure Service Bus emulator (Docker Compose).

**Status:** ⏳ In progress — the producer, shared messaging adapters/publisher, consumer runner, four consumer wrappers, dashboard reporting, and topology validator are implemented. The Carwash HTTP verification API is also implemented. VerifierService remains a stub with a TODO and only validates topology; local emulator routing is not yet end-to-end proven.

### 2.1 Azure Service Bus Emulator Topology (Docker Compose)
**File:** `infra/servicebus/compose.yaml` and `config.json`

**Status:** ⏳ Configuration exists — `infra/servicebus/compose.yaml` and `config.json` define the local emulator setup and filtered subscriptions. End-to-end routing validation remains outstanding.

**Topology:**
- Service Bus namespace (emulator)
- `contact.events` topic
- Subscriptions:
  - `digital-channels` (no filter — receives all)
  - `insurance` (filter: `hasInsurance = true`)
  - `parks-resorts` (filter: `hasParksResorts = true`)
  - `carwash` (filter: `hasCarwashProduct = true`)

**Deliverable:** docker-compose.yaml that spins up emulator with full topology; `config.json` mapping subscriptions

### 2.2 .NET Console Producer
**File:** `src/ServiceBusPoc.Producer/Services/ProducerService.cs`

**Status:** ✅ Implemented — `ProducerService`, `ServiceBusMessagePublisher`, and `ContactEventPublisher` publish envelope-wrapped events through the shared sender adapter.

**Responsibilities:**
- Parse event from JSON or CLI args
- Serialize to JSON with envelope
- Send to Service Bus topic
- Log result (success/error)

**Features:**
- Constructor injection for explicit service dependencies
- Configuration via `IConfiguration` and options pattern
- Error handling with retry logic (exponential backoff)
- Structured logging with `ILogger`

**Usage Example:**
```
dotnet run -- --producer \
  --contact-id C001 \
  --first-name John \
  --has-insurance true \
  --has-carwash-product true
```

**Deliverable:** Producer class + settings model; can emit test events to local emulator

### 2.3 .NET Console Consumers
**Files:**
- `src/ServiceBusPoc.DigitalChannels/`
- `src/ServiceBusPoc.Insurance/`
- `src/ServiceBusPoc.ParksResorts/`
- `src/ServiceBusPoc.Carwash/`

**Status:** ✅ Implemented — `SubscriptionConsumerRunner`, shared receiver adapter, and all four consumer wrappers are implemented. Dashboard reporting is an implemented supporting surface. Dedicated consumer tests and end-to-end emulator routing proof remain outstanding.

**Shared Consumer Base:**
- Listen to subscription
- Deserialize event + validate schema
- Apply business logic
- Complete message

**Per-Consumer Logic:**
- **DigitalChannels:** Log all received events (validation harness)
- **Insurance:** Log events with `hasInsurance = true`
- **ParksResorts:** Log events with `hasParksResorts = true`
- **Carwash:** Process contacts received through the `hasCarwashProduct=true` subscription. This consumer is independent of the verification API in 2.4.

**Deliverable:** Runnable consumers for all 4 subscriptions; async message handling

**Supporting surface:** Dashboard reporting is implemented for service state, heartbeats, and published/received event reporting.

### 2.4 Carwash Verification API
**File:** `src/ServiceBusPoc.Carwash/Api/CarwashApiServer.cs`

**Status:** ✅ Implemented ahead of the messaging work — Carwash exposes `POST /carwash/v1/verify`, which Pulse or mock Pulse calls (see `src/ServiceBusPoc.Carwash/API.md`). Request/response contracts, validation, error handling, and Carwash API test files are present. The mock verification rule returns true for RAC IDs beginning with `VALID` (case-insensitive). This HTTP API and the Service Bus consumer in 2.3 are separate integration points.

**Responsibilities:**
- Accept a RAC member ID and return the verification result.
- Validate request and return the documented HTTP error response.

**Deliverable:** Carwash verification endpoint and its request/response contract. It does not call Pulse or depend on Service Bus message processing.

### 2.5 Scenario Verifier & Test Harness
**File:** `src/ServiceBusPoc.Verifier/Services/VerifierService.cs`

**Status:** ⏳ Stub only — `VerifierService` contains a TODO and currently performs topology validation only; routing scenarios and schema/error scenarios are not implemented.

**Purpose:** Run deterministic end-to-end scenarios locally without real cloud infrastructure.

**Scenarios:**
1. **Filter Routing Validation**
   - Emit contact with `hasInsurance=true, hasParksResorts=false, hasCarwashProduct=false`
   - Assert Insurance subscription receives it, others do not
   - Repeat for all attribute combinations (8 test cases)

2. **Carwash Consumer Validation**
   - Emit contact with `hasCarwashProduct=true`
   - Assert Carwash consumer receives event
   - Assert the consumer does not receive events when `hasCarwashProduct=false`

3. **Schema Validation**
   - Emit malformed event (missing required field)
   - Assert consumer rejects gracefully with schema error

**Deliverable:** Harness that runs scenarios sequentially, logs results, exits with pass/fail status

---

## Phase 3: Unit & Integration Tests (MVP Validation)

**Goal:** Prove filter routing and integration logic with automated tests.

**Status:** ✅ Partial — the solution build passes with 0 warnings/errors and `dotnet test` passes 100 tests. Tests are present for the Carwash API, ProducerService, ProducerConfiguration, ProducerIntegration, ServiceBusMessagePublisher, and TopologyValidator. No dedicated consumer, schema, or settings tests are present.

### 3.1 Schema Tests
**File:** `tests/ServiceBusPoc.Tests/SchemaTests.cs`

**Status:** ⏳ Not started.

- Validate event DTOs serialize/deserialize correctly
- Validate envelope structure
- Test schema validation against invalid data
- Confirm required fields enforced

### 3.2 Filter Routing Tests
**File:** `tests/ServiceBusPoc.Tests/FilterRoutingTests.cs`

**Status:** ⏳ Not started.

- Mock Service Bus client
- Send event to topic
- Verify correct subscriptions receive message (mock filter evaluation)
- Test all 8 attribute combinations (2³ boolean attributes)
- Assert no cross-subscription leakage

### 3.3 Carwash API and Consumer Tests
**File:** `tests/ServiceBusPoc.Tests/CarwashIntegrationTests.cs`

**Status:** ✅ Carwash API coverage is present. Dedicated Service Bus consumer tests are not present; they must remain independent of the verification API.

- Test verification API request validation and responses.
- Test Carwash consumer routing separately from the HTTP API.

### 3.4 Settings & Configuration Tests
**File:** `tests/ServiceBusPoc.Tests/SettingsTests.cs`

**Status:** ⏳ Not started.

- Validate ServiceBusSettings loads from config
- Test missing/invalid connection string handling
- Test environment variable override

**Coverage:** No coverage measurement or ≥80% result is asserted by this plan; coverage remains a gap to measure.

---

## Phase 4: Infrastructure as Code (Bicep & Cloud Deployment)

**Goal:** Define production-grade Azure Service Bus infrastructure; deployable via `az bicep build` and `az deployment group create`.

**Status:** ⏳ Not started — local emulator files exist under `infra/servicebus/`, but no Bicep files exist yet. ADR-008 approves Bicep as the IaC choice; implementation is still pending.

### 4.1 Bicep Modules
**Files:**
- `infra/modules/servicebus-namespace.bicep` — Namespace with RBAC
- `infra/modules/topic.bicep` — Topic with subscriptions
- `infra/modules/role-assignments.bicep` — Least-privilege RBAC rules

**Features:**
- Parameterized resource names (follow CAF naming conventions)
- Subscription filters (insurance, parks-resorts, carwash)
- Managed Identity support for consumer authentication
- Key Vault integration for connection strings
- Outputs: namespace, topic, subscription names

### 4.2 Main Bicep Template
**File:** `infra/main.bicep`

- Orchestrate all modules
- Accept parameters (environment, location, etc.)
- Validate CAF naming conventions
- Generate deployment outputs

### 4.3 Bicep Parameter Files
**Files:**
- `infra/main.bicepparam` — Default parameters
- `infra/prod.bicepparam` — Production overrides (optional for POC)

### 4.4 ADR: Cloud vs. Local Strategy
**File:** `docs/decisions/001-emulator-first-validation.md`

**Status:** ✅ Done — ADR-001 through ADR-009 are recorded under `docs/decisions/`; ADR-009 defines the independent Carwash consumer and verification API boundary.

Document the decision to validate with local emulator before cloud deployment, including:
- Rationale: faster iteration, no cost, deterministic testing
- Trade-offs: emulator limitations (single instance, TCP only)
- Validation path: emulator → cloud optional

**Deliverable:** Full Bicep scaffold; can validate with `az bicep build`

---

## Phase 5: Deployment & Runtime Scripts

**Goal:** Automate local emulator and Azure cloud deployment.

**Status:** ⏳ Partial — `scripts/run-local-poc.ps1` exists and starts consumers and the verifier. Its presence does not prove end-to-end emulator routing. `deploy-azure.ps1` is not started.

### 5.1 Local POC Script
**File:** `scripts/run-local-poc.ps1`

**Status:** ⏳ Exists and starts consumers/verifier; end-to-end completion and routing results are not yet proven.

**Steps:**
1. Spin up Docker Compose (Service Bus emulator)
2. Wait for emulator readiness
3. Apply config.json topology
4. Start all 4 consumers in background tasks
5. Run producer with test scenarios
6. Collect consumer logs
7. Generate a report for all 8 boolean routing combinations
8. Cleanup

**Deliverable:** One-command local POC execution

### 5.2 Azure Deployment Script
**File:** `scripts/deploy-azure.ps1`

**Status:** ⏳ Not started — depends on Phase 4 Bicep.

**Steps:**
1. Validate bicep syntax
2. Authenticate Azure CLI
3. Create/validate resource group
4. Deploy Bicep template
5. Retrieve connection string from Key Vault
6. Seed emulator topology config (if applicable)

**Deliverable:** One-command cloud deployment (gated by environment/credentials)

---

## Phase 6: Documentation & Handoff

**Goal:** Enable team members (Dev, QA, Producer) to understand and operate the system.

**Status:** ⏳ Partial — `docs/architecture/project-goal.md`, `docs/decisions/` (ADRs), `README.md`, and `PROJECT_BRIEF.md` exist. `DEVELOPER.md`, `RUNBOOK.md`, and `docs/api.md` are not started (Carwash's own `API.md` covers the HTTP API only).

### 6.1 Architecture Documentation
**File:** `docs/architecture/project-goal.md`

**Status:** ✅ Done.

- System diagram (producers → topic → subscriptions → consumers)
- Event flow walkthrough
- Filter routing rules
- Carwash consumer and verification-API flows

### 6.2 Developer Guide
**File:** `docs/DEVELOPER.md`

**Status:** ⏳ Not started.

- Prerequisites (.NET 10, Docker, Azure CLI, PowerShell 7+)
- Quick start (clone, `scripts/run-local-poc.ps1`)
- Event contract reference
- Adding a new consumer (template + checklist)
- Adding a new event type
- Troubleshooting

### 6.3 Operational Runbook
**File:** `docs/RUNBOOK.md`

**Status:** ⏳ Not started.

- How to run locally
- How to deploy to Azure
- Common errors and fixes
- Log inspection
- Manual testing procedures

### 6.4 API Reference
**File:** `docs/api.md`

**Status:** ⏳ Not started as a consolidated doc — `src/ServiceBusPoc.Carwash/API.md` documents the Carwash HTTP API in isolation.

- Producer CLI reference
- Consumer configuration
- Pulse API integration (schema, error codes)
- Event schema reference

---

## MVP Scope Definition

**MVP = Phases 1–3 + Phase 5.1 (Local Scripts)**

**Status:** ⏳ In progress — core producer/consumer messaging, dashboard reporting, and topology validation are implemented, and the solution build/tests pass. The verifier scenarios, dedicated consumer/schema/settings tests, measured coverage, and end-to-end emulator routing proof remain outstanding. Bicep is not implemented.

The MVP aims to demonstrate:
1. ⏳ End-to-end event flow: producer → topic → subscriptions → consumers (implemented surfaces exist; emulator routing is not yet proven)
2. ⏳ Filter routing validation (all 8 scenarios)
3. ✅ Carwash exposes the verification API that Pulse or mock Pulse calls; its mock verification rule is implemented
4. ⏳ Test coverage — 100 tests pass, including Carwash API, ProducerService, ProducerConfiguration, ProducerIntegration, ServiceBusMessagePublisher, and TopologyValidator; dedicated consumer/schema/settings tests are outstanding
5. ⏳ Reproducible local execution via Docker + script
6. ⏳ Schema validation and error handling — contracts and DTOs exist; dedicated schema tests and verifier scenarios are not implemented

**MVP Does NOT Include:**
- Cloud deployment to real Azure subscription
- Production Bicep (deferred to Phase 4)
- Live Pulse integration beyond the Carwash verification API contract
- Persistence, sagas, load testing, custom domains

**MVP Validation Criteria:**
- ⏳ `scripts/run-local-poc.ps1` completes with all 8 routing scenarios passing
- ✅ Carwash exposes `POST /carwash/v1/verify` for Pulse or mock Pulse to call
- ✅ No secrets committed; all sensitive config externalized
- ⏳ README and DEVELOPER.md enable onboarding (README exists; DEVELOPER.md not started)

---

## Implementation Sequence & Dependencies

```
Phase 1: Foundation
├── Project structure
├── Event contracts
└── .NET project scaffold

Phase 2: Core MVP Implementation
├── Emulator docker-compose
├── Producer
├── 4 Consumers (in parallel)
├── Carwash verification API (complete)
└── Scenario verifier

Phase 3: Tests (parallel with Phase 2)
├── Schema tests
├── Filter routing tests
├── Carwash API and independent consumer tests
└── Settings tests

Phase 5.1: Local Scripts
└── run-local-poc.ps1

Phase 4: Cloud IaC (after MVP)
├── Bicep modules
├── Main template
└── Deployment script

Phase 6: Documentation (parallel with others)
├── Architecture
├── Developer guide
├── Runbook
└── API reference
```

---

## Key Technical Decisions (to be formalized as ADRs)

1. **Emulator First:** Validate with local Azure Service Bus emulator before cloud (faster, cheaper, deterministic)
2. **Mock Verification Rule in MVP:** Carwash exposes a verification API for Pulse or mock Pulse to call; live integration is deferred.
3. **Independent Carwash Paths:** The `hasCarwashProduct=true` consumer and the verification API have no runtime dependency on each other.
4. **Dependency Injection:** Use constructor injection with `Microsoft.Extensions.DependencyInjection` for all consumers and producers
5. **Structured Logging:** Use `ILogger` with JSON-structured output for machine-parseable logs
6. **Schema Validation:** Use JSON Schema for event contracts; C# DTO validation via FluentValidation or data annotations
7. **Configuration:** No hardcoded secrets; all via `IConfiguration` (appsettings.json, environment variables, Key Vault)

---

## Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Emulator incompatibility | MVP can't validate cloud behavior | Test subscription filters with explicit edge cases; compare with cloud docs |
| Event schema churn | Consumers break on schema changes | Version schemas explicitly; use envelope with dataVersion field |
| Filter logic errors | Wrong subscriptions receive events | Write exhaustive filter tests (2³ scenarios); manual verification via logs |
| Secret leakage | Credentials committed to repo | Pre-commit hook + review rule: reject any connection strings in code |

---

## Success Metrics (MVP Complete)

- [ ] All 8 filter routing scenarios pass (emulator + tests)
- [x] Carwash exposes its verification API contract for Pulse or mock Pulse callers
- [ ] No schema validation errors on valid events
- [ ] Graceful error handling on invalid events (schema, network, malformed)
- [ ] Single command (`scripts/run-local-poc.ps1`) validates entire system
- [ ] Test coverage ≥80% for business logic
- [ ] Sensitive configuration remains externalized
- [ ] README enables new dev onboarding in <30 min
- [ ] All code builds and tests pass in CI

---

## Post-MVP Roadmap (Phase 4+)

1. **Cloud Deployment** (Phase 4): Bicep for real Azure; deploy to actual subscription with credentials
2. **Live Pulse Integration** (Phase 4.1): Connect Pulse to the Carwash verification API; replace the mock verification rule as needed.
3. **Persistence** (Future): Store verification data as required; expose a query API if needed.
4. **Sagas & Orchestration** (Future): Handle multi-step workflows (e.g., contact validated → provision in system X)
5. **Load Testing** (Future): Validate throughput, latency, subscription filter performance
6. **Producer Integration** (Future): Integrate real CRM/MDM and product systems as publishers
7. **Consumer Integration** (Future): Connect Insurance, Parks & Resorts digital channels

---

## Roles & Responsibilities

| Role | Responsibility |
|------|-----------------|
| **Producer (Remy)** | Scope coordination, plan approval, handoff to Dev, merge PR |
| **Dev (Nova+Sage+Milo)** | Implement all phases 1–5; write tests; author ADRs; deliver working MVP |
| **QA (Ivy)** | Independent verification of filter routing and the separate Carwash consumer/API paths; sign-off on MVP validation criteria |

---

## Timeline Estimate (Dev Time)

| Phase | Effort | Duration |
|-------|--------|----------|
| Phase 1 | 1–2 days | Structure, contracts, scaffolds |
| Phase 2 | 3–5 days | Emulator, producer, consumers, verifier |
| Phase 3 | 2–3 days | Tests (parallel with Phase 2) |
| Phase 5.1 | 1 day | Local scripts |
| **MVP Subtotal** | **7–11 days** | |
| Phase 4 | 2–3 days | Bicep, cloud deploy |
| Phase 6 | 1–2 days | Documentation (parallel) |
| **Full Project** | **10–16 days** | |

---

## Next Steps

1. Prove local emulator startup and filtered routing for the implemented producer and four consumers; do not treat script startup as end-to-end proof.
2. Implement VerifierService scenarios in 2.5, including routing and schema/error checks.
3. Add dedicated consumer, schema, and settings tests; measure meaningful coverage rather than assuming an 80% result.
4. Validate the dashboard reporting surface alongside the local run.
5. Implement Phase 4 Bicep modules and deployment templates after MVP validation.
