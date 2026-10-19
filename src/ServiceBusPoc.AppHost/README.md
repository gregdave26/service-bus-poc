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

The host starts the four filtered consumers, Carwash's HTTP API and consumer
process, the producer, and the connectivity verifier. Existing project
configuration remains unchanged; Aspire only supplies the `ServiceBus:*`
environment variables and the consumer subscription names.

The producer and verifier are short-lived processes. The consumer processes
remain running until the Aspire application is stopped.
