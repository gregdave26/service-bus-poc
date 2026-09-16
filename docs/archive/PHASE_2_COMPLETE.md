# Phase 2 Planning Complete: Hybrid TDD + RAC Engineering Standards

**Date:** 2026-09-15  
**Status:** ✅ **Ready for Dev Implementation**  
**Timeline:** 14–16 hours (dev time)  
**Coverage Target:** ≥80% on all business logic  

---

## What's New: Architecture Standards Integration

Phase 2 plan now includes **comprehensive RAC Engineering Standards** applied throughout development:

### 📚 New Documentation (1,416 lines total)

1. **[PHASE_2_PLAN.md](./PHASE_2_PLAN.md)** (406 lines)
   - Complete MVP specification with acceptance criteria
   - **NEW:** "Code Quality & Architecture Standards" section (lines 60–175)
   - **NEW:** SOLID principles table with violation examples
   - **NEW:** GRASP principles application guide
   - **NEW:** Clean Code practices with code examples (naming, methods, error handling, immutability)
   - **NEW:** Anti-patterns checklist (hard-coded values, sync-over-async, reflection, etc.)
   - **NEW:** Code review checklist for all PRs
   - **UPDATED:** Acceptance criteria now includes Quality & Architecture criteria (items 7–11)

2. **[PHASE_2_HYBRID_TDD.md](./PHASE_2_HYBRID_TDD.md)** (321 lines)
   - TDD workflow with detailed examples
   - **NEW:** "Workflow for Each Component" section (lines 79–240)
   - **NEW:** Step-by-step producer implementation with architecture annotations
   - **NEW:** Concrete test examples following SOLID principles
   - **NEW:** Step 3 refactoring showing Open/Closed principle in action
   - **NEW:** "Architecture Principles Applied Throughout Phase 2" section
   - **NEW:** Clean Architecture diagram (domain vs infrastructure)
   - **NEW:** SOLID principles in Phase 2 with application table
   - **NEW:** Clean Code checklist for developers

3. **[PHASE_2_APPROVED.md](./PHASE_2_APPROVED.md)** (165 lines)
   - High-level summary for team review
   - **NEW:** "Code Quality Assurance (Pre-PR Review Checklist)" section (lines 60–124)
   - Comprehensive checklist covering: Architecture, SOLID, Clean Code, Error Handling, Async/Await, Testing, Compiler, Anti-Patterns

4. **[docs/phase-2-dev-guidelines.md](./docs/phase-2-dev-guidelines.md)** (524 lines) **NEW**
   - Developer-focused implementation guide
   - **Core Principles section:** Clean Architecture, SOLID, Clean Code (with examples)
   - **TDD Workflow section:** Step-by-step with code examples for each step
   - **Dependency Injection Pattern:** Required pattern with Program.cs example
   - **Error Handling Guide:** Do's and don'ts with code samples
   - **Async/Await Best Practices:** Blocking call anti-patterns
   - **Structured Logging:** Correlation ID guidance
   - **Anti-Patterns Avoider Table:** 8 anti-patterns with rationale and fixes
   - **Code Review Checklist:** 30+ items grouped by concern
   - **Complete Consumer Service Example:** Full working code + unit test

---

## Architecture Standards Coverage

### ✅ Clean Architecture

All business logic must be independent of infrastructure:

```
Domain Logic               Infrastructure (via interfaces)
├─ EventEnvelope          ├─ IServiceBusSender
├─ ContactUpdatedEvent    ├─ IServiceBusReceiver
├─ Validation             ├─ ICorrelationIdGenerator
├─ Retry strategy         ├─ ILogger<T>
└─ Event deserialization  └─ IHttpClient
```

### ✅ SOLID Principles (5 Total)

| Principle | How It's Applied | Violation to Avoid |
|-----------|---|---|
| **S**ingle Responsibility | ProducerService publishes; ConsumerService receives | Mixing business logic with Service Bus SDK calls |
| **O**pen/Closed | Services use interfaces; extensible without modification | Hard-coded dependencies; can't swap implementations |
| **L**iskov Substitution | All consumers implement IConsumerService identically | Consumers with different behavior/signatures |
| **I**nterface Segregation | Tests mock only required interfaces (IServiceBusSender) | Mocking huge interfaces with unused methods |
| **D**ependency Inversion | Services depend on abstractions (interfaces) | Direct instantiation: `new ServiceBusClient()` in service code |

### ✅ GRASP Principles (6 Total)

- **Creator:** ProducerService creates EventEnvelope (knows domain rules)
- **Information Expert:** ProducerService is expert in event publishing
- **Low Coupling:** Services depend on interfaces; can swap implementations
- **High Cohesion:** Each service focused on one concern
- **Polymorphism:** All consumers implement same interface; business logic varies by subscription
- **Pure Fabrication:** Helper classes (if needed) created to reduce coupling

### ✅ Clean Code Practices

**1. Naming (self-documenting):**
```csharp
// ✅ GOOD
public async Task PublishWithRetryAsync(EventEnvelope envelope)
private EventEnvelope CreateEnvelope(ContactData contact)

// ❌ BAD
public async Task Pub(object x)
private object Proc(object y)
```

**2. Methods (small, focused):**
```csharp
// ✅ GOOD: Separate concerns
private EventEnvelope CreateEnvelope(ContactData contact) { /* ... */ }
private async Task PublishWithRetryAsync(EventEnvelope envelope) { /* ... */ }

// ❌ BAD: Multiple concerns in one method
private async Task PublishAsync(ContactData contact)
{
    var envelope = new EventEnvelope { /* ... */ };
    var message = new ServiceBusMessage(JsonSerializer.Serialize(envelope));
    await _sender.SendMessageAsync(message);  // 3 concerns: create + serialize + send
}
```

**3. Immutability (prevent bugs):**
```csharp
// ✅ GOOD: Immutable record
public sealed record EventEnvelope(
    string Id, string Type, DateTime Timestamp, 
    string CorrelationId, string Source, string DataVersion, object Data);

// ❌ BAD: Mutable properties
public class EventEnvelope { public string Id { get; set; } }  // Can be changed!
```

**4. Error Handling (explicit, logged):**
```csharp
// ✅ GOOD: Logged, caller decides
catch (ServiceBusCommunicationException ex)
{
    _logger.LogWarning(ex, "Communication error; retrying");
    throw;
}

// ❌ BAD: Silent failure
catch { }  // No logging, no throw
```

### ✅ Anti-Patterns to AVOID (7 Total)

| Anti-Pattern | Why Bad | Fix |
|---|---|---|
| Hard-coded values | Tests fail; config changes require recompile | Use IConfiguration, environment variables, DI |
| Sync-over-async (`.Result`, `.Wait()`) | Deadlock risk; blocks threads | Use `await` throughout |
| Reflection | Slow; breaks at runtime; hard to maintain | Use concrete types + DI |
| Service Locator | Hidden dependencies; hard to test | Constructor injection only |
| God Object | Too many responsibilities | Split into focused services |
| Catch-swallow | Silent failures; hard to debug | Log exceptions; re-throw |
| Premature optimization | Over-engineering; hard to maintain | Optimize only after profiling |

---

## TDD Workflow (with Architecture Guidance)

Every component follows this workflow:

### Step 1: Write Test First (Domain Logic Only)

```csharp
[Fact]
public async Task PublishContactEventAsync_CreatesEnvelopeWithCorrelationId()
{
    // Arrange: Mock infrastructure (Dependency Inversion)
    var mockSender = new Mock<IServiceBusSender>();
    var producer = new ProducerService(mockSender.Object);
    
    // Act: Call domain logic
    await producer.PublishContactEventAsync(contact);
    
    // Assert: Verify business logic
    mockSender.Verify(s => s.SendMessageAsync(
        It.Is<ServiceBusMessage>(m => m.CorrelationId != null)), 
        Times.Once);
}
```

### Step 2: Implement Minimal Code (Pass Tests)

```csharp
public class ProducerService
{
    private readonly IServiceBusSender _sender;  // Dependency Injection
    
    public ProducerService(IServiceBusSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }
    
    public async Task PublishContactEventAsync(ContactData contact)
    {
        var envelope = CreateEnvelope(contact);
        await _sender.SendMessageAsync(ConvertToServiceBusMessage(envelope));
    }
    
    private EventEnvelope CreateEnvelope(ContactData contact)
    {
        return new EventEnvelope(
            Id: Guid.NewGuid().ToString(),
            Type: "contact.updated",
            Timestamp: DateTime.UtcNow,
            CorrelationId: _correlationIdGenerator.GenerateId(),
            Source: "producer",
            DataVersion: "1.0",
            Data: contact);
    }
}
```

✅ **SOLID:** Single Responsibility (publishes only), Dependency Inversion (uses interface)  
✅ **Clean Code:** Self-documenting names; immutable domain object

### Step 3: Refactor (Keep Tests Passing)

Add retry logic, error handling, structured logging:

```csharp
public async Task PublishContactEventAsync(ContactData contact)
{
    var envelope = CreateEnvelope(contact);
    
    try
    {
        await PublishWithRetryAsync(envelope);
    }
    catch (ServiceBusCommunicationException ex)
    {
        _logger.LogError(ex, "Failed to publish {EventId}", envelope.Id);
        throw;
    }
}

private async Task PublishWithRetryAsync(EventEnvelope envelope, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await _sender.SendMessageAsync(ConvertToServiceBusMessage(envelope));
            _logger.LogInformation("Event {EventId} published", envelope.Id);
            return;
        }
        catch (ServiceBusCommunicationException ex) when (attempt < maxRetries)
        {
            var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt - 1) * 100);
            _logger.LogWarning(ex, "Retry {Attempt}/{MaxRetries}", attempt, maxRetries);
            await Task.Delay(delay);
        }
    }
}
```

✅ **Open/Closed:** Retry strategy is now configurable  
✅ **Clean Code:** Structured logging with context  
✅ **Error Handling:** Explicit, logged, caller decides

### Step 4: Verify with Integration Test

Spot-check against running emulator:
```powershell
dotnet run --project src/ServiceBusPoc.Producer -- --contact-id C001 --first-name John
```

---

## Phase 2 Work Items (Updated)

All 9 tasks now include architecture standards compliance:

| Task | TDD | Implementation | Architecture | Coverage |
|------|-----|---|---|---|
| Docker Compose | N/A | 1–2 hrs | Topology design | (N/A) |
| **Producer** | 1 hr | 1.5 hrs | ✅ Clean Arch, SOLID, TDD | ≥80% |
| **Consumers (4×)** | 1.75 hrs | 2.5 hrs | ✅ Clean Arch, SOLID, TDD | ≥80% each |
| **Carwash Integration** | 1 hr | 1.5 hrs | ✅ Clean Arch, SOLID, TDD | ≥80% |
| **Integration Tests** | N/A | 3 hrs | ✅ Emulator validation | (coverage via unit tests) |
| **Arch Verification** | N/A | As-needed | ✅ PR review checklist | All components |
| **Coverage Validation** | N/A | 0.5 hrs | ✅ 80%+ measure | All business logic |
| **Documentation** | N/A | 1 hr | ✅ Setup/topology guides | (N/A) |

**Total:** 14–16 hours

---

## Acceptance Criteria (Functional + Quality)

### Functional Criteria
1. ✅ Docker Compose topology with 4 subscriptions/filters
2. ✅ Producer sends correctly-structured envelopes
3. ✅ 4 consumers receive filtered events correctly
4. ✅ Carwash consumer calls member verification API
5. ✅ End-to-end scenarios pass (5+ filter routing tests)
6. ✅ Documentation complete

### Quality & Architecture Criteria
7. ✅ **Clean Architecture:** Domain logic independent of infrastructure
8. ✅ **SOLID Principles:** All 5 principles applied (S, O, L, I, D)
9. ✅ **Clean Code:** Self-documenting names, small focused methods, immutable objects
10. ✅ **TDD:** All business logic tests written first; ≥80% coverage
11. ✅ **Error Handling:** Explicit, logged, no catch-swallow
12. ✅ **Async/Await:** All I/O is async; no blocking calls
13. ✅ **Logging:** Structured with correlation IDs
14. ✅ **No Anti-Patterns:** No hard-coded values, sync-over-async, reflection, etc.
15. ✅ **Zero Warnings:** `dotnet build` and `dotnet test` pass cleanly
16. ✅ **Architecture Verification:** All PRs pass code review checklist

---

## Developer Resources

### Before Implementation Starts

**Required Reading (in order):**

1. [PHASE_2_PLAN.md](./PHASE_2_PLAN.md) — Full specification (406 lines)
   - Sections: Goal, Context, Testing Strategy, In Scope, Acceptance Criteria

2. [PHASE_2_HYBRID_TDD.md](./PHASE_2_HYBRID_TDD.md) — TDD approach (321 lines)
   - Sections: Decision, Approach by Layer, Workflow Examples, Architecture Principles

3. [docs/phase-2-dev-guidelines.md](../phase-2-dev-guidelines.md) — Developer guide (524 lines)
   - Sections: Core Principles, TDD Workflow, DI Pattern, Error Handling, Logging, Anti-Patterns, Code Review Checklist

**Quick Reference:**
- [PHASE_2_APPROVED.md](./PHASE_2_APPROVED.md) — 1-page summary with checklists

### During Implementation

**Code Review Checklist:**
Use `docs/phase-2-dev-guidelines.md` sections 10–14 for every PR:
- ✅ Architecture & Design (3 items)
- ✅ SOLID Principles (5 items)
- ✅ Clean Code (5 items)
- ✅ Error Handling & Logging (4 items)
- ✅ Async/Await (3 items)
- ✅ Testing (4 items)
- ✅ Compiler & Tooling (3 items)
- ✅ Anti-Patterns (8 items)

**Questions?** Ask Producer (Remy) before starting a component.

---

## What Changed from Earlier Phase 2 Plan

| Aspect | Before | After |
|--------|--------|-------|
| **Testing** | Not specified | Hybrid TDD (tests first for business logic) |
| **Architecture** | Assumed | **Explicitly required:** Clean Architecture, SOLID, GRASP |
| **Clean Code** | Assumed | **Explicitly required:** Self-documenting names, small methods, immutability |
| **Error Handling** | Not specified | **Explicit:** Logged with context; no catch-swallow |
| **Developer Guidance** | None | **524-line developer guide** with concrete examples |
| **Code Review** | Not specified | **30+ item checklist** covering architecture, SOLID, clean code, testing |
| **Anti-Patterns** | Implicit | **Explicit list:** Hard-coded values, sync-over-async, reflection, service locator, god objects, premature optimization, catch-swallow |
| **Timeline** | 11–14 hours | **14–16 hours** (includes TDD + architecture review) |
| **Documentation** | ~600 lines | **1,416 lines** (comprehensive guidance) |

---

## Next Steps

### For Producer (Remy)

1. ✅ Review PHASE_2_APPROVED.md (1-page summary)
2. ✅ Confirm timeline (14–16 hours) and acceptance criteria
3. ✅ Coordinate handoff to Dev team

### For Dev (Nova+Sage+Milo)

1. ✅ Read all 4 documents in order (1,416 lines total)
2. ✅ Study TDD workflow with architecture examples
3. ✅ Begin with Docker Compose topology
4. ✅ Write ProducerServiceTests.cs first (TDD)
5. ✅ Follow architecture standards from phase-2-dev-guidelines.md
6. ✅ Use code review checklist for every PR

### For QA (Ivy, optional)

1. ✅ Prepare end-to-end scenario testing for Phase 2 completion
2. ✅ Spot-check architecture compliance (domain logic separation, dependency injection)

---

## Success Criteria

Phase 2 is **complete and successful** when:

✅ All 9 work items pass (Docker, Producer, 4 Consumers, Carwash, Integration, Architecture, Coverage, Docs)  
✅ 14–16 hours elapsed  
✅ `dotnet build` → 0 errors, 0 warnings  
✅ `dotnet test` → All tests pass  
✅ `dotnet test /p:CollectCoverage=true` → ≥80% coverage on all business logic  
✅ `run-local-poc.ps1` → All 5+ filter routing scenarios pass  
✅ Every PR passes architecture standards code review checklist  
✅ No hard-coded values, no sync-over-async, no catch-swallow errors  
✅ All developers confirm understanding of Clean Architecture, SOLID, Clean Code principles  

---

**Status:** ✅ Phase 2 Planning Complete  
**Ready for:** `/ai-team-orchestration` to coordinate Dev implementation  
**Approach:** Hybrid TDD + RAC Engineering Standards  
**Timeline:** 14–16 hours (dev time)  
**Coverage Target:** ≥80% on all business logic  
**Quality Bar:** Clean Architecture, SOLID Principles, Clean Code, Zero Warnings  

---

**Total Documentation:** 1,416 lines covering:
- 📋 Specification & Acceptance Criteria
- 🧪 TDD Workflow with Examples
- 🏗️ Architecture Standards (Clean Architecture, SOLID, GRASP, Clean Code)
- 📚 Developer Implementation Guide
- ✅ Code Review Checklists
- 🚫 Anti-Patterns to Avoid
- 📝 Logging & Error Handling
- 🔧 Complete Working Examples

**All Phase 2 code must follow these standards to ensure production-ready, maintainable software.**
