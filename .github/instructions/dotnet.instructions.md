---
description: 'C# and .NET conventions for the Service Bus POC'
applyTo: '**/*.cs'
---

# .NET Conventions

Design principles (Clean Code, SOLID, GRASP) come from the user-level instruction set and are not repeated here.

## Language and Formatting

- Target .NET 10.0 with nullable reference types and implicit usings enabled.
- Apply the style in `.editorconfig`; prefer file-scoped namespaces and single-line usings.
- Use pattern matching and switch expressions where they read more clearly.
- Use `nameof` rather than string literals for member names.
- Use `is null` / `is not null`, and trust null annotations instead of adding redundant guards.
- XML doc comments on public APIs.

## Naming

PascalCase for types, methods, properties, and constants. camelCase for locals and private fields. Interfaces are `I`-prefixed.

## Async

All I/O is async. Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`. Thread `CancellationToken` through async call chains. Entry point is `async Task Main`.

## Dependency Injection and Configuration

- Constructor injection only; no static state.
- Build console apps with `Host.CreateDefaultBuilder`, registering services in `ConfigureServices`.
- Configuration comes from environment variables only (ADR-006) — never hard-code secrets.
- Inject `IOptions<T>`, never `T` directly. Configuration types live in `ServiceBusPoc.Core.Configuration.*` and use validation data annotations.

## Logging

Inject `ILogger<T>`. Always use named properties, never interpolated strings:

```csharp
_logger.LogInformation("Contact {ContactId} received from {Source}", contact.ContactId, source);
```

Include correlation and event identifiers so messages can be traced downstream.

## Error Handling

Catch specific exception types with filters where the distinction matters, log with full context via `LogError(ex, ...)`, and re-throw when recovery isn't possible. Never swallow exceptions.

```csharp
catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
```

## Event Contracts and Serialization

- JSON Schema files in `/contracts/*.schema.json` (draft-7) are the source of truth; C# DTOs must match them exactly.
- Always serialize via `JsonSerializerOptionsHelper.DefaultOptions` — never a default `JsonSerializerOptions()`.

## Testing

xUnit with Moq, target ≥80% meaningful coverage (ADR-007). Write tests first where practical. Cover happy paths, validation, error handling, and edge cases. Do not emit "Arrange/Act/Assert" comments. Match the naming style of nearby test files.

Before opening a PR: `dotnet build` with zero warnings, and `dotnet test` passing.
