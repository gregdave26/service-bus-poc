# ADR 007: Application Architecture - Separate Console Apps per Role

**Status:** APPROVED (2026-09-15)  
**Date:** 2026-09-15  
**Decided By:** Product Owner  

---

## Context

The POC requires multiple distinct roles with different responsibilities:
- **Producer:** Publishes contact events to Service Bus
- **DigitalChannels Consumer:** Receives all events (validates routing)
- **Insurance Consumer:** Receives insurance-filtered events
- **ParksResorts Consumer:** Receives parks-resorts-filtered events
- **Carwash Consumer+API:** Receives carwash-filtered events and exposes HTTP API
- **Scenario Verifier:** Runs deterministic test scenarios

Each role could be deployed separately or together in different configurations.

## Problem Statement

How should these roles be packaged and deployed?

- Monolithic single app vs. multiple focused apps
- Different scaling and deployment characteristics
- Local dev vs. cloud deployment needs differ

## Options Considered

### A. Separate Console App per Role (Selected)
- `ServiceBusPoc.Producer` — Produces events
- `ServiceBusPoc.DigitalChannels` — Consumer
- `ServiceBusPoc.Insurance` — Consumer
- `ServiceBusPoc.ParksResorts` — Consumer
- `ServiceBusPoc.Carwash` — Consumer + HTTP API
- `ServiceBusPoc.Verifier` — Scenario tester
- Each app has own `Program.cs`, configuration, dependencies
- **Pros:** Clear separation of concerns; independent scaling; testable in isolation; easy to understand
- **Cons:** More projects to manage; more executables; deployment complexity

### B. Unified Console App with Role Selection
- Single `ServiceBusPoc.exe --role producer` or `--role consumer:insurance`
- Role-specific logic behind `if/switch` statements
- **Pros:** One executable; simpler packaging
- **Cons:** Harder to understand; mixed concerns; all roles loaded in memory; difficult to scale individual roles

### C. Minimal WebAPI Host
- Use ASP.NET Core Minimal APIs
- All roles run in single web host
- Producers accessible via HTTP endpoints; consumers as background services
- **Pros:** Modern .NET approach; integrated logging and DI
- **Cons:** More infrastructure; heavier runtime; overkill for MVP

## Decision

**Use separate console app per role.**

- Each role has its own project and executable
- Each reads its own configuration
- Each runs independently (or orchestrated in `run-local-poc.ps1`)
- Shared code in `ServiceBusPoc.Core` project (contracts, utilities, logging setup)

## Consequences

### Positive
- Crystal-clear responsibility per app
- Easy to understand and modify (find code in right project)
- Independent testing and validation
- Easy to run one role while testing another
- Natural scaling: deploy more instances of a single role later
- Easier to onboard new developers (one app = one concern)
- Each app can have its own entry point and configuration
- Scenario verifier orchestrates all apps; can validate integration

### Negative
- More projects to maintain (6 vs. 1)
- More complex build/deploy (6 executables instead of 1)
- Shared code must live in separate project (`ServiceBusPoc.Core`)
- Developers must understand project dependencies

### Mitigation
- Create shared `ServiceBusPoc.Core` project for contracts, utilities, settings
- Use consistent project structure across all consumer apps
- Document in DEVELOPER.md how to add new consumer (copy template)
- PowerShell script orchestrates all apps in `run-local-poc.ps1`

## Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Building/testing 6 apps is slow | Medium | CI runs parallel builds; developers usually work on one app |
| Code duplication across consumers | Medium | Extract common consumer base class in Core project |
| Coordination complexity | Low | Script orchestrates startup/shutdown; CI validates end-to-end |

## Trade-Offs

- **Simplicity vs. Clarity:** Separate apps slightly more complex but far clearer
- **Single Executable vs. Modular:** Multiple executables trade for better modularity

## Related Decisions

- **ADR 004:** Async-first (each app benefits from async model)
- **ADR 006:** Configuration (each app reads own settings from environment)

## Implementation Notes

**Project structure:**
```
src/
├── ServiceBusPoc.Core/                    # Shared contracts, utilities
│   ├── Contracts/
│   │   ├── ContactUpdatedEvent.cs
│   │   ├── ProductHoldingChangeEvent.cs
│   └── ServiceBusSettings.cs
├── ServiceBusPoc.Producer/                 # Producer app
│   ├── Program.cs
│   ├── ProducerService.cs
│   └── ServiceBusPoc.Producer.csproj
├── ServiceBusPoc.DigitalChannels/          # DigitalChannels consumer
│   ├── Program.cs
│   ├── DigitalChannelsConsumer.cs
│   └── ServiceBusPoc.DigitalChannels.csproj
├── ServiceBusPoc.Insurance/                # Insurance consumer
│   ├── Program.cs
│   ├── InsuranceConsumer.cs
│   └── ServiceBusPoc.Insurance.csproj
├── ServiceBusPoc.ParksResorts/             # Parks & Resorts consumer
│   ├── Program.cs
│   ├── ParksResortsConsumer.cs
│   └── ServiceBusPoc.ParksResorts.csproj
├── ServiceBusPoc.Carwash/                  # Carwash consumer + API
│   ├── Program.cs
│   ├── CarwashConsumer.cs
│   ├── CarwashApi.cs
│   ├── CarwashContactRepository.cs
│   └── ServiceBusPoc.Carwash.csproj
└── ServiceBusPoc.Verifier/                 # Scenario verifier
    ├── Program.cs
    ├── ScenarioRunner.cs
    ├── MockPulseClient.cs
    └── ServiceBusPoc.Verifier.csproj

tests/
├── ServiceBusPoc.Core.Tests/
├── ServiceBusPoc.Producer.Tests/
├── ServiceBusPoc.Insurance.Tests/
├── ServiceBusPoc.Carwash.Tests/
└── ServiceBusPoc.Verifier.Tests/
```

**Shared Core project (.csproj):**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Azure.Messaging.ServiceBus" Version="7.x" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="8.x" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.x" />
  </ItemGroup>
</Project>
```

**Consumer template (Program.cs):**
```csharp
using Microsoft.Extensions.Hosting;
using ServiceBusPoc.Core;
using ServiceBusPoc.Insurance;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configuration
        services.Configure<ServiceBusSettings>(options =>
        {
            options.ConnectionString = 
                Environment.GetEnvironmentVariable("ServiceBusConnectionString")
                ?? throw new InvalidOperationException("Missing: ServiceBusConnectionString");
            options.TopicName = "contact.events";
            options.SubscriptionName = "insurance";
        });

        // Service registration
        services.AddSingleton<ServiceBusClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<ServiceBusSettings>>();
            return new ServiceBusClient(settings.Value.ConnectionString);
        });

        // Hosted service (runs in background)
        services.AddHostedService<InsuranceConsumer>();
    })
    .Build();

await host.RunAsync();
```

**Verifier orchestrates all (simplified example):**
```powershell
# run-local-poc.ps1
$apps = @(
    "ServiceBusPoc.DigitalChannels",
    "ServiceBusPoc.Insurance",
    "ServiceBusPoc.ParksResorts",
    "ServiceBusPoc.Carwash"
)

# Start all consumers in background
foreach ($app in $apps) {
    Write-Host "Starting $app..."
    Start-Job -ScriptBlock { 
        cd src/$app; dotnet run 
    } -Name $app
}

# Run producer and verifier
Write-Host "Running scenarios..."
dotnet run --project src/ServiceBusPoc.Verifier

# Cleanup
Get-Job | Stop-Job
Get-Job | Remove-Job
```

## Post-MVP Roadmap

- Consider consolidation if complexity explodes (unlikely with 6 apps)
- Containerize each app separately for independent Docker deployments
- Create Kubernetes manifests per app for scaling

## Sign-Off

- ✅ Product Owner (2026-09-15)
