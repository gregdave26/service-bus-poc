# Local Aspire orchestration spike

This project evaluates .NET Aspire as a local process orchestrator for the
existing Service Bus POC. It does not replace the Service Bus emulator,
`infra/servicebus/compose.yaml`, the Bicep topology, or the custom dashboard.

## Run

Start the existing emulator first:

```powershell
docker compose --env-file .env -f infra/servicebus/compose.yaml up -d
```

Set the same emulator settings used by the existing local run scripts:

```powershell
$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true"
$env:ServiceBus__Namespace = "sbemulatorns"
$env:ServiceBus__TopicName = "contact.events"
dotnet run --project src/ServiceBusPoc.AppHost/ServiceBusPoc.AppHost.csproj
```

Alternatively, use the repository launcher:

```powershell
.\scripts\run-aspire.ps1
```

The launcher ensures the .NET and Aspire HTTPS development certificates are
trusted before starting the AppHost. If you start Aspire directly instead, run
this once from an elevated PowerShell when prompted:

```powershell
dotnet dev-certs https --trust
aspire certs trust
```

Use `-NoEmulator` when the Compose emulator is already running, or `-NoBuild`
when the AppHost has already been built:

```powershell
.\scripts\run-aspire.ps1 -NoEmulator -NoBuild
```

The host starts the four filtered consumers, Carwash's HTTP API and consumer
process, the producer, and the connectivity verifier. Existing project
configuration remains unchanged; Aspire only supplies the `ServiceBus:*`
environment variables, consumer subscription names, and safe local Producer
defaults. The contact identity, source, and membership defaults can be overridden
with `Producer__*` environment variables. The three routing flags default to
`true` for the sample event but honor explicit `Producer__HasInsurance`,
`Producer__HasParksResorts`, and `Producer__HasCarwashProduct` values, including
`false`.

The producer and verifier are short-lived processes. The consumer processes
remain running until the Aspire application is stopped.

## Configuration and responsibility comparison

See the [source-based comparison](../../docs/architecture/aspire-configuration-comparison.md)
for configuration precedence, topic/subscription wiring, Carwash API ownership,
startup/shutdown differences, and remaining parity gaps. The custom dashboard
and Digital Site applications are intentionally not started by this spike.
Aspire does not replace their user-facing functionality. For an isolated run
without the custom dashboard, set `Dashboard__Enabled=false` in the launcher
environment or ignored root `.env` to disable custom reporting.
