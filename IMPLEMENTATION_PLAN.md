# Implementation Plan & MVP - Enterprise Contact Events POC

## Executive Summary

This document proposes a phased implementation approach for the Azure Service Bus contact events POC. The plan prioritizes validating the core messaging backbone, filter routing, and Carwash integration in the fastest, leanest way possible. The MVP delivers end-to-end event flow with local emulator testing before moving to cloud deployment.

**Current status (2026-09-16):** Phase 1 (foundation, contracts, scaffolding) is complete. Within Phase 2, the Carwash HTTP verification API is built and tested ahead of schedule, but Service Bus messaging (producer, consumers, emulator topology) is not yet implemented — see per-phase status notes below for detail.

---

## Phase 1: Foundation & Setup (MVP Prep)

**Goal:** Establish project structure, event contracts, and scaffold .NET application.

### 1.1 Project Structure & Tooling
- [x] Repository initialized with .github/ directories for agents and instructions
- [x] Create `contracts/` folder with JSON Schema definitions for canonical event types
- [x] Create `src/ServiceBusPoc.*/` .NET console project scaffolds (Producer, DigitalChannels, Insurance, ParksResorts, Carwash, Verifier)
- [x] Create `tests/ServiceBusPoc.Tests/` xUnit test project
- [ ] Create `infra/` folder structure for Bicep modules
- [x] Create `scripts/` folder for local and Azure deployment scripts
- [x] Create `docs/decisions/` folder for ADRs

**Deliverable:** Folder structure matching key files table in brief; .csproj files ready for dotnet build — ✅ done (8 projects build clean).

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

**Deliverable:** JSON Schema files with validation tests; C# DTOs generated or hand-coded from schemas

---

## Phase 2: MVP Core Implementation (Local Emulator)

**Goal:** Implement end-to-end producer → Service Bus → consumer routing with local Azure Service Bus emulator (Docker Compose).

**Status:** ⏳ Not started — Carwash's HTTP verification API (2.4/2.5 scope, see below) is built and tested, but the actual Service Bus messaging (2.1–2.3) is not. Producer, DigitalChannels, Insurance, ParksResorts, and Carwash consumer services are all stubs that log settings and return immediately (see `TODO: Implement ... in Phase 2` in `ProducerService.cs` and `CarwashConsumerService.cs`).

### 2.1 Azure Service Bus Emulator Topology (Docker Compose)
**File:** `infra/servicebus/compose.yaml` and `config.json`

**Status:** ⏳ Not started — no `infra/` folder exists yet.

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

**Status:** ⏳ Stub only — logs configured namespace/topic and returns; no publish logic yet.

**Responsibilities:**
- Parse event from JSON or CLI args
- Serialize to JSON with envelope
- Send to Service Bus topic
- Log result (success/error)

**Features:**
- Dependency injection via `IServiceProvider`
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

**Status:** ⏳ Console app scaffolds exist for all four; each has a stub service that logs its configured filter/settings but does not yet subscribe or process messages.

**Shared Consumer Base:**
- Listen to subscription
- Deserialize event + validate schema
- Apply business logic
- Complete message

**Per-Consumer Logic:**
- **DigitalChannels:** Log all received events (validation harness)
- **Insurance:** Log events with `hasInsurance = true`
- **ParksResorts:** Log events with `hasParksResorts = true`
- **Carwash:** Parse contact data and prepare for Pulse API call (see 2.4)

**Deliverable:** Runnable consumers for all 4 subscriptions; async message handling

### 2.4 Carwash-to-Pulse Contact API Integration
**File:** `src/ServiceBusPoc.Carwash/Api/CarwashApiServer.cs`

**Status:** ✅ Done, but scoped differently than originally planned — instead of a `CarwashContactMatcher` that calls out to Pulse, Carwash exposes its own `POST /carwash/v1/verify` HTTP endpoint for Pulse to call (see `src/ServiceBusPoc.Carwash/API.md`). Request/response contracts, validation, and error handling are implemented and covered by tests in `tests/ServiceBusPoc.Tests/`. Business logic is still mocked (RAC IDs starting with `VALID` return true). The Service Bus side of the Carwash consumer (receiving `hasCarwashProduct=true` events and invoking this logic) is not yet wired up — see 2.3.

**Responsibilities:**
- Receive contact event from Carwash subscription
- Match contact properties with Pulse Contact CRUD API schema
- Transform event data to Pulse API request shape
- (MVP: Mock Pulse API calls; production will call real endpoint)

**Contract Match Logic:**
- Map `contactId` → Pulse contact identifier
- Map `firstName`, `lastName`, `email`, `phone` to Pulse contact properties
- Track matched contacts in-memory (MVP) or persist to SQL (future)

**Pulse API Schema (Mocked for MVP):**
```csharp
public class PulseContactRequest
{
    public string ContactId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
}
```

**Deliverable:** CarwashContactMatcher that transforms events; mock Pulse HTTP client; logs successful matches

### 2.5 Scenario Verifier & Test Harness
**File:** `src/ServiceBusPoc.Verifier/Services/VerifierService.cs`

**Status:** ⏳ Scaffold exists; scenario logic not yet implemented.

**Purpose:** Run deterministic end-to-end scenarios locally without real cloud infrastructure.

**Scenarios:**
1. **Filter Routing Validation**
   - Emit contact with `hasInsurance=true, hasParksResorts=false, hasCarwashProduct=false`
   - Assert Insurance subscription receives it, others do not
   - Repeat for all attribute combinations (8 test cases)

2. **Carwash Integration Validation**
   - Emit contact with `hasCarwashProduct=true`
   - Assert Carwash consumer receives event
   - Assert ContactMatcher produces Pulse API request shape
   - Verify no errors in matching logic

3. **Schema Validation**
   - Emit malformed event (missing required field)
   - Assert consumer rejects gracefully with schema error

**Deliverable:** Harness that runs scenarios sequentially, logs results, exits with pass/fail status

---

## Phase 3: Unit & Integration Tests (MVP Validation)

**Goal:** Prove filter routing and integration logic with automated tests.

**Status:** ⏳ Partial — 39 tests exist and pass, but they cover the Carwash HTTP API only (constructor, JSON parsing, validation, start/stop, request/response contracts). No schema, filter-routing, Carwash-Service-Bus-integration, or settings tests exist yet under the names below.

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

### 3.3 Carwash Integration Tests
**File:** `tests/ServiceBusPoc.Tests/CarwashIntegrationTests.cs`

**Status:** ✅ Partially covered — `CarwashApiServer*Tests.cs`, `VerifyMemberRequestTests.cs`, `VerifyMemberResponseTests.cs`, and `ErrorResponseTests.cs` cover the HTTP API contract and validation. Pulse HTTP client mocking and Service-Bus-triggered integration are not started.

- Mock Pulse API HTTP client
- Test event-to-Pulse-request transformation
- Test error handling (timeout, 4xx, 5xx responses)
- Test contact matching edge cases (null fields, special characters)

### 3.4 Settings & Configuration Tests
**File:** `tests/ServiceBusPoc.Tests/SettingsTests.cs`

**Status:** ⏳ Not started.

- Validate ServiceBusSettings loads from config
- Test missing/invalid connection string handling
- Test environment variable override

**Target Coverage:** ≥80% meaningful code coverage (focus on business logic and routing)

---

## Phase 4: Infrastructure as Code (Bicep & Cloud Deployment)

**Goal:** Define production-grade Azure Service Bus infrastructure; deployable via `az bicep build` and `az deployment group create`.

**Status:** ⏳ Not started — no `infra/` folder or `.bicep` files exist yet. ADR-008 approves Bicep as the IaC choice; implementation is still pending.

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

**Status:** ✅ Done — ADR-001 through ADR-008 are approved and recorded under `docs/decisions/`.

Document the decision to validate with local emulator before cloud deployment, including:
- Rationale: faster iteration, no cost, deterministic testing
- Trade-offs: emulator limitations (single instance, TCP only)
- Validation path: emulator → cloud optional

**Deliverable:** Full Bicep scaffold; can validate with `az bicep build`

---

## Phase 5: Deployment & Runtime Scripts

**Goal:** Automate local emulator and Azure cloud deployment.

**Status:** ⏳ Partial — `scripts/debug-run.ps1` and `scripts/test-carwash-api.ps1` exist and are used for local Carwash API testing. `run-local-poc.ps1` exists but depends on Phase 2/4 work (emulator topology, producer/consumer messaging) that isn't implemented yet, so it cannot complete end-to-end. `deploy-azure.ps1` is not started.

### 5.1 Local POC Script
**File:** `scripts/run-local-poc.ps1`

**Status:** ⏳ Script scaffold exists (see `scripts/README.md`); blocked on Phase 2 messaging and Phase 4 emulator topology.

**Steps:**
1. Spin up Docker Compose (Service Bus emulator)
2. Wait for emulator readiness
3. Apply config.json topology
4. Start all 4 consumers in background tasks
5. Run producer with test scenarios
6. Collect consumer logs
7. Generate report (routing verified, Carwash integration working)
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
- Carwash integration flow

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

**Status:** ⏳ In progress — Phase 1 is complete; Phase 2 messaging, most of Phase 3 tests, and Phase 5.1's end-to-end script are outstanding. The Carwash HTTP API (originally scoped under 2.4) is complete and tested ahead of the rest of the MVP.

The MVP aims to demonstrate:
1. ⏳ End-to-end event flow: producer → topic → subscriptions → consumers
2. ⏳ Filter routing validation (all 8 scenarios)
3. ⏳ Carwash integration with Pulse API shape (mocked) — HTTP API side done; Service Bus side not started
4. ⏳ Comprehensive unit & integration tests — Carwash API tests done; schema/routing/settings tests outstanding
5. ⏳ Reproducible local execution via Docker + script
6. ⏳ Schema validation and error handling — contracts and DTOs exist; end-to-end validation not yet wired up

**MVP Does NOT Include:**
- Cloud deployment to real Azure subscription
- Production Bicep (deferred to Phase 4)
- Real Pulse API calls (mocked in MVP)
- Persistence, sagas, load testing, custom domains

**MVP Validation Criteria:**
- ✅ `dotnet build` succeeds
- ✅ `dotnet test` passes all tests (39/39; coverage of the completed Carwash API surface, not yet ≥80% overall)
- ⏳ `scripts/run-local-poc.ps1` completes with all 8 routing scenarios passing
- ⏳ Carwash consumer produces valid Pulse API request shape (HTTP verification endpoint done; Pulse-facing client not started)
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
├── Carwash matcher
└── Scenario verifier

Phase 3: Tests (parallel with Phase 2)
├── Schema tests
├── Filter routing tests
├── Carwash integration tests
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
2. **Mock Pulse API in MVP:** Real API calls deferred to post-MVP integration phase
3. **In-Memory Carwash Matcher:** No database in MVP; track matched contacts in memory; log results
4. **Dependency Injection:** Use `IServiceProvider` + `Microsoft.Extensions.DependencyInjection` for all consumers and producers
5. **Structured Logging:** Use `ILogger` with JSON-structured output for machine-parseable logs
6. **Schema Validation:** Use JSON Schema for event contracts; C# DTO validation via FluentValidation or data annotations
7. **Configuration:** No hardcoded secrets; all via `IConfiguration` (appsettings.json, environment variables, Key Vault)

---

## Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Emulator incompatibility | MVP can't validate cloud behavior | Test subscription filters with explicit edge cases; compare with cloud docs |
| Event schema churn | Consumers break on schema changes | Version schemas explicitly; use envelope with dataVersion field |
| Carwash integration latency | Real Pulse API calls may timeout | MVP mocks API; post-MVP adds circuit breaker + retry logic |
| Filter logic errors | Wrong subscriptions receive events | Write exhaustive filter tests (2³ scenarios); manual verification via logs |
| Secret leakage | Credentials committed to repo | Pre-commit hook + review rule: reject any connection strings in code |

---

## Success Metrics (MVP Complete)

- [ ] All 8 filter routing scenarios pass (emulator + tests)
- [ ] Carwash integration produces valid Pulse API request shape
- [ ] No schema validation errors on valid events
- [ ] Graceful error handling on invalid events (schema, network, malformed)
- [ ] Single command (`scripts/run-local-poc.ps1`) validates entire system
- [ ] Test coverage ≥80% for business logic
- [ ] Zero secrets in committed code
- [ ] README enables new dev onboarding in <30 min
- [ ] All code builds and tests pass in CI

---

## Post-MVP Roadmap (Phase 4+)

1. **Cloud Deployment** (Phase 4): Bicep for real Azure; deploy to actual subscription with credentials
2. **Real Pulse API Integration** (Phase 4.1): Replace mock client; test against real Pulse endpoints
3. **Persistence** (Future): Store matched contacts in SQL; expose query API
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
| **QA (Ivy)** | Independent verification of filter routing; Carwash integration E2E tests; sign-off on MVP validation criteria |

---

## Timeline Estimate (Dev Time)

| Phase | Effort | Duration |
|-------|--------|----------|
| Phase 1 | 1–2 days | Structure, contracts, scaffolds |
| Phase 2 | 3–5 days | Producer, 4 consumers, integration |
| Phase 3 | 2–3 days | Tests (parallel with Phase 2) |
| Phase 5.1 | 1 day | Local scripts |
| **MVP Subtotal** | **7–11 days** | |
| Phase 4 | 2–3 days | Bicep, cloud deploy |
| Phase 6 | 1–2 days | Documentation (parallel) |
| **Full Project** | **10–16 days** | |

---

## Next Steps

1. **Approve this plan** with Producer (Remy)
2. **Create ADRs** for key technical decisions (Phase 1)
3. **Launch dev team** (Nova+Sage+Milo) on Phase 1 & 2 in parallel
4. **Define QA scenarios** (optional Ivy) for filter routing validation
5. **Begin implementation** following the sequence above

