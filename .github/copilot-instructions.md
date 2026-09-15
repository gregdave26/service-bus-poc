# Repository Instructions - Service Bus POC

## User-Level Design Principles

Apply the user's detailed instruction sets for:

- [Clean Code](C:/Users/dg15938/.copilot/instructions/clean-code.md)
- [SOLID principles](C:/Users/dg15938/.copilot/instructions/solid-principles.md)
- [GRASP principles](C:/Users/dg15938/.copilot/instructions/grasp-principles.md)

## Code Style

- Prefer self-documenting code that expresses intent through clear names, focused methods, and simple structure.
- Follow Clean Code principles and avoid comments that merely restate what the code does.
- Add comments only when they clarify non-obvious reasoning, business rules, external constraints, or important trade-offs.
- Keep comments concise and update or remove them when the related code changes.

---

## Service Bus POC-Specific Guidance

### Target Framework and Language

- **Target:** .NET 10.0 (modern, LTS-eligible)
- **C# Version:** 12 (implicit)
- **Nullable Reference Types:** Always enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings:** Enabled to reduce boilerplate

### Async-First Programming (ADR-004)

**All I/O is async by default. Never block threads.**

```csharp
// ✅ Correct
public async Task RunAsync()
{
    await serviceBusClient.SendMessageAsync(message);
}

// ❌ Never do this
public void Run()
{
    serviceBusClient.SendMessageAsync(message).Wait();  // DEADLOCK RISK
}
```

- Entry point: `async Task Main(string[] args)` in `Program.cs`
- All Service Bus operations use async variants
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- Chain async calls with `await` or return `Task` directly

### Dependency Injection Setup

Every console app follows this DI pattern:

```csharp
// Program.cs
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services
            .AddLogging(builder => builder.AddStructuredConsoleLogging())
            .AddServiceBusConfiguration(context.Configuration)
            .AddScoped<MyService>();
    })
    .Build();

var service = host.Services.GetRequiredService<MyService>();
await service.RunAsync();
```

**Key Principles:**
- All configuration from environment variables (ADR-006)
- Use `IOptions<T>` for configuration injection
- Register services in `ConfigureServices` delegate
- Resolve and run services after building host
- No static state; always use constructor injection

### Configuration via IOptions<T>

```csharp
public class MyService
{
    private readonly IOptions<ServiceBusSettings> _settings;
    
    public MyService(IOptions<ServiceBusSettings> settings)
    {
        _settings = settings;
    }
    
    public async Task RunAsync()
    {
        var connectionString = _settings.Value.ConnectionString;
    }
}
```

**Rules:**
- Always inject `IOptions<T>`, never `T` directly
- Configuration classes in `ServiceBusPoc.Core.Configuration.*`
- Use `[Required]` and range validation data annotations
- Settings loaded from environment variable sections (e.g., `ServiceBusSettings` → `ServiceBus:*`)

### Structured Logging

**Never string-format in log calls; always use named properties.**

```csharp
// ✅ Correct
_logger.LogInformation("Contact {ContactId} received from {Source}", 
    contact.ContactId, source);

// ❌ Wrong
_logger.LogInformation($"Contact {contact.ContactId} received");
```

- Inject `ILogger<T>` where T is the containing class
- Use `LogInformation`, `LogWarning`, `LogError` appropriately
- Include context as named properties for downstream tracing

### Event Contracts and Serialization

**JSON Schema as Source of Truth:**

- Event shapes defined in `/contracts/*.schema.json` (draft-7)
- C# DTOs generated or coded to match schemas exactly
- Data annotations (`[Required]`, `[EmailAddress]`) enforce schema constraints

**Serialization/Deserialization:**

```csharp
using ServiceBusPoc.Core.Utilities;

// Always use shared options
var json = JsonSerializer.Serialize(contact, 
    JsonSerializerOptionsHelper.DefaultOptions);

var deserialized = JsonSerializer.Deserialize<ContactData>(json,
    JsonSerializerOptionsHelper.DefaultOptions);
```

**Never use default `JsonSerializerOptions()`** — enforces consistent property naming (camelCase), null handling, and enum serialization.

### Testing

- **Framework:** xUnit (v2.9+)
- **Mocking:** Moq (v4.20+)
- **Target Coverage:** ≥80% (see ADR-007)

**Test Structure:**

```csharp
public class ContactDataTests
{
    [Fact]
    public void Validation_WithInvalidEmail_Fails()
    {
        var contact = new ContactData { Email = "not-email" };
        var context = new ValidationContext(contact);
        var results = new List<ValidationResult>();
        
        var isValid = Validator.TryValidateObject(contact, context, results, true);
        
        Assert.False(isValid);
    }
    
    [Theory]
    [InlineData("valid@example.com", true)]
    [InlineData("invalid", false)]
    public void EmailValidation_WithVariousInputs(string email, bool shouldPass)
    {
        // ...
    }
}
```

**Test Scope:**
- DTO serialization/deserialization round-trips
- Validation attribute behavior
- Configuration loading from environment
- Service Bus message handling (with mocks)
- **Out of scope (Phase 1):** End-to-end Service Bus topology (Phase 2+)

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `ContactData`, `ProducerService` |
| Properties | PascalCase | `FirstName`, `HasInsurance` |
| Methods | PascalCase | `RunAsync()`, `SendMessageAsync()` |
| Local variables | camelCase | `contactId`, `isValid` |
| Constants | PascalCase | `MaxRetries`, `DefaultTimeout` |
| Interfaces | I + PascalCase | `ILogger<T>`, `IOptions<T>` |

### Error Handling

```csharp
try 
{
    await client.SendMessageAsync(message);
}
catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
{
    _logger.LogWarning("Message lock lost: {Message}", ex.Message);
    // Decide: retry, dead-letter, or propagate
}
catch (ServiceBusException ex)
{
    _logger.LogError(ex, "Service Bus error");
    throw;
}
```

**Rules:**
- Catch specific exception types, not bare `catch`
- Log full context with `LogError(ex, "...")`
- Re-throw when unable to recover
- Never swallow exceptions silently

### Configuration Loading

All apps load from environment variables only (ADR-006). **No secrets in code.**

```powershell
# Example .env or CI/CD variables
ServiceBus__ConnectionString="Endpoint=sb://..."
ServiceBus__Namespace="my-namespace"
ServiceBus__TopicName="contact.events"
Carwash__ApiUrl="https://api.carwash.example.com"
Carwash__MockMode="true"
```

Apps parse these via `IConfiguration` → `IOptions<T>` pattern.

### Common Mistakes to Avoid

❌ **Blocking async code**
```csharp
var result = client.SendMessageAsync(msg).Result;  // DEADLOCK!
```

❌ **Hardcoded secrets**
```csharp
var client = new ServiceBusClient("Endpoint=sb://...;SharedAccessKey=...");
```

❌ **String-formatted logging**
```csharp
_logger.LogInformation($"ID: {id}");  // Use structured logging
```

❌ **Bare exception catches**
```csharp
try { /* ... */ } catch { }  // WRONG!
```

✅ **Always:**
- Use `async/await` consistently throughout
- Load all configuration from environment variables
- Use structured logging with named properties
- Log and context before re-throwing exceptions
- Include XML documentation on public APIs

### Key ADRs for This Project

- **ADR-004:** Async-first programming model (no blocking)
- **ADR-006:** Configuration via environment variables only
- **ADR-007:** Testing strategy and coverage targets (≥80%)

For complete context, see `docs/decisions/` folder.

---

**Last Updated:** 2026-09-15  
**Phase:** 1 (Foundation & Setup Complete)
