# Phase 2: MVP Core Implementation - Plan

**Status:** Ready for Orchestration  
**Date:** 2026-09-15  
**Duration:** 2–3 days (dev time)  
**Blockers:** Phase 1 complete (all projects scaffold, DI setup done, ADRs approved)

---

## Goal

Implement end-to-end producer → Service Bus topic → consumer routing with **local Azure Service Bus emulator** (Docker Compose) to validate the filter-routing topology and Carwash-to-Pulse integration without cloud credentials.

**Observable outcome:**
- Docker Compose topology running emulator with all subscriptions and filters
- Producer sends test contact events with attributes (hasInsurance, hasParksResorts, hasCarwashProduct)
- All 4 consumers receive correctly filtered events
- Carwash consumer verifies members via Carwash API (`/carwash/v1/verify`)
- `run-local-poc.ps1` script orchestrates full end-to-end test

---

## Context

- **Phase 1 complete:** All 7 project scaffolds, Core library, shared contracts, DI setup, ADRs approved
- **Carwash API ready:** 39 unit tests passing; POST `/carwash/v1/verify` implemented with mock validation
- **Event contracts:** JSON Schema defined in PHASE_1_PLAN.md; C# DTOs exist (ContactUpdatedEvent, ContactAttributes, etc.)
- **Local emulator:** Azure Service Bus emulator (Docker) replaces cloud dependencies
- **Technology stack:** .NET 10 (async-first), xUnit, Moq, Docker Compose, structured logging

---

## Testing Strategy: Hybrid TDD

**Philosophy:** Use Test-Driven Development for business logic (pure functions, no external dependencies). Use integration tests for Service Bus interactions (emulator-based).

### Test Layers

| Layer | Approach | Files | Rationale |
|-------|----------|-------|-----------|
| **Event envelope construction** (Producer) | TDD (unit) | `ProducerServiceTests.cs` | Pure functions; easy to mock; clear specs on what envelope should contain |
| **Event deserialization** (all Consumers) | TDD (unit) | `ConsumerServiceTests.cs` (per consumer) | No external deps; validate JSON parsing and attribute extraction |
| **Carwash API integration** (CarwashConsumerService) | TDD (unit) | `CarwashConsumerServiceTests.cs` | Mock HTTP client; test retry logic, error handling, verification call |
| **Service Bus subscribe/receive** | Integration test | Emulator-based verification | Needs running Docker emulator; validates SDK usage |
| **Filter routing scenarios** | Integration test | `run-local-poc.ps1` (5+ scenarios) | End-to-end; verifies subscriptions receive correct events |

### TDD Workflow (per component)

1. **Write test first** — Define expected behavior (envelope structure, envelope attributes, deserialization, API call)
2. **Implement to pass test** — Minimal code to satisfy test
3. **Refactor** — Improve readability, extract helpers, add error handling
4. **Verify with integration test** — Spot-check with running emulator

### Test Coverage Target

- **Business logic layers (Producer, Consumers):** ≥ 80% code coverage
- **Service Bus interactions:** Covered by `run-local-poc.ps1` integration tests
- **Carwash integration:** 80%+ coverage (mocked HTTP calls)

---

## Code Quality & Architecture Standards

All Phase 2 implementation must follow RAC Engineering Standards:

### 🏗️ Architecture Principles

**Clean Architecture / Hexagonal Architecture:**
- Dependency flow: Controllers → Use Cases → Domain → Infrastructure
- Example: `ProducerService` (domain logic) depends on `IServiceBusClient` (abstraction), not concrete SDK
- Domain logic must be independent of infrastructure (Service Bus, HTTP)

**Domain-Driven Design (DDD):**
- `EventEnvelope`, `ContactUpdatedEvent` are domain objects; keep pure
- Use Value Objects for immutable data (CorrelationId, ContactId)
- Services coordinate domain objects and infrastructure

**Separation of Concerns:**
- `ProducerService` handles event envelope construction (business logic)
- `Program.cs` handles Service Bus connection (infrastructure setup)
- Tests mock infrastructure, validate only domain logic

### ✅ SOLID Principles (enforced in code review)

| Principle | Application | Violation to Avoid |
|-----------|-------------|-------------------|
| **S**ingle Responsibility | Each service class has one reason to change: ProducerService publishes events, ConsumerService receives | Mixing business logic with HTTP/database code |
| **O**pen/Closed | Services use interfaces (IServiceBusClient, ILogger); extensible without modifying | Hard-coding `ServiceBusClient` directly in services |
| **L**iskov Substitution | All ConsumerService implementations behave identically at interface level | Carwash consumer overrides with different behavior (inherit common base) |
| **I**nterface Segregation | Tests mock only what's needed (e.g., IServiceBusSender, not entire SDK) | Injecting huge interfaces with unused methods |
| **D**ependency Inversion | Services depend on abstractions (interfaces), not concrete classes | `new ServiceBusClient()` inside service code |

### 🎯 GRASP Principles (applied implicitly)

- **Creator:** ProducerService creates EventEnvelope (knows how to construct it)
- **Information Expert:** ProducerService knows domain rules (how to build envelope)
- **Low Coupling:** Services depend on interfaces, not concrete implementations
- **High Cohesion:** ProducerService handles only producer logic; consumers are separate services
- **Polymorphism:** All 4 consumers implement same IConsumerService interface
- **Pure Fabrication:** `EventEnvelopeFactory` (if needed) created solely to reduce coupling

### 📝 Clean Code Practices

**Naming (self-documenting code):**
```csharp
// ✅ GOOD: Names explain intent, no comments needed
private async Task<EventEnvelope> CreateEnvelopeAsync(ContactData contact)
private async Task PublishWithRetryAsync(EventEnvelope envelope)
private void LogEventReceived(string subscriptionName, EventEnvelope envelope)

// ❌ BAD: Vague names require comments
private async Task Proc(object x)  // Creates envelope
private async Task Pub(object y)   // Publishes with retry
```

**Methods (small, focused, testable):**
```csharp
// ✅ GOOD: Separate concerns, easy to test
private EventEnvelope CreateEnvelope(ContactData contact)
{
    return new EventEnvelope
    {
        Id = Guid.NewGuid().ToString(),
        Timestamp = _timeProvider.UtcNow,
        CorrelationId = _correlationIdGenerator.GenerateId(),
        Type = "contact.updated",
        DataVersion = "1.0",
        Data = contact
    };
}

// ❌ BAD: Mixed concerns, hard to test
private async Task PublishAsync(ContactData contact)
{
    var envelope = new EventEnvelope { /* ... */ };
    var message = new ServiceBusMessage(JsonSerializer.Serialize(envelope));
    await _sender.SendMessageAsync(message);
    _logger.LogInformation("Event sent");
}
```

**Error handling (explicit, no silent failures):**
```csharp
// ✅ GOOD: Clear error semantics
try
{
    await _sender.SendMessageAsync(message);
}
catch (ServiceBusCommunicationException ex)
{
    _logger.LogWarning(ex, "Communication error; retrying");
    throw;  // Caller decides retry strategy
}

// ❌ BAD: Swallowing errors
try { await _sender.SendMessageAsync(message); }
catch { }  // Silent failure
```

**Immutability (prevent bugs):**
```csharp
// ✅ GOOD: Immutable domain object
public sealed record EventEnvelope(
    string Id,
    string Type,
    DateTime Timestamp,
    string CorrelationId,
    string Source,
    string DataVersion,
    object Data);

// ❌ BAD: Mutable, error-prone
public class EventEnvelope
{
    public string Id { get; set; }  // Can be modified after construction
    public string Type { get; set; }
}
```

### 🚫 Anti-Patterns to Avoid

| Anti-Pattern | Why Bad | How to Fix |
|--------------|---------|-----------|
| **Hard-coded values** | Makes testing/config changes hard | Use IConfiguration or dependency injection for all settings |
| **Sync-over-async** | Deadlock risk; blocks threads | All I/O must use async/await |
| **Reflection** | Slow, breaks at runtime, hard to maintain | Use concrete types or DI container |
| **Service Locator** | Hidden dependencies, hard to test | Inject dependencies via constructor |
| **God Object** | Too many responsibilities | Split into focused single-responsibility services |
| **Premature optimization** | Over-engineering, hard to maintain | Optimize only after profiling; keep it simple first |

### 🔍 Code Review Checklist (for Dev)

Before opening PR:
- ✅ **Architecture:** Domain logic separated from infrastructure
- ✅ **SOLID:** Each class has one reason to change; interfaces used for abstraction
- ✅ **Clean Code:** Names self-document; no cryptic abbreviations or comments explaining "why"
- ✅ **Testability:** All business logic is unit-testable without external services
- ✅ **Error Handling:** Exceptions caught where meaningful; logged with context
- ✅ **Immutability:** Domain objects use `record` or sealed classes; no accidental mutations
- ✅ **Async/Await:** No blocking calls (`.Result`, `.Wait()`); all I/O is async
- ✅ **Logging:** Structured logging (ILogger) with correlation IDs for tracing
- ✅ **No Warnings:** Build with zero compiler warnings
- ✅ **Coverage:** ≥80% coverage on business logic; integration tests validate SDK usage

---

## In Scope

### 2.1 Docker Compose Emulator Topology
**File:** `infra/servicebus/compose.yaml` (and supporting `config.json`)

**Topology:**
- Service Bus namespace (emulator on port 5671 AMQP)
- `contact.events` topic
- 4 subscriptions with filters:
  - `digital-channels` (no filter — receives all events)
  - `insurance` (SQL filter: `attributes.hasInsurance = true`)
  - `parks-resorts` (SQL filter: `attributes.hasParksResorts = true`)
  - `carwash` (SQL filter: `attributes.hasCarwashProduct = true`)

**Acceptance Criteria:**
- `docker-compose up -d infra/servicebus/compose.yaml` starts emulator
- All subscriptions and filters are created and verified
- Connection string works with Azure SDK: `sb://localhost` (or emulator default)
- Script can list subscriptions/filters via Azure CLI or SDK

**Deliverable:** `compose.yaml` that replicates the Bicep topology locally

### 2.2 Producer Implementation (TDD)
**Files:** 
- `src/ServiceBusPoc.Producer/ProducerService.cs`
- `tests/ServiceBusPoc.Tests/ProducerServiceTests.cs` (TDD — write tests first)
- `src/ServiceBusPoc.Producer/Program.cs`

**Test-First Behavior (write these tests first):**
- `PublishContactEventAsync_CreatesEnvelopeWithMetadata_IncludesCorrelationId` — Verify envelope structure
- `PublishContactEventAsync_SerializesContactDataCorrectly_IncludesAllAttributes` — Verify JSON serialization
- `PublishContactEventAsync_SetsTimestampToUtcNow` — Verify timestamp generation
- `PublishContactEventAsync_WithSendFailure_RetriesUpTo3Times` — Verify retry logic
- `PublishContactEventAsync_AfterMaxRetries_LogsErrorAndThrows` — Verify error handling

**Implementation:**
- Accept contact event data (CLI args, JSON stdin, or environment)
- Create `EventEnvelope` with id, type, timestamp, correlationId, source, dataVersion
- Wrap `ContactUpdatedEvent` in envelope
- Send to Service Bus topic via `ServiceBusClient.CreateSender()`
- Implement exponential backoff retry (max 3 attempts)
- Structured logging for success/failure

**Acceptance Criteria:**
- All TDD tests pass (≥ 80% coverage)
- Producer sends envelope-wrapped events
- Retry logic works with exponential backoff
- Logs include correlation ID for tracing
- `dotnet run --project src/ServiceBusPoc.Producer -- --contact-id C001 --first-name John --has-carwash-product true` publishes event

**Deliverable:** Functional producer with ≥ 80% unit test coverage

### 2.3 Consumer Implementations (TDD)
**Files (per consumer):** 
- `src/ServiceBusPoc.{DigitalChannels,Insurance,ParksResorts,Carwash}/ConsumerService.cs`
- `tests/ServiceBusPoc.Tests/{DigitalChannels,Insurance,ParksResorts,Carwash}ConsumerServiceTests.cs` (TDD — write tests first)
- `src/ServiceBusPoc.{*}/Program.cs`

**Test-First Behavior (write these tests per consumer):**
- `ReceiveContactEventAsync_DeserializesEnvelopeCorrectly_ExtractsContactData` — Verify envelope deserialization
- `ReceiveContactEventAsync_LogsEventWithStructuredFields_IncludesContactIdAndAttributes` — Verify logging
- `ReceiveContactEventAsync_WithMalformedJSON_LogsErrorAndSkips` — Error handling
- `ReceiveContactEventAsync_LogsSubscriptionAndEventType` — Verify subscription awareness

**Implementation:**
- Connect to Service Bus subscription via `ServiceBusClient.CreateReceiver(subscriptionName)`
- Receive messages with max 10s timeout
- Deserialize event envelope and `ContactUpdatedEvent`
- Extract contact ID, name, and attributes
- Log event with structured fields (subscription, contact ID, attributes, event type)
- Mark message as complete (ProcessMessageAsync)
- Graceful shutdown on cancellation

**Carwash-specific additions (after base consumer works):**
- After event deserialization: POST to `http://localhost:5000/carwash/v1/verify`
- Include RacId in request body
- Log verification result (ValidMember true/false)
- Mock HTTP client in tests; real HttpClient in production

**Acceptance Criteria (per consumer):**
- All TDD tests pass (≥ 80% coverage)
- Consumer connects to correct subscription
- Deserialization and logging work correctly
- Carwash consumer calls API with correct RacId
- `dotnet run --project src/ServiceBusPoc.{DigitalChannels,Insurance,ParksResorts,Carwash}` runs without errors

**Deliverable:** 4 functional consumers with ≥ 80% unit test coverage

### 2.4 Carwash Integration Tests (TDD)
**File:** `tests/ServiceBusPoc.Tests/CarwashConsumerServiceTests.cs` (TDD — write tests first)

**Test-First Behavior:**
- `ReceiveCarwashEventAsync_CallsVerifyApi_WithCorrectRacId` — Verify API call structure
- `ReceiveCarwashEventAsync_WithValidMember_LogsVerificationSuccess` — Success case
- `ReceiveCarwashEventAsync_WithInvalidMember_LogsVerificationFailure` — Failure case
- `ReceiveCarwashEventAsync_WithApiTimeout_RetriesAndLogs` — Error handling
- `ReceiveCarwashEventAsync_WithApiDown_LogsErrorButContinues` — Resilience

**Implementation:**
- Mock `HttpClient` in tests; use real `HttpClient` in production
- Extract API call logic to testable method (e.g., `VerifyMemberAsync(racId)`)
- Add retry logic with exponential backoff (max 3 attempts)
- Log all outcomes (success, invalid member, API errors)
- Continue processing regardless of verification result (Phase 2 behavior)

**Acceptance Criteria:**
- All TDD tests pass (≥ 80% coverage for Carwash integration)
- `HttpClient` is mockable via dependency injection
- API calls include correct RacId and headers
- Retry logic works as expected

**Deliverable:** CarwashConsumerService with ≥ 80% coverage on integration logic

### 2.5 Filter Routing Verification (Integration Tests)

**Test Scenarios:**
1. **All attributes false**: Only `digital-channels` receives
2. **hasInsurance=true**: `digital-channels` + `insurance` receive
3. **hasParksResorts=true**: `digital-channels` + `parks-resorts` receive
4. **hasCarwashProduct=true**: `digital-channels` + `carwash` receive
5. **Multiple attributes**: `digital-channels` + intersection of matching consumers receive

**Acceptance Criteria:**
- Script orchestrates producer → send test event → wait for all consumers → verify subscription counts
- Output shows which consumers received each event
- All 5 scenarios pass (correct subscription filtering)
- Script logs test results and exit code (0 = pass, 1 = fail)

**Deliverable:** End-to-end scenario verification in `run-local-poc.ps1`

### 2.5 Carwash-to-Pulse Integration Test
**File:** `run-local-poc.ps1` (Carwash scenario)

**Test:**
1. Start Carwash consumer and API server (already running)
2. Producer sends contact with `hasCarwashProduct=true` and `RacId=VALID-12345` (mock valid member)
3. Carwash consumer receives event, calls `POST /carwash/v1/verify { "RacId": "VALID-12345" }`
4. Carwash API responds `{ "ValidMember": true }`
5. Carwash logs verification success

**Acceptance Criteria:**
- Carwash consumer logs show: event received, API call made, member validated
- Test passes when logs confirm verification flow

**Deliverable:** Carwash integration verification scenario

### 2.6 Documentation & Runbooks
**Files:**
- `docs/PHASE_2_SETUP.md` — Prerequisites, Docker setup, env vars, quick start
- `docs/TOPOLOGY.md` — Diagram and explanation of subscriptions/filters
- `docs/CONSUMER_VERIFICATION.md` — How to run individual consumers and spot-check events

**Acceptance Criteria:**
- New user can follow docs to set up emulator, start all services, and run POC

**Deliverable:** Operational runbooks for Phase 2 artifacts

---

## Out of Scope

- ❌ **Cloud deployment** (Bicep, Azure resources) — Phase 3
- ❌ **Production error handling** (DLQ, retry policies, circuit breaker) — Phase 2+ refinement
- ❌ **Event persistence, sagas, sessions** — Future phases
- ❌ **Authentication/authorization** — Phase 3 (Carwash API will add subscription key validation)
- ❌ **Real member database** — Phase 2+ (mock validation in place)
- ❌ **Carwash member sync logic** — Phase 2+ (verification only in Phase 2)
- ❌ **Performance/load testing** — Post-MVP

---

## Acceptance Criteria (Overall Phase 2)

### Functional Criteria

1. **Emulator topology**: Docker Compose runs and all subscriptions/filters verified
2. **Producer (TDD)**: 
   - Unit tests written first; all pass
   - ≥ 80% code coverage on ProducerService
   - Sends events with correct envelope structure
3. **All 4 Consumers (TDD)**: 
   - Unit tests written first; all pass
   - ≥ 80% code coverage on each ConsumerService
   - Receive and log events with correct subscription filtering
4. **Carwash Integration (TDD)**:
   - Unit tests with mocked HttpClient; all pass
   - ≥ 80% coverage on API integration logic
   - Calls Carwash API and logs verification result
5. **End-to-end Integration Tests**: 
   - `run-local-poc.ps1` runs all 5+ filter routing scenarios
   - All scenarios pass (correct subscription filtering verified)
   - Carwash integration test verifies API call and logging
6. **Documentation**: Phase 2 setup and topology docs complete

### Quality & Architecture Criteria (RAC Engineering Standards)

7. **Clean Architecture**:
   - Domain logic (envelope construction, event deserialization) is independent of infrastructure
   - Services depend on interfaces, not concrete implementations
   - All business logic is unit-testable with mocked dependencies

8. **SOLID Principles**:
   - **Single Responsibility**: Each service class has one reason to change
   - **Open/Closed**: Services use interfaces for extensibility
   - **Liskov Substitution**: All consumer implementations are interchangeable
   - **Interface Segregation**: Tests mock only required dependencies
   - **Dependency Inversion**: Services depend on abstractions, not concretions

9. **Clean Code**:
   - Names are self-documenting; no cryptic abbreviations
   - Methods are small, focused, and testable
   - Immutable domain objects (using `record` or sealed classes)
   - Error handling is explicit and logged with context
   - No anti-patterns (sync-over-async, reflection, god objects, hard-coded values)

10. **No Compiler Warnings**: 
    - `dotnet build` succeeds with zero warnings
    - `dotnet test` passes all unit tests
    - `dotnet test /p:CollectCoverage=true` shows ≥ 80% coverage on business logic

11. **Structured Logging**: 
    - All logs use ILogger pattern with structured fields
    - Correlation IDs included for distributed tracing
    - No sensitive data in logs

---

## Work Breakdown (Hybrid TDD Approach)

| Item | Phase | Time | Dependencies |
|------|-------|------|--------------|
| Docker Compose topology | Impl | 1–2 hours | Phase 1 complete |
| **Producer TDD tests** | **TDD (first)** | **1 hour** | Docker running |
| **Producer implementation** | **Impl** | **1.5 hours** | Tests written |
| **DigitalChannels TDD tests** | **TDD (first)** | **0.75 hours** | Producer done |
| **DigitalChannels implementation** | **Impl** | **1 hour** | Tests written |
| **Insurance TDD tests** | **TDD (first)** | **0.5 hours** | DigitalChannels template |
| **Insurance implementation** | **Impl** | **0.75 hours** | Tests written |
| **ParksResorts TDD tests** | **TDD (first)** | **0.5 hours** | DigitalChannels template |
| **ParksResorts implementation** | **Impl** | **0.75 hours** | Tests written |
| **Carwash consumer TDD tests** | **TDD (first)** | **1 hour** | Carwash API done |
| **Carwash consumer + API integration** | **Impl** | **1.5 hours** | Tests written |
| **Filter routing tests** (5 scenarios, emulator) | **Integration** | **2 hours** | All consumers done |
| **Carwash integration test** (end-to-end) | **Integration** | **1 hour** | Carwash consumer done |
| **Documentation** | **Docs** | **1 hour** | All above done |
| **Coverage validation** (`dotnet test /p:CollectCoverage=true`) | **QA** | **0.5 hours** | All tests written |
| **Total Phase 2** | — | **14–16 hours** | — |

**Timeline increase from hybrid TDD:** +1–2 hours (relative to implementation-first approach), but high confidence gain (≥ 80% coverage on all business logic)

---

## Risk Mitigations

| Risk | Mitigation |
|------|-----------|
| Emulator filters don't work as expected | Test each filter independently before end-to-end test |
| Connection string/AMQP port issues | Verify emulator is running and reachable before producer starts |
| Consumer hangs on receive | Use max timeout (10s); test with producer sending events first |
| Carwash API not available when consumer starts | Carwash API already running as background task in Phase 1; consumer waits/retries |
| Filter logic doesn't match Bicep | Document filter syntax; verify with Azure CLI `az servicebus topic subscription rule` commands |

---

## Handoff & Dependencies

**Blockers:**
- ✅ Phase 1 complete (project scaffolds, Core library, event contracts, Carwash API + 39 tests)

**Depends on:**
- Docker Compose emulator running
- `ServiceBusPoc.Core` with event contracts and DI helpers

**Hands off to Phase 3:**
- Bicep IaC for cloud deployment
- Real member database integration
- Production error handling (DLQ, exponential backoff, circuit breaker)

---

## Verification Commands

```bash
# Build all projects
dotnet build src/ServiceBusPoc.slnx

# Run tests (includes Carwash API unit tests)
dotnet test tests/ServiceBusPoc.Tests/

# Start Docker Compose emulator
docker-compose -f infra/servicebus/compose.yaml up -d

# Run end-to-end POC
.\scripts\run-local-poc.ps1

# Spot-check: run producer manually
dotnet run --project src/ServiceBusPoc.Producer -- \
  --contact-id C001 --first-name John --has-carwash-product true

# Spot-check: run DigitalChannels consumer
dotnet run --project src/ServiceBusPoc.DigitalChannels
```

---

## Sign-Off

- **Producer (Remy):** Scope defined, acceptance criteria clear, dependencies identified ✓
- **Dev (Nova+Sage+Milo):** Ready to implement per plan ⏳
- **QA (Ivy, optional):** Standby for end-to-end scenario verification ⏳
