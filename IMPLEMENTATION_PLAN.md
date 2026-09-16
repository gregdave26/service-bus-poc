# Implementation Plan & MVP - Enterprise Contact Events POC

## Executive Summary

This document proposes a phased implementation approach for the Azure Service Bus contact events POC. The plan prioritizes validating the core messaging backbone, filter routing, and Carwash integration in the fastest, leanest way possible. The MVP delivers end-to-end event flow with local emulator testing before moving to cloud deployment.

---

## Phase 1: Foundation & Setup (MVP Prep)

**Goal:** Establish project structure, event contracts, and scaffold .NET application.

### 1.1 Project Structure & Tooling
- [x] Repository initialized with .github/ directories for agents and instructions
- [ ] Create `contracts/` folder with JSON Schema definitions for canonical event types
- [ ] Create `src/ServiceBus.Poc/` .NET console project scaffold
- [ ] Create `tests/ServiceBus.Poc.Tests/` xUnit test project
- [ ] Create `infra/` folder structure for Bicep modules
- [ ] Create `scripts/` folder for local and Azure deployment scripts
- [ ] Create `docs/decisions/` folder for ADRs

**Deliverable:** Folder structure matching key files table in brief; .csproj files ready for dotnet build

### 1.2 Event Contracts (JSON Schema)
Define canonical event schemas in `contracts/` as JSON Schema:

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

### 2.1 Azure Service Bus Emulator Topology (Docker Compose)
**File:** `infra/servicebus/compose.yaml` and `config.json`

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
**File:** `src/ServiceBus.Poc/Producer.cs`

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
- `src/ServiceBus.Poc/Consumers/DigitalChannelsConsumer.cs`
- `src/ServiceBus.Poc/Consumers/InsuranceConsumer.cs`
- `src/ServiceBus.Poc/Consumers/ParksResortsConsumer.cs`
- `src/ServiceBus.Poc/Consumers/CarwashConsumer.cs`

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
**File:** `src/ServiceBus.Poc/Integration/CarwashContactMatcher.cs`

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
**File:** `src/ServiceBus.Poc/ScenarioVerifier.cs`

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

### 3.1 Schema Tests
**File:** `tests/ServiceBus.Poc.Tests/SchemaTests.cs`

- Validate event DTOs serialize/deserialize correctly
- Validate envelope structure
- Test schema validation against invalid data
- Confirm required fields enforced

### 3.2 Filter Routing Tests
**File:** `tests/ServiceBus.Poc.Tests/FilterRoutingTests.cs`

- Mock Service Bus client
- Send event to topic
- Verify correct subscriptions receive message (mock filter evaluation)
- Test all 8 attribute combinations (2³ boolean attributes)
- Assert no cross-subscription leakage

### 3.3 Carwash Integration Tests
**File:** `tests/ServiceBus.Poc.Tests/CarwashIntegrationTests.cs`

- Mock Pulse API HTTP client
- Test event-to-Pulse-request transformation
- Test error handling (timeout, 4xx, 5xx responses)
- Test contact matching edge cases (null fields, special characters)

### 3.4 Settings & Configuration Tests
**File:** `tests/ServiceBus.Poc.Tests/SettingsTests.cs`

- Validate ServiceBusSettings loads from config
- Test missing/invalid connection string handling
- Test environment variable override

**Target Coverage:** ≥80% meaningful code coverage (focus on business logic and routing)

---

## Phase 4: Infrastructure as Code (Bicep & Cloud Deployment)

**Goal:** Define production-grade Azure Service Bus infrastructure; deployable via `az bicep build` and `az deployment group create`.

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
**File:** `docs/decisions/001-emulator-first.md`

Document the decision to validate with local emulator before cloud deployment, including:
- Rationale: faster iteration, no cost, deterministic testing
- Trade-offs: emulator limitations (single instance, TCP only)
- Validation path: emulator → cloud optional

**Deliverable:** Full Bicep scaffold; can validate with `az bicep build`

---

## Phase 5: Deployment & Runtime Scripts

**Goal:** Automate local emulator and Azure cloud deployment.

### 5.1 Local POC Script
**File:** `scripts/run-local-poc.ps1`

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

### 6.1 Architecture Documentation
**File:** `docs/architecture/project-goal.md`

- System diagram (producers → topic → subscriptions → consumers)
- Event flow walkthrough
- Filter routing rules
- Carwash integration flow

### 6.2 Developer Guide
**File:** `docs/DEVELOPER.md`

- Prerequisites (.NET 10, Docker, Azure CLI, PowerShell 7+)
- Quick start (clone, `scripts/run-local-poc.ps1`)
- Event contract reference
- Adding a new consumer (template + checklist)
- Adding a new event type
- Troubleshooting

### 6.3 Operational Runbook
**File:** `docs/RUNBOOK.md`

- How to run locally
- How to deploy to Azure
- Common errors and fixes
- Log inspection
- Manual testing procedures

### 6.4 API Reference
**File:** `docs/api.md`

- Producer CLI reference
- Consumer configuration
- Pulse API integration (schema, error codes)
- Event schema reference

---

## MVP Scope Definition

**MVP = Phases 1–3 + Phase 5.1 (Local Scripts)**

The MVP demonstrates:
1. ✅ End-to-end event flow: producer → topic → subscriptions → consumers
2. ✅ Filter routing validation (all 8 scenarios)
3. ✅ Carwash integration with Pulse API shape (mocked)
4. ✅ Comprehensive unit & integration tests
5. ✅ Reproducible local execution via Docker + script
6. ✅ Schema validation and error handling

**MVP Does NOT Include:**
- Cloud deployment to real Azure subscription
- Production Bicep (deferred to Phase 4)
- Real Pulse API calls (mocked in MVP)
- Persistence, sagas, load testing, custom domains

**MVP Validation Criteria:**
- ✅ `dotnet build` succeeds
- ✅ `dotnet test` passes all tests (≥80% coverage)
- ✅ `scripts/run-local-poc.ps1` completes with all 8 routing scenarios passing
- ✅ Carwash consumer produces valid Pulse API request shape
- ✅ No secrets committed; all sensitive config externalized
- ✅ README and DEVELOPER.md enable onboarding

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

