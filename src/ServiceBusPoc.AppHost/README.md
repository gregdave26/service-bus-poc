# Local Aspire orchestration

This is an **optional launcher**, not the default workflow. See the
[configuration comparison and integration limitations](../../docs/architecture/aspire-configuration-comparison.md)
before launching. A Git worktree does not isolate Docker resources, ports or
broker subscriptions. Do not run the shared defaults alongside another session.

Aspire orchestrates the existing Service Bus POC processes: the contact event
consumers, producer, verifier, and the Digital Site commerce services. It does
not replace the Service Bus emulator, `infra/servicebus/compose.yaml`, or the
Bicep topology.

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

The launcher also starts the Node.js dashboard UI, which is the landing page at
`http://localhost:5100` (`-NoUI` skips it, `-NoBrowser` prevents opening it).
The Aspire dashboard URL, which changes each run, is printed when the AppHost
starts.

The host starts the four filtered consumers, Carwash's HTTP API and consumer
process, the producer, the connectivity verifier, and the Digital Site services:
CommerceToolsStub (`http://localhost:5201`), CommerceApi (`http://localhost:5200`),
CartProcessor, and FulfilmentStub. Existing project configuration remains
unchanged. Aspire supplies the `ServiceBus:*` environment variables, consumer
subscription names, safe local Producer defaults, and the Digital Site
`CommerceTools__ApiUrl` (taken from the stub endpoint) and `CommerceTools__ProjectKey`
(`CommerceTools:ProjectKey` from configuration, default `rac-rsa-poc`). The
contact identity, source, and membership defaults can be overridden
with `Producer__*` environment variables. The three routing flags default to
`true` for the sample event but honor explicit `Producer__HasInsurance`,
`Producer__HasParksResorts`, and `Producer__HasCarwashProduct` values, including
`false`.

The producer and verifier are short-lived processes. The consumer and Digital
Site processes remain running until the Aspire application is stopped.

## Configuration and responsibility comparison

See the [source-based comparison](../../docs/architecture/aspire-configuration-comparison.md)
for configuration precedence, topic/subscription wiring, Carwash API ownership,
startup/shutdown differences, and remaining parity gaps. For an isolated run
without custom reporting, set `Dashboard__Enabled=false` in the launcher
environment or ignored root `.env`.
