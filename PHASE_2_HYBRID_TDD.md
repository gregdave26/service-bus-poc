# Phase 2 Hybrid TDD Approach

**Updated:** 2026-09-15  
**Status:** Ready for Dev Implementation

---

## Decision: Hybrid TDD for Phase 2

Based on analysis and team approval, Phase 2 will use **Test-Driven Development (TDD) for business logic layers** and **integration tests for Service Bus interactions**.

### Rationale

- **TDD benefits:** Clarifies requirements, ensures ≥80% coverage, enables safe refactoring, catches logic bugs early
- **Integration tests needed:** Service Bus topology can only be validated with running emulator; can't be unit-tested
- **Time tradeoff:** +1–2 hours vs. implementation-first (14–16 hours total), but high confidence gain

---

## TDD Approach by Layer

### ✅ Unit Tests (TDD — Write Tests First)

| Component | Tests to Write | Coverage Target |
|-----------|---|---|
| **ProducerService** | Envelope structure, metadata, retry logic, error handling | ≥80% |
| **ConsumerService** (DigitalChannels, Insurance, ParksResorts) | Event deserialization, logging, subscription awareness, error handling | ≥80% per consumer |
| **CarwashConsumerService** | API call structure, mocked HttpClient, retry logic, error scenarios | ≥80% |

**Example Test (ProducerService):**
```csharp
[Fact]
public async Task PublishContactEventAsync_CreatesEnvelopeWithMetadata_IncludesCorrelationId()
{
    // Arrange
    var mockSender = new Mock<ServiceBusSender>();
    var producer = new ProducerService(mockSender.Object);
    var contact = new ContactData { ContactId = "C001", FirstName = "John" };
    
    // Act
    await producer.PublishContactEventAsync(contact);
    
    // Assert: Verify envelope was created with correlation ID
    mockSender.Verify(s => s.SendMessageAsync(
        It.Is<ServiceBusMessage>(m => 
            m.CorrelationId != null && 
            m.ApplicationProperties["type"] == "contact.updated")),
        Times.Once);
}
```

### 🔌 Integration Tests (Implementation → Test)

| Scenario | How | Coverage |
|----------|-----|----------|
| **Service Bus subscribe/receive** | Emulator-based; verify SDK usage | `run-local-poc.ps1` |
| **Filter routing** | 5+ scenarios; verify subscriptions receive correct events | Integration tests |
| **Carwash member verification** | End-to-end; verify API call and logging | Integration test |

**Example Scenario (run-local-poc.ps1):**
```powershell
# Scenario: hasCarwashProduct=true → Only carwash + digital-channels receive
$event = @{
    contactId = "C001"
    firstName = "John"
    attributes = @{ hasCarwashProduct = $true; hasInsurance = $false; hasParksResorts = $false }
}
Publish-Event -Event $event
Wait-ForConsumers
Assert-SubscriptionReceivedEvent -Subscription "digital-channels" -EventId $event.id
Assert-SubscriptionReceivedEvent -Subscription "carwash" -EventId $event.id
Assert-SubscriptionDidNotReceive -Subscription "insurance" -EventId $event.id
```

---

## Workflow for Each Component (with Architecture Principles)

### Example: Producer Implementation with Clean Architecture & SOLID

#### 1. Write ProducerServiceTests.cs (TDD First)

```csharp
// Tests define the contract: what should ProducerService do?
// ✅ Domain logic only; no Service Bus SDK
public class ProducerServiceTests
{
    [Fact]
    public async Task PublishContactEventAsync_CreatesEnvelopeWithCorrelationId()
    {
        // Arrange: Mock infrastructure (Dependency Inversion)
        var mockSender = new Mock<IServiceBusSender>();
        var mockGenerator = new Mock<ICorrelationIdGenerator>();
        mockGenerator.Setup(g => g.GenerateId()).Returns("trace-123");
        var producer = new ProducerService(mockSender.Object, mockGenerator.Object);
        
        // Act
        await producer.PublishContactEventAsync(new ContactData { ContactId = "C001" });
        
        // Assert: Verify domain logic (envelope structure)
        mockSender.Verify(s => s.SendMessageAsync(
            It.Is<ServiceBusMessage>(m => m.CorrelationId == "trace-123")), 
            Times.Once);
    }
    
    [Fact]
    public async Task PublishContactEventAsync_WithSendFailure_RetriesUpTo3Times()
    {
        // Arrange
        var mockSender = new Mock<IServiceBusSender>();
        mockSender
            .SetupSequence(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>()))
            .ThrowsAsync(new ServiceBusCommunicationException("Temporary"))
            .ThrowsAsync(new ServiceBusCommunicationException("Temporary"))
            .Returns(Task.CompletedTask);
        
        var producer = new ProducerService(mockSender.Object, new MockGenerator());
        
        // Act
        await producer.PublishContactEventAsync(new ContactData { ContactId = "C001" });
        
        // Assert
        mockSender.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>()), Times.Exactly(3));
    }
}
```

✅ **SOLID:** Dependency Inversion (IServiceBusSender interface)  
✅ **Single Responsibility:** Tests validate only envelope logic, not Service Bus SDK  
✅ **Clean Code:** Test names self-document expected behavior

#### 2. Implement ProducerService (Minimal to Pass Tests)

```csharp
// ✅ Clean Architecture: Domain logic separated from infrastructure
public class ProducerService
{
    private readonly IServiceBusSender _sender;
    private readonly ICorrelationIdGenerator _correlationIdGenerator;
    private readonly ILogger<ProducerService> _logger;
    
    // ✅ Dependency Injection: All dependencies injected
    public ProducerService(
        IServiceBusSender sender, 
        ICorrelationIdGenerator correlationIdGenerator,
        ILogger<ProducerService> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _correlationIdGenerator = correlationIdGenerator ?? throw new ArgumentNullException(nameof(correlationIdGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    // ✅ Single method responsibility: Publish one event with retry
    public async Task PublishContactEventAsync(ContactData contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        
        var envelope = CreateEnvelope(contact);
        await PublishWithRetryAsync(envelope);
    }
    
    // ✅ Separate concern: Domain logic (envelope creation)
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
    
    // ✅ Separate concern: Retry strategy
    private async Task PublishWithRetryAsync(EventEnvelope envelope, int maxRetries = 3)
    {
        var message = ConvertToServiceBusMessage(envelope);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _sender.SendMessageAsync(message);
                _logger.LogInformation("Event {EventId} published", envelope.Id);
                return;
            }
            catch (ServiceBusCommunicationException ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt - 1) * 100);
                _logger.LogWarning(ex, "Retry {Attempt}/{MaxRetries} for {EventId}", 
                    attempt, maxRetries, envelope.Id);
                await Task.Delay(delay);
            }
        }
    }
}

// ✅ Immutable domain object (record prevents accidental mutations)
public sealed record EventEnvelope(
    string Id,
    string Type,
    DateTime Timestamp,
    string CorrelationId,
    string Source,
    string DataVersion,
    object Data);

// ✅ Interface for abstraction (Dependency Inversion)
public interface IServiceBusSender
{
    Task SendMessageAsync(ServiceBusMessage message);
}

public interface ICorrelationIdGenerator
{
    string GenerateId();
}
```

✅ **Open/Closed:** Retry logic configurable; envelope structure extensible  
✅ **Clean Code:** Self-documenting method names; no comments needed  
✅ **Immutability:** EventEnvelope is a sealed record (prevents bugs)  

#### 3. Refactor & Optimize (All Tests Still Pass)

```csharp
// Optional: Extract retry strategy to separate class if it grows
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    public async Task ExecuteAsync(Func<Task> operation, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (ServiceBusCommunicationException ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt - 1) * 100);
                await Task.Delay(delay);
            }
        }
    }
}

// Now ProducerService can use the injected strategy (Open/Closed principle)
public ProducerService(
    IServiceBusSender sender,
    ICorrelationIdGenerator generator,
    IRetryPolicy retryPolicy,
    ILogger<ProducerService> logger)
{
    // ...
    _retryPolicy = retryPolicy;
}
```

✅ **Low Coupling:** Retry strategy is now pluggable  
✅ **GRASP Information Expert:** Each class is expert in its domain (retry logic, envelope construction)  

#### 4. Verify with Integration Test

```powershell
# run-local-poc.ps1
Publish-Event -ContactId "C001" -FirstName "John" -HasCarwashProduct $true
Wait-ForProducerCompletion -TimeoutSeconds 10
Assert-EventPublished -EventId $publishedEventId -SubscriptionName "digital-channels"
```

✅ **Integration validation:** Producer works end-to-end with Service Bus emulator  

---

## Architecture Principles Applied Throughout Phase 2

### Clean Architecture (Domain Separated from Infrastructure)

```
┌─────────────────────────────────────┐
│      Presentation Layer             │ (Program.cs, debug-run.ps1)
│   (CLI orchestration)               │
└────────────────┬────────────────────┘
                 │
┌────────────────▼────────────────────┐
│     Application Layer               │ (ProducerService, ConsumerService)
│  (Use-cases, coordinate domain+infra)│
└────────────────┬────────────────────┘
                 │
     ┌───────────┴───────────┐
     │                       │
┌────▼──────────────┐   ┌────▼──────────────┐
│   Domain Layer    │   │ Infrastructure    │
│  (Business Logic) │   │   Layer (via      │
│  • EventEnvelope  │   │   interfaces)     │
│  • Validation     │   │  • Service Bus    │
│  • Retry Strategy │   │  • HTTP Client    │
└───────────────────┘   └───────────────────┘
```

### SOLID Principles in Phase 2

| Principle | How It's Applied | Violation to Avoid |
|-----------|---|---|
| **S**ingle | ProducerService publishes only | Mixing SDK calls + domain logic |
| **O**pen/Closed | Services use interfaces; extensible via DI | Hard-coded dependencies |
| **L**iskov | All consumers implement IConsumerService | Consumers with different signatures |
| **I**nterface Seg | Tests mock only required interfaces | Mocking entire SDK |
| **D**ependency Inv | Services depend on abstractions | Directly instantiating concrete classes |

### Clean Code Checklist (for Dev Implementation)

- ✅ **Names are self-documenting** — `PublishContactEventAsync`, `CreateEnvelope`, not `Proc`, `Pub`
- ✅ **Methods are small & focused** — Each method does one thing
- ✅ **Immutable domain objects** — Use `record` or sealed classes
- ✅ **Error handling explicit** — Logged with context; caller decides retry
- ✅ **No anti-patterns** — No hard-coded values, sync-over-async, reflection, service locators
- ✅ **Dependency injection** — All dependencies injected; mockable in tests
- ✅ **Structured logging** — ILogger with correlation IDs for tracing
- ✅ **Async-first** — All I/O is `async`/`await`; no `.Result` or `.Wait()`

---

## Coverage Validation

After all tests and implementations complete:

```bash
# Measure coverage for all test projects
dotnet test tests/ServiceBusPoc.Tests/ /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Verify ≥80% coverage on:
# - ProducerService
# - All ConsumerService implementations
# - CarwashConsumerService (integration logic)
```

**Acceptance:** Coverage report shows ≥80% for all business logic layers.

---

## Timeline: 14–16 Hours

| Phase | Est. Time | Cumulative |
|-------|-----------|-----------|
| Docker Compose setup | 1–2 hours | 1–2 hours |
| **Producer TDD + Impl** | **2.5 hours** | **3.5–4.5 hours** |
| **Consumer TDD + Impl** (4 consumers) | **4.25 hours** | **7.75–8.75 hours** |
| **Carwash Integration TDD + Impl** | **2.5 hours** | **10.25–11.25 hours** |
| **Integration Tests** (emulator scenarios) | **3 hours** | **13.25–14.25 hours** |
| **Documentation & coverage validation** | **1–1.5 hours** | **14.25–15.75 hours** |

---

## Acceptance Criteria Checklist

- [ ] ProducerService: All unit tests pass, ≥80% coverage
- [ ] ConsumerService (4×): All unit tests pass, ≥80% coverage per consumer
- [ ] CarwashConsumerService: All unit tests pass, ≥80% coverage on integration logic
- [ ] `run-local-poc.ps1`: All 5+ filter routing scenarios pass
- [ ] Carwash end-to-end test: Verifies API call and logging
- [ ] Coverage report: ≥80% on all business logic
- [ ] `dotnet build`: Zero errors, zero warnings
- [ ] `dotnet test`: All tests pass
- [ ] Documentation: Phase 2 setup and topology docs complete

---

## Next Steps

1. **Producer (Remy):** Review and approve hybrid TDD approach
2. **Dev (Nova+Sage+Milo):** Begin with Docker Compose, then Producer TDD tests
3. **QA (Ivy, optional):** Prepare end-to-end scenario verification for Phase 2 completion

**Plan document:** [PHASE_2_PLAN.md](./PHASE_2_PLAN.md)  
**Work items:** 7 tasks + coverage validation in SQL tracking
