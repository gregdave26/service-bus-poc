# Phase 2 Ready for Development — Hybrid TDD Approved

**Date:** 2026-09-15  
**Status:** ✅ Ready for Dev implementation

---

## Summary of Changes

Phase 2 plan has been updated to **use hybrid TDD (Test-Driven Development)** for business logic layers while maintaining integration tests for Service Bus interactions.

### What Changed

| Aspect | Before | After |
|--------|--------|-------|
| Producer implementation | Write code first, test later | **Write tests first (TDD), then implement** |
| Consumer implementations | Write code first | **Write tests first (TDD), then implement** |
| Carwash integration | Write code first | **Write tests first (TDD) with mocked HttpClient, then implement** |
| Service Bus interactions | (unchanged) | Integration tests via emulator (`run-local-poc.ps1`) |
| Test coverage target | Implied | **≥80% on all business logic layers** |
| Timeline impact | 11–14 hours | **14–16 hours (+1–2 hours)** |

### Key Benefits

✅ **Clear specifications** — Tests define "what should this component do?"  
✅ **High confidence** — ≥80% coverage on all business logic ensures fewer bugs  
✅ **Refactoring safety** — Comprehensive tests enable confident optimization  
✅ **Reduced integration surprises** — Business logic validated before emulator testing  
✅ **Maintainability** — Tests serve as living documentation

---

## TDD Workflow (All Business Logic Components)

1. **Write test first** — Define expected behavior
   ```csharp
   [Fact]
   public async Task PublishContactEventAsync_CreatesEnvelopeWithCorrelationId()
   {
       // Arrange
       var mockSender = new Mock<ServiceBusSender>();
       var producer = new ProducerService(mockSender.Object);
       
       // Act
       await producer.PublishContactEventAsync(contact);
       
       // Assert
       mockSender.Verify(s => s.SendMessageAsync(
           It.Is<ServiceBusMessage>(m => m.CorrelationId != null)), 
           Times.Once);
   }
   ```

2. **Implement to pass test** — Minimal code satisfying test
   ```csharp
   public async Task PublishContactEventAsync(ContactData contact)
   {
       var envelope = new EventEnvelope
       {
           CorrelationId = _correlationIdGenerator.GenerateId(),
           // ...
       };
       await _sender.SendMessageAsync(Convert(envelope));
   }
   ```

3. **Refactor** — Improve readability, add error handling (tests ensure no regressions)

4. **Verify with integration test** — Spot-check against running emulator

---

## Work Items (Updated with TDD Guidance)

**All Phase 2 tasks have been updated in SQL tracking:**

1. ✅ **phase2-docker-compose** — Emulator topology (no TDD needed)
2. ✅ **phase2-producer** — ProducerServiceTests.cs (TDD) + implementation
3. ✅ **phase2-consumers** — Consumer*Tests.cs (TDD) + 4 consumer implementations
4. ✅ **phase2-carwash-integration** — CarwashConsumerServiceTests.cs (TDD with mocked HttpClient) + integration
5. ✅ **phase2-end-to-end-tests** — run-local-poc.ps1 scenarios (integration tests)
6. ✅ **phase2-coverage-validation** — dotnet test /p:CollectCoverage=true (new task)
7. ✅ **phase2-documentation** — Setup guides, topology docs

**Total:** 8 tasks, 14–16 hours estimated

---

## Documents Updated

- ✅ **PHASE_2_PLAN.md** — Added "Testing Strategy: Hybrid TDD" section with layer definitions
- ✅ **PHASE_2_PLAN.md** — Updated all component sections (2.2–2.5) with "Test-First Behavior" subsections
- ✅ **PHASE_2_PLAN.md** — Adjusted work breakdown to show TDD phases and timeline impact
- ✅ **PHASE_2_PLAN.md** — Enhanced acceptance criteria to include ≥80% coverage requirement
- ✅ **PHASE_2_HYBRID_TDD.md** — New document with detailed TDD approach and examples

---

## Acceptance Criteria (Updated)

**Phase 2 is complete when:**

1. ✅ Docker Compose emulator topology running with all 4 subscriptions/filters verified
2. ✅ **ProducerServiceTests.cs** (TDD) — All tests pass, ≥80% coverage on ProducerService
3. ✅ **ConsumerServiceTests.cs** (TDD) — All tests pass per consumer, ≥80% coverage each
4. ✅ **CarwashConsumerServiceTests.cs** (TDD with mocked HttpClient) — All tests pass, ≥80% integration coverage
5. ✅ **Integration tests** — run-local-poc.ps1 runs all 5+ filter routing scenarios; all pass
6. ✅ **Carwash integration end-to-end** — Test verifies API call and member verification logging
7. ✅ **Coverage validation** — `dotnet test /p:CollectCoverage=true` shows ≥80% on all business logic
8. ✅ **No compiler warnings** — `dotnet build` and `dotnet test` pass cleanly
9. ✅ **Documentation** — Phase 2 setup, topology, and verification runbooks complete

---

## Next Steps

**For Dev (Nova+Sage+Milo):**
1. Read [PHASE_2_PLAN.md](./PHASE_2_PLAN.md) (updated with TDD sections)
2. Read [PHASE_2_HYBRID_TDD.md](./PHASE_2_HYBRID_TDD.md) (TDD approach and examples)
3. Start with Docker Compose topology setup
4. Begin ProducerServiceTests.cs (TDD first)
5. Follow TDD workflow: test → implement → refactor → verify

**For Producer (Remy):**
- Approve hybrid TDD approach and updated timeline (14–16 hours)
- Confirm all acceptance criteria
- Coordinate handoff to Dev

**For QA (Ivy, optional):**
- Prepare end-to-end scenario verification for Phase 2 completion
- Spot-check filter routing and Carwash integration

---

## Timeline Summary

| Phase | Duration | Cumulative |
|-------|----------|-----------|
| Planning (this phase) | ✅ Done | — |
| Docker Compose | 1–2 hours | 1–2 hours |
| Producer (TDD tests + impl) | 2.5 hours | 3.5–4.5 hours |
| Consumers (TDD tests + impl) | 4.25 hours | 7.75–8.75 hours |
| Carwash (TDD + impl) | 2.5 hours | 10.25–11.25 hours |
| Integration tests | 3 hours | 13.25–14.25 hours |
| Documentation + coverage | 1–1.5 hours | **14.25–15.75 hours** |

---

## Code Quality Assurance (Pre-PR Review Checklist)

Before opening a PR, Dev team must verify all Phase 2 code meets RAC Engineering Standards:

### Architecture & Design
- ✅ Domain logic (event construction, deserialization) separated from infrastructure (Service Bus SDK)
- ✅ All services depend on interfaces, not concrete implementations
- ✅ Business logic is testable without external services (mocked in unit tests)
- ✅ Service Bus interactions validated via integration tests with emulator

### SOLID Principles
- ✅ **Single Responsibility:** Each service class has one reason to change
- ✅ **Open/Closed:** Services use interfaces; extensible without modification
- ✅ **Liskov Substitution:** All consumer implementations are interchangeable
- ✅ **Interface Segregation:** Tests mock only required dependencies
- ✅ **Dependency Inversion:** All services depend on abstractions via constructor injection

### Clean Code
- ✅ Method/variable names are self-documenting; no cryptic abbreviations
- ✅ No comments explaining "what" — if needed, code intent is unclear
- ✅ Methods are small and focused; each does one thing
- ✅ Immutable domain objects (use `record` or sealed classes)
- ✅ No hard-coded values (all configuration via IConfiguration or DI)

### Error Handling & Logging
- ✅ Exceptions caught where meaningful; logged with context
- ✅ Structured logging using ILogger with correlation IDs
- ✅ No silent failures (catch-swallow anti-pattern avoided)
- ✅ Retry logic explicit and logged

### Async/Await
- ✅ All I/O is async; no `.Result` or `.Wait()` blocking calls
- ✅ CancellationToken properly threaded through async methods
- ✅ No sync-over-async anti-pattern

### Testing
- ✅ Unit tests written first (TDD approach)
- ✅ All business logic is mockable via interfaces
- ✅ Coverage ≥80% on ProducerService, all Consumers, Carwash integration
- ✅ Integration tests validate Service Bus SDK usage with emulator

### Compiler & Tooling
- ✅ `dotnet build` passes with zero warnings
- ✅ `dotnet test` passes all unit tests
- ✅ `dotnet test /p:CollectCoverage=true` shows ≥80% coverage

### Avoid Anti-Patterns
- ❌ No hard-coded values (use IConfiguration)
- ❌ No reflection (use concrete types + DI)
- ❌ No service locator pattern
- ❌ No god objects (multiple responsibilities)
- ❌ No premature optimization
- ❌ No sync-over-async blocking
- ❌ No catch-swallow error handling

---

**Status:** ✅ **Ready for Development**  
**Approach:** Hybrid TDD (business logic unit tests + Service Bus integration tests)  
**Architecture:** Clean Architecture + SOLID + GRASP principles enforced  
**Code Quality:** RAC Engineering Standards compliance required  
**Timeline:** 14–16 hours (total Phase 2)  
**Coverage Target:** ≥80% on all business logic  
**Next Action:** Dev reviews PHASE_2_PLAN.md and PHASE_2_HYBRID_TDD.md, begins implementation with architecture standards in mind
