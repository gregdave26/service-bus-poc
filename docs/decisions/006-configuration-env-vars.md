# ADR 006: Configuration & Secrets Management - Environment Variables

**Status:** APPROVED (2026-09-15)  
**Date:** 2026-09-15  
**Decided By:** Product Owner  

---

## Context

All console applications and services need configuration like:
- Service Bus connection string
- Topic and subscription names
- Carwash API port
- Logging levels

Sensitive values (connection strings, API keys) must never be committed to source code.

## Problem Statement

How should we manage application configuration and secrets in MVP and cloud deployment?

- MVP needs simple, zero-friction setup
- Developers need environment-specific config (local emulator, cloud)
- Secrets must never leak to GitHub
- Configuration must be externally configurable without code changes

## Options Considered

### A. Environment Variables Only (Selected)
- All configuration via environment variables
- Application reads from `IConfiguration` environment provider
- Simple setup: `$env:ServiceBusConnectionString = "..."` in PowerShell
- **Pros:** Simple, no files, environment-native, easy per-deployment override
- **Cons:** Less organized; no defaults; hard to discover available variables

### B. appsettings.json + Environment Overrides
- `appsettings.json` contains non-sensitive defaults
- Environment variables override for sensitive values
- Standard .NET practice
- **Pros:** Discoverable defaults; clean configuration structure
- **Cons:** Risk of committing secrets if not careful; more files to manage

### C. Azure Key Vault from Day 1
- Use Azure Identity + Key Vault SDK
- Cloud-grade secrets management
- **Pros:** Production-ready; secure storage; audit trail
- **Cons:** Requires Azure setup; friction for local dev; overkill for MVP

## Decision

**Use environment variables only for MVP.**

- All configuration via environment variables
- `IConfiguration` reads from environment provider
- `IOptions<T>` pattern for strongly-typed settings
- No sensitive values in appsettings.json or code
- Pre-commit hook to catch secrets (post-MVP: add to CI)

## Consequences

### Positive
- Zero risk of secret leakage (no files with secrets)
- Simple for local dev (PowerShell `$env:` commands)
- Docker Compose can set via `environment:` section
- CI/CD can inject secrets via runner environment
- Easy to verify no secrets in code (grep for connection string patterns)
- Minimal configuration boilerplate

### Negative
- No default values; must set all variables
- Less discoverable (developers must know variable names)
- No audit trail or versioning (unlike Key Vault)
- Hard to maintain mapping of variable names to purpose
- Local development requires manual setup

### Mitigation
- Document all environment variables in DEVELOPER.md with examples
- Create `.env.example` file with all variable names (no values)
- Phase 4: Transition to Key Vault for cloud
- Create setup script (`scripts/setup-env.ps1`) to ease local setup

## Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Developer forgets to set required variable | Low | Application fails fast with clear error; document setup |
| Secret accidentally pasted in terminal | Low | PowerShell history can leak; educate team; consider secret masking |
| Different variables across environments | Low | Document environment-specific variables; CI validates |

## Trade-Offs

- **Discoverability vs. Simplicity:** Sacrifice documentation for zero friction
- **Security Features vs. MVP Speed:** Simple env vars traded for faster setup; Key Vault post-MVP

## Related Decisions

- **ADR 001:** Emulator-first (environment variables configure emulator topology)
- **ADR 007:** Separate console apps (each app reads own settings; keep them isolated)

## Implementation Notes

**Settings class with IOptions pattern:**
```csharp
namespace ServiceBusPoc.Configuration;

public class ServiceBusSettings
{
    public required string ConnectionString { get; set; }
    public required string TopicName { get; set; }
    public required string SubscriptionName { get; set; }
}

public class CarwashSettings
{
    public required int ApiPort { get; set; }
    public required int MessageProcessingTimeoutSeconds { get; set; }
}
```

**Dependency injection (Program.cs):**
```csharp
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    // Load from environment variables
    services.Configure<ServiceBusSettings>(options =>
    {
        options.ConnectionString = 
            Environment.GetEnvironmentVariable("ServiceBusConnectionString")
            ?? throw new InvalidOperationException("Missing: ServiceBusConnectionString");
        
        options.TopicName = 
            Environment.GetEnvironmentVariable("ServiceBusTopicName") 
            ?? "contact.events";
        
        options.SubscriptionName = 
            Environment.GetEnvironmentVariable("ServiceBusSubscriptionName")
            ?? throw new InvalidOperationException("Missing: ServiceBusSubscriptionName");
    });
    
    services.Configure<CarwashSettings>(options =>
    {
        options.ApiPort = int.Parse(
            Environment.GetEnvironmentVariable("CarwashApiPort") ?? "5000");
    });
    
    // Register services using IOptions<T>
    services.AddSingleton<ServiceBusClient>(sp =>
    {
        var settings = sp.GetRequiredService<IOptions<ServiceBusSettings>>();
        return new ServiceBusClient(settings.Value.ConnectionString);
    });
});

var host = builder.Build();
await host.RunAsync();
```

**Consumer using settings:**
```csharp
public class InsuranceConsumer : BackgroundService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IOptions<ServiceBusSettings> _options;
    
    public InsuranceConsumer(ServiceBusClient client, IOptions<ServiceBusSettings> options)
    {
        _serviceBusClient = client;
        _options = options;
    }
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var processor = _serviceBusClient.CreateProcessor(
            _options.Value.TopicName,
            _options.Value.SubscriptionName);
        
        // ... rest of implementation
    }
}
```

**Local setup (PowerShell):**
```powershell
# Set environment variables for local development
$env:ServiceBusConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=..."
$env:ServiceBusTopicName = "contact.events"
$env:ServiceBusSubscriptionName = "insurance"
$env:CarwashApiPort = "5000"

# Run consumer
dotnet run --project src/ServiceBus.Poc.Insurance
```

**Docker setup (compose.yaml):**
```yaml
services:
  insurance-consumer:
    build: .
    environment:
      - ServiceBusConnectionString=Endpoint=sb://servicebus:5672/...
      - ServiceBusTopicName=contact.events
      - ServiceBusSubscriptionName=insurance
    depends_on:
      - servicebus
```

**Documentation (.env.example):**
```
# Service Bus Configuration
ServiceBusConnectionString=
ServiceBusTopicName=contact.events
ServiceBusSubscriptionName=

# Carwash Configuration
CarwashApiPort=5000
CarwashMessageProcessingTimeoutSeconds=30

# Logging
DOTNET_LOG_LEVEL=Information
```

## Post-MVP Roadmap

Phase 4 (Cloud Deployment):
- Add `Azure.Identity` + `Azure.Security.KeyVault.Secrets`
- Update configuration to read from Key Vault for cloud
- Keep environment variables for local dev
- Document transition in ADR-006-v2

Phase 5 (Advanced):
- Implement configuration provider pattern
- Support multiple configuration sources (env vars, Key Vault, config files)
- Add configuration change notifications

## Sign-Off

- ✅ Product Owner (2026-09-15)
