# Phase 2 Development Guidelines (RAC Engineering Standards)

**Applies to:** Phase 2 MVP Implementation (Producer, Consumers, Integration)  
**Date:** 2026-09-15  
**Scope:** All business logic and test code

---

## Before You Start: Read These First

1. ✅ [PHASE_2_PLAN.md](../../PHASE_2_PLAN.md) — Complete specification and acceptance criteria
2. ✅ [PHASE_2_HYBRID_TDD.md](../../PHASE_2_HYBRID_TDD.md) — TDD workflow with examples
3. ✅ [PHASE_2_APPROVED.md](../../PHASE_2_APPROVED.md) — Architecture standards and checklist

---

## Core Principles (Non-Negotiable)

### 1. Clean Architecture: Separate Domain from Infrastructure

**Domain Logic** (business rules, no external dependencies):
- Event envelope construction
- Event deserialization and attribute extraction
- Validation logic
- Retry strategy (exponential backoff calculation)

**Infrastructure Layer** (via interfaces):
- Service Bus SDK interactions
- HTTP client calls
- Logging
- Configuration

**Example:**
```csharp
// ✅ DOMAIN LOGIC (testable without Service Bus)
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

// ❌ WRONG (mixes infrastructure with domain)
private EventEnvelope CreateEnvelope(ContactData contact)
{
    var envelope = new EventEnvelope { /* ... */ };
    await _serviceBusClient.SendAsync(envelope);  // ❌ SDK call mixed with domain logic
    return envelope;
}
```

### 2. SOLID: One Principle Per Service Class

| Principle | Implementation |
|-----------|---|
| **S** Single Responsibility | ProducerService publishes events; ConsumerService receives events; each has ONE reason to change |
| **O** Open/Closed | Services use interfaces; can add retry strategies without modifying ProducerService |
| **L** Liskov Substitution | All consumers (DigitalChannels, Insurance, ParksResorts, Carwash) implement IConsumerService identically |
| **I** Interface Segregation | Tests mock only required interfaces (IServiceBusSender), not entire SDK |
| **D** Dependency Inversion | Services depend on abstractions (IServiceBusSender, ILogger), not concrete classes |

### 3. Clean Code: Self-Documenting Intent

**Names explain purpose without comments:**
```csharp
// ✅ GOOD: Name is self-documenting
public async Task PublishWithRetryAsync(EventEnvelope envelope)
public EventEnvelope CreateEnvelope(ContactData contact)
public void LogEventReceived(string subscriptionName, EventEnvelope envelope)

// ❌ BAD: Name requires explanation
public async Task Pub(object x)  // What does this do? Publish? Publicize?
public object Proc(object y)     // Process? Produce?
public void Log1(string a, object b)  // What are a and b?
```

**Methods do ONE thing:**
```csharp
// ✅ GOOD: Focused method
private EventEnvelope CreateEnvelope(ContactData contact)
{
    return new EventEnvelope(
        Id: Guid.NewGuid().ToString(),
        // ...
    );
}

// ❌ BAD: Multiple concerns
private async Task PublishAsync(ContactData contact)
{
    var envelope = new EventEnvelope { /* ... */ };
    var message = new ServiceBusMessage(JsonSerializer.Serialize(envelope));
    await _sender.SendMessageAsync(message);  // Creates + serializes + sends = 3 concerns
    _logger.LogInformation("Event sent");
}
```

**Immutable domain objects (prevent bugs):**
```csharp
// ✅ GOOD: Immutable record (sealed prevents inheritance)
public sealed record EventEnvelope(
    string Id,
    string Type,
    DateTime Timestamp,
    string CorrelationId,
    string Source,
    string DataVersion,
    object Data);

// ❌ BAD: Mutable properties (can be changed after construction)
public class EventEnvelope
{
    public string Id { get; set; }  // Can be modified!
    public string Type { get; set; }
}
```

---

## TDD Workflow (For All Business Logic)

### Step 1: Write Test First

```csharp
[Fact]
public async Task PublishContactEventAsync_CreatesEnvelopeWithCorrelationId()
{
    // Arrange: Mock only infrastructure
    var mockSender = new Mock<IServiceBusSender>();
    var producer = new ProducerService(mockSender.Object);
    var contact = new ContactData { ContactId = "C001", FirstName = "John" };
    
    // Act
    await producer.PublishContactEventAsync(contact);
    
    // Assert: Verify domain logic
    mockSender.Verify(s => s.SendMessageAsync(
        It.Is<ServiceBusMessage>(m => m.CorrelationId != null)), 
        Times.Once);
}
```

**Checklist:**
- ✅ Arrange: Mock infrastructure (interfaces, not concrete classes)
- ✅ Act: Call domain logic
- ✅ Assert: Verify business logic (envelope structure, not SDK calls)

### Step 2: Implement Minimal Code to Pass Test

```csharp
public class ProducerService
{
    private readonly IServiceBusSender _sender;
    private readonly ICorrelationIdGenerator _correlationIdGenerator;
    
    public ProducerService(
        IServiceBusSender sender, 
        ICorrelationIdGenerator correlationIdGenerator)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _correlationIdGenerator = correlationIdGenerator ?? throw new ArgumentNullException(nameof(correlationIdGenerator));
    }
    
    public async Task PublishContactEventAsync(ContactData contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        
        var envelope = CreateEnvelope(contact);
        var message = ConvertToServiceBusMessage(envelope);
        
        await _sender.SendMessageAsync(message);
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

**Checklist:**
- ✅ Constructor injection (all dependencies explicit)
- ✅ Separate method for domain logic (CreateEnvelope)
- ✅ ArgumentNullException for input validation

### Step 3: Refactor (Keep Tests Passing)

Add retry logic, error handling, logging:

```csharp
public async Task PublishContactEventAsync(ContactData contact)
{
    ArgumentNullException.ThrowIfNull(contact);
    
    var envelope = CreateEnvelope(contact);
    
    try
    {
        await PublishWithRetryAsync(envelope);
    }
    catch (ServiceBusCommunicationException ex)
    {
        _logger.LogError(ex, "Failed to publish event {EventId}", envelope.Id);
        throw;
    }
}

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
            _logger.LogWarning(ex, "Retry {Attempt}/{MaxRetries}", attempt, maxRetries);
            await Task.Delay(delay);
        }
    }
}
```

**Checklist:**
- ✅ Retry logic tested separately
- ✅ Error logging includes context (EventId, Attempt)
- ✅ All existing tests still pass

### Step 4: Verify with Integration Test

Run manual spot-check against emulator:
```powershell
dotnet run --project src/ServiceBusPoc.Producer -- --contact-id C001 --first-name John --has-carwash-product true
```

**Checklist:**
- ✅ Producer runs without errors
- ✅ Event appears in subscription logs

---

## Dependency Injection Pattern (Required)

**All services must use constructor injection:**

```csharp
// ✅ GOOD: Dependencies explicit; mockable
public class ProducerService
{
    private readonly IServiceBusSender _sender;
    private readonly ICorrelationIdGenerator _generator;
    private readonly ILogger<ProducerService> _logger;
    
    public ProducerService(
        IServiceBusSender sender,
        ICorrelationIdGenerator generator,
        ILogger<ProducerService> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}

// ❌ WRONG: New dependencies inside (can't mock; hidden)
public class ProducerService
{
    private IServiceBusSender _sender = new ServiceBusClient().CreateSender("topic");  // ❌ Hidden dependency
    
    public async Task PublishAsync(ContactData contact)
    {
        var generator = new CorrelationIdGenerator();  // ❌ Can't replace
        // ...
    }
}
```

**In Program.cs:**
```csharp
services.AddSingleton<IServiceBusSender>(sp =>
    new ServiceBusSenderAdapter(new ServiceBusClient(connectionString).CreateSender(topicName)));
services.AddSingleton<ICorrelationIdGenerator, CorrelationIdGenerator>();
services.AddScoped<ProducerService>();
services.AddLogging();
```

---

## Error Handling: Explicit, Not Silent

**✅ GOOD: Logged, caller decides:**
```csharp
try
{
    await _sender.SendMessageAsync(message);
}
catch (ServiceBusCommunicationException ex)
{
    _logger.LogWarning(ex, "Communication error for event {EventId}", envelope.Id);
    throw;  // Caller decides: retry or fail
}
```

**❌ BAD: Silent failure:**
```csharp
try
{
    await _sender.SendMessageAsync(message);
}
catch { }  // Swallowed! No logging, no throw
```

**❌ BAD: Too generic:**
```csharp
try
{
    await _sender.SendMessageAsync(message);
}
catch (Exception ex)  // Catches everything, including programming errors
{
    _logger.LogWarning(ex, "Error");
    // Lose important stack trace info
}
```

---

## Async/Await: No Blocking Calls

**✅ GOOD: Pure async:**
```csharp
public async Task<EventEnvelope> DeserializeEnvelopeAsync(string json)
{
    return await Task.Run(() => JsonSerializer.Deserialize<EventEnvelope>(json));
}

public async Task ReceiveEventsAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        var message = await _receiver.ReceiveMessageAsync();  // Async wait
        // ...
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);  // Async delay
    }
}
```

**❌ BAD: Blocking calls:**
```csharp
public async Task<EventEnvelope> DeserializeEnvelopeAsync(string json)
{
    return JsonSerializer.Deserialize<EventEnvelope>(json).Result;  // ❌ .Result blocks thread
}

public async Task ReceiveEventsAsync()
{
    while (true)
    {
        var message = _receiver.ReceiveMessage().Result;  // ❌ Blocking
        Thread.Sleep(1000);  // ❌ Blocks thread (use await Task.Delay)
    }
}
```

---

## Structured Logging: Correlation IDs for Tracing

**✅ GOOD: Structured with correlation ID:**
```csharp
var correlationId = _correlationIdGenerator.GenerateId();

_logger.LogInformation(
    "Event {EventId} received on subscription {SubscriptionName} with correlation {CorrelationId}",
    envelope.Id,
    subscriptionName,
    correlationId);
```

**❌ BAD: Unstructured:**
```csharp
_logger.LogInformation($"Got event {envelope.Id}");  // No subscription, no correlation ID
```

**Structured logging enables:**
- Filtering by subscription
- Correlation ID for end-to-end tracing
- Aggregation and analysis

---

## Anti-Patterns to AVOID

| Anti-Pattern | Why Bad | Fix |
|---|---|---|
| **Hard-coded values** | Tests fail; config changes require recompile | Use IConfiguration, environment variables, DI |
| **Sync-over-async** (`.Result`, `.Wait()`) | Deadlock risk; blocks threads | Use `await` throughout |
| **Catch-swallow** | Silent failures; hard to debug | Log exceptions; re-throw or handle meaningfully |
| **Service Locator** | Hidden dependencies; hard to test | Constructor injection only |
| **God Object** | Too many responsibilities | Split into focused services |
| **Reflection** | Slow; breaks at runtime | Use concrete types + DI |
| **Premature optimization** | Over-engineering; hard to maintain | Keep it simple; optimize only after profiling |
| **Mutable domain objects** | Bugs from accidental modification | Use `record` or sealed classes |

---

## Code Review Checklist (Before Opening PR)

Run through this checklist before submitting for review:

### Architecture
- ✅ Domain logic separated from infrastructure (Service Bus, HTTP)
- ✅ All business logic is unit-testable with mocked dependencies
- ✅ Services depend on interfaces, not concrete classes

### SOLID
- ✅ Each class has ONE reason to change (Single Responsibility)
- ✅ Services use interfaces for extensibility (Open/Closed)
- ✅ All consumers have identical interface (Liskov Substitution)
- ✅ Tests mock only required dependencies (Interface Segregation)
- ✅ Services depend on abstractions, not concretions (Dependency Inversion)

### Clean Code
- ✅ Method/variable names are self-documenting
- ✅ No comments explaining "what" (if needed, code is unclear)
- ✅ Each method does ONE thing
- ✅ Domain objects are immutable (record or sealed class)
- ✅ No hard-coded values

### Testing
- ✅ Tests written first (TDD)
- ✅ All business logic is mockable via interfaces
- ✅ Coverage ≥80% on [component]Service
- ✅ All tests pass: `dotnet test`

### Async/Await
- ✅ All I/O is async (no `.Result`, `.Wait()`)
- ✅ CancellationToken threaded through async methods
- ✅ No sync-over-async

### Error Handling
- ✅ Exceptions caught where meaningful
- ✅ Logging includes context (correlation ID, event ID, attempt)
- ✅ No catch-swallow; re-throw or handle explicitly

### Build & Tooling
- ✅ `dotnet build` → 0 errors, 0 warnings
- ✅ `dotnet test` → All tests pass
- ✅ `dotnet test /p:CollectCoverage=true` → ≥80% coverage

---

## Example: Complete Consumer Service (Following All Guidelines)

```csharp
// ConsumerService.cs
public class ConsumerService : IConsumerService
{
    private readonly IServiceBusReceiver _receiver;
    private readonly ILogger<ConsumerService> _logger;
    private readonly string _subscriptionName;
    
    public ConsumerService(
        IServiceBusReceiver receiver,
        ILogger<ConsumerService> logger,
        string subscriptionName)
    {
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subscriptionName = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));
    }
    
    public async Task ReceiveEventsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = await _receiver.ReceiveMessageAsync(cancellationToken);
                
                if (message == null) continue;
                
                await ProcessMessageAsync(message, cancellationToken);
                await _receiver.CompleteMessageAsync(message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer {SubscriptionName} cancelled", _subscriptionName);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in consumer {SubscriptionName}", _subscriptionName);
            }
        }
    }
    
    private async Task ProcessMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(message.Body);
            var envelope = await DeserializeEnvelopeAsync(json);
            
            _logger.LogInformation(
                "Event {EventId} received on {SubscriptionName}",
                envelope.Id,
                _subscriptionName);
            
            await HandleEventAsync(envelope, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message on {SubscriptionName}", _subscriptionName);
            throw;
        }
    }
    
    private async Task<EventEnvelope> DeserializeEnvelopeAsync(string json)
    {
        return await Task.Run(() => JsonSerializer.Deserialize<EventEnvelope>(json)
            ?? throw new InvalidOperationException("Envelope deserialization returned null"));
    }
    
    private async Task HandleEventAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        // Business logic here (can be overridden in subclasses)
        await Task.CompletedTask;
    }
}

// Test
[Fact]
public async Task ReceiveEventsAsync_WithValidMessage_DeserializesAndLogs()
{
    // Arrange
    var mockReceiver = new Mock<IServiceBusReceiver>();
    var mockLogger = new Mock<ILogger<ConsumerService>>();
    var consumer = new ConsumerService(mockReceiver.Object, mockLogger.Object, "test-subscription");
    
    var message = new Mock<ServiceBusReceivedMessage>();
    var json = JsonSerializer.Serialize(new EventEnvelope(
        Id: "evt-001",
        Type: "contact.updated",
        Timestamp: DateTime.UtcNow,
        CorrelationId: "trace-123",
        Source: "test",
        DataVersion: "1.0",
        Data: new ContactData { ContactId = "C001" }));
    
    message.Setup(m => m.Body).Returns(new BinaryData(json));
    mockReceiver.Setup(r => r.ReceiveMessageAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(message.Object);
    
    using var cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromMilliseconds(100));
    
    // Act
    await consumer.ReceiveEventsAsync(cts.Token);
    
    // Assert
    mockLogger.Verify(l => l.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("Event evt-001")),
        null,
        null!), Times.Once);
}
```

---

## Summary

**Every line of Phase 2 code must follow:**

1. ✅ **Clean Architecture:** Domain logic independent of infrastructure
2. ✅ **SOLID Principles:** One responsibility per class; depend on abstractions
3. ✅ **Clean Code:** Self-documenting names; small focused methods; immutable objects
4. ✅ **TDD Workflow:** Tests written first; domain logic testable with mocks
5. ✅ **No Anti-Patterns:** No hard-coded values, sync-over-async, or silent failures
6. ✅ **Structured Logging:** Correlation IDs for end-to-end tracing
7. ✅ **Zero Warnings:** `dotnet build` and `dotnet test` must pass cleanly

**If you're unsure about a design decision, ask:** Is this code domain logic (testable, no dependencies) or infrastructure (via interfaces)?

---

**Questions?** Review PHASE_2_PLAN.md and PHASE_2_HYBRID_TDD.md, then ask Producer (Remy) for clarification.
