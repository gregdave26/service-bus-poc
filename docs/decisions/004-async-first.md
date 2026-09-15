# ADR 004: Async-First Programming Model

**Status:** APPROVED (2026-09-15)  
**Date:** 2026-09-15  
**Decided By:** Product Owner  

---

## Context

The POC includes console applications that must handle multiple concurrent operations:
- Producers publishing events to Service Bus
- 4 consumers listening to subscriptions simultaneously
- Carwash running event listener and HTTP API concurrently
- Scenario verifier orchestrating multiple producers and consumers

All leverage the .NET 10 async APIs throughout.

## Problem Statement

Should we use async/await throughout the codebase, or keep synchronous code where possible?

- Service Bus SDK is async-first (events delivered via async handlers)
- HTTP API requires async request handling
- Multiple consumers and API need concurrent execution in single process
- Simple sync code vs. scalable async architecture

## Options Considered

### A. Async-First (Selected)
- All I/O operations use async/await (Service Bus, HTTP, logging)
- Enables multiple consumers and API in single process
- Production-grade design
- **Pros:** Scalable, efficient resource use, production-ready, required by SDK
- **Cons:** Slightly more complex code; async all the way down

### B. Sync-First with Async Where Required
- Minimize async; only use where SDK requires it
- Simpler code for business logic
- **Pros:** Easier to understand; less code
- **Cons:** Less scalable; thread pool starvation risk; blocks threads

### C. Mixed Approach
- Async for I/O (Service Bus, HTTP)
- Sync for business logic
- **Pros:** Balance between simplicity and correctness
- **Cons:** Inconsistent; harder to reason about; blocks on sync-over-async

## Decision

**Use async-first programming model throughout.**

- All I/O operations use async/await
- All consumer message handlers are async
- Carwash API endpoints are async
- Producer sends events asynchronously
- Scenario verifier uses async/await for orchestration

## Consequences

### Positive
- Efficient resource utilization (thread pool not exhausted)
- Multiple consumers and API run concurrently in single process
- Proper error propagation through `async` stack
- Aligned with .NET 10 and Service Bus SDK best practices
- Production-grade architecture
- Enables future scaling (async is foundation for distributed systems)

### Negative
- Slightly more verbose code (async Task methods)
- Requires understanding of async/await semantics
- No `.Result` or `.Wait()` (blocking is forbidden)
- Testing requires async test methods

### Mitigation
- Document async patterns in DEVELOPER.md
- Provide code templates for consumer and producer implementations
- Use `async Task Main` in all console apps
- Enforce async in code review

## Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Async deadlocks (.Result/.Wait()) | Medium | Code review; document .Result is forbidden; static analysis |
| Developers unfamiliar with async | Low | Provide templates; pair programming during Phase 2 |
| Debugging async harder | Low | VS debugger has async support; document breakpoint strategies |

## Trade-Offs

- **Complexity vs. Scalability:** Accept slightly more complex code for production-grade design
- **Learning Curve vs. Correctness:** Require async expertise but gain correctness

## Related Decisions

- **ADR 006:** Configuration (configuration loading can be sync or async; keep consistent)
- **ADR 007:** Application architecture (separate console apps benefit from async concurrency model)

## Implementation Notes

All consumer implementations follow this pattern:
```csharp
public class ContactEventConsumer : BackgroundService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ContactEventConsumer> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var processor = _serviceBusClient.CreateProcessor(
            topicName: "contact.events",
            subscriptionName: "subscription-name");
        
        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += ProcessErrorAsync;
        
        await processor.StartProcessingAsync(cancellationToken);
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
    
    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        var contact = JsonSerializer.Deserialize<Contact>(body);
        
        _logger.LogInformation("Processing contact {ContactId}", contact.ContactId);
        
        // Business logic here (async)
        await HandleContactAsync(contact);
        
        await args.CompleteMessageAsync(args.CancellationToken);
    }
    
    private async Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error processing message");
    }
    
    private async Task HandleContactAsync(Contact contact)
    {
        // Async business logic
        await Task.CompletedTask;
    }
}
```

Producer pattern:
```csharp
public class ContactEventProducer
{
    private readonly ServiceBusClient _serviceBusClient;
    
    public async Task PublishContactAsync(Contact contact)
    {
        var sender = _serviceBusClient.CreateSender("contact.events");
        var json = JsonSerializer.Serialize(contact);
        var message = new ServiceBusMessage(json);
        
        await sender.SendMessageAsync(message);
    }
}
```

All main methods use `async Task`:
```csharp
static async Task Main(string[] args)
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices(services =>
        {
            // Service registration
        })
        .Build();
    
    await host.RunAsync();
}
```

## Sign-Off

- ✅ Product Owner (2026-09-15)
