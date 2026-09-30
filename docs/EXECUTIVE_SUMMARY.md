# Service Bus POC - Executive Summary

This repository contains two functionally different demonstrations presented through the same local dashboard:

1. **Service Bus POC** - asynchronous enterprise event publishing, broker-side routing, and independent consumers.
2. **Local POS processing** - a local receipt-processing and reporting flow backed by SQLite.

The Local POS flow is not another Service Bus subscription and does not participate in the `contact.events` routing path. Keeping these concerns separate is important when evaluating the application.

## Part 1: Service Bus POC

### Purpose

The Service Bus POC demonstrates Azure Service Bus as an enterprise messaging backbone for contact and product-related events. A producer publishes an event once, and multiple business capabilities receive it independently through topic subscriptions and broker-side filters.

The POC validates event contracts, message routing, independent consumers, and a separate Carwash verification API in a repeatable local environment. It is a demonstration rather than a production-sized deployment.

### Application components

| Component | Responsibility |
|---|---|
| Producer | Publishes event envelopes to the `contact.events` topic. |
| Digital Channels consumer | Receives every event. |
| Insurance consumer | Receives messages with `hasInsurance = true`. |
| Parks & Resorts consumer | Receives messages with `hasParksResorts = true`. |
| Carwash consumer | Receives messages with `hasCarwashProduct = true`. |
| Carwash verification API | Exposes `POST /carwash/v1/verify` for Pulse or mock Pulse. It is independent of the Carwash Service Bus consumer. |
| Verifier | Runs scenarios to prove that subscription filters route messages correctly. |
| Dashboard | Shows Service Bus service status and message activity and provides an event-publishing interface. |

### Event and routing flow

The POC models a `contact.events` topic with:

- `ContactUpdated` events for contact creation or changes.
- `ProductHoldingChange` events for product or holding lifecycle changes.

Events use a common `EventEnvelope` containing metadata such as event ID, type, source, timestamp, data version, and correlation ID. The JSON Schema files in [`contracts/`](../contracts/) are the canonical contract definitions.

The flow is:

1. A producer creates a valid event envelope.
2. The producer sends it to the `contact.events` topic.
3. Service Bus evaluates each subscription rule at the broker.
4. Digital Channels receives every event.
5. Insurance, Parks & Resorts, and Carwash receive only matching events.
6. Consumers process their subscriptions independently; they do not call one another.

The local topology is declared in [`infra/servicebus/config.json`](../infra/servicebus/config.json):

| Subscription | Rule | Consumer |
|---|---|---|
| `digital-channels` | No filter; receives all messages | Digital Channels |
| `insurance` | `hasInsurance = true` | Insurance |
| `parks-resorts` | `hasParksResorts = true` | Parks & Resorts |
| `carwash` | `hasCarwashProduct = true` | Carwash |

The Carwash HTTP path is separate from this flow: Pulse calls the verification endpoint directly and does not publish through, or depend on, the Carwash consumer.

### How Docker is used

Docker supplies a local substitute for the cloud Service Bus dependency. This allows the .NET applications and integration scenarios to run against a known topology without Azure credentials, an Azure subscription, or cloud messaging charges.

Running Docker Compose from [`infra/servicebus/`](../infra/servicebus/) starts two containers:

1. **Service Bus emulator** - provides the local Service Bus namespace, topic, subscriptions, filters, AMQP endpoint, and management/health-check endpoint. It loads [`config.json`](../infra/servicebus/config.json) through a bind mount.
2. **Azure SQL Edge** - provides the SQL dependency required internally by the emulator. The application does not connect to this database directly.

The containers share the `sb-emulator` network. The emulator reaches SQL Edge using the `sqledge` service alias. Host processes reach the emulator through these published ports:

| Host port | Purpose |
|---:|---|
| `5672` | AMQP endpoint used by the .NET Azure Service Bus SDK |
| `5300` | Emulator management/health-check HTTP endpoint; configurable with `EMULATOR_HTTP_PORT` |

The images are defined in [`infra/servicebus/compose.yaml`](../infra/servicebus/compose.yaml):

| Image | Why it is used |
|---|---|
| `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest` | Microsoft’s local Azure Service Bus emulator. It reproduces the namespace/topic/subscription behavior needed to validate publishing, consumption, and broker-side filters. |
| `mcr.microsoft.com/azure-sql-edge:latest` | Microsoft’s SQL Edge image, required as the emulator’s backing SQL service. It is an infrastructure dependency, not a business application. |

The emulator uses `pull_policy: always`, and both images currently use the `latest` tag. This is convenient for a POC but means local reproducibility depends on the image versions available when they are pulled; production tooling should pin tested versions.

Docker is **not** currently used to package the .NET applications or the browser UI. The scripts build and start the .NET projects as host processes, and the UI uses the Node/Vite toolchain. This is a dependency-emulator setup, not an all-in-one application container deployment.

### Local execution

[`scripts/run-local-poc.ps1`](../scripts/run-local-poc.ps1) checks prerequisites, starts the Compose stack, waits for the emulator, builds the .NET solution, sets emulator connection variables, starts the four consumers, runs the verifier, writes logs, and cleans up by default.

Typical setup and execution:

```powershell
Copy-Item infra/servicebus/.env.example infra/servicebus/.env
# Set SQL_PASSWORD and accept the emulator EULA in .env
docker compose -f infra/servicebus/compose.yaml up -d
.\scripts\run-local-poc.ps1
docker compose -f infra/servicebus/compose.yaml down
```

[`scripts/check-emulator-status.ps1`](../scripts/check-emulator-status.ps1) checks Docker, container state, port `5672`, and an AMQP protocol response.

### Service Bus limitations

The emulator is not a production Service Bus replacement. It has no Azure SLA, uses AMQP over TCP for this setup, and loses entities and messages when containers are recreated; `config.json` is reapplied on startup. The intended cloud target is Azure Service Bus provisioned through Bicep and protected with appropriate RBAC.

## Part 2: Local POS processing

### Purpose

Local POS processing is a separate dashboard demonstration of a point-of-sale receipt flow. It shows how a locally captured receipt can be represented as a POS event, persisted, and made available for downstream reporting or finance export.

Unlike Part 1, this flow:

- does not publish to the `contact.events` topic;
- does not use the Service Bus emulator or Service Bus subscriptions;
- does not use the Service Bus consumer applications; and
- uses the dashboard’s local SQLite database as its persistence boundary.

### Processing flow

The dashboard presents the following conceptual stages:

1. **POS Receipt** - a receipt is captured locally.
2. **`POS_RECEIPT_EVENT`** - the receipt is represented as a POS event.
3. **`POS_RECEIPT_LINE_ITEM`** - receipt line-item information is available for processing.
4. **Reporting / Finance export** - the event is ready for downstream reporting or export.

The flow is visualized by the `Local Processing` dashboard tab. The implementation is intentionally a local processing example; it does not claim to be a complete POS integration or finance system.

### Local storage and API

The Node.js dashboard server creates a SQLite database at `ui/data/pos-events.db` by default. The path can be changed with `POS_EVENTS_DB_PATH`.

The server creates a `PosEvents` table with:

- `id`
- `event_type`
- `receipt_number`
- `occurred_at`
- `status`
- `payload`

An example POS transaction is inserted idempotently on startup so the dashboard has a demonstrable record. The UI refreshes the data through `GET /api/pos-events` and displays the event type, receipt number, timestamp, status, transaction ID, item count, currency, and total.

The relevant implementation is in [`ui/server.js`](../ui/server.js), and the presentation is in [`ui/src/main.jsx`](../ui/src/main.jsx).

### Operational boundary

The POS database is local application state used to demonstrate processing and reporting readiness. It is separate from the Service Bus emulator containers and is not persisted by Docker. If the UI is run from the host, the SQLite file is stored on the host under `ui/data/`. A production POS design would need explicit decisions about ingestion, durability, schema evolution, reconciliation, security, and integration with reporting or finance platforms.

## Shared dashboard role

The React/Vite UI provides two views over these separate demonstrations:

- **Contact Events** - Service Bus emulator status, producer/consumer heartbeats, event publishing, and message activity.
- **Local Processing** - POS receipt processing backed by the local SQLite `PosEvents` table.

The shared dashboard is a presentation and local orchestration surface. Its presence does not make the two processing paths one system.

## Key takeaway

The repository demonstrates two complementary but independent capabilities. The Service Bus POC proves asynchronous enterprise event distribution and filtered subscription routing. Local POS processing proves a small, local receipt-to-reporting workflow using SQLite. Docker is used for the first capability’s local Service Bus dependency; it is not used as the POS data store and does not containerize the application UI or .NET services.
