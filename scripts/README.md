# Service Bus POC - Scripts

This folder contains the supported PowerShell entry points for running, testing, and
debugging the Service Bus POC application. Run them from the repository root or from
the `scripts` directory; each script resolves paths relative to its own location.

## Prerequisites

- PowerShell 7+ (cross-platform)
- .NET 10 SDK
- Node.js 20+
- Podman in WSL (default), or Docker Engine with the Compose v2 plugin in WSL when
  `CONTAINER_RUNTIME=docker` is set in the shell. `CONTAINER_WSL_DISTRO` selects the
  WSL distribution (default `Ubuntu`).
- 4 GB free disk space (for emulator container)
- For emulator checks, the selected runtime must be running in WSL with Linux containers
  and the emulator EULA accepted in the repository-root `.env`.
- SDK-based checks use the local emulator connection string by default. Set
  `ServiceBus__ConnectionString` to override it; no credentials are committed.

## Available Scripts

### `test-all.ps1` — All Automated Tests

**Purpose:** Run every xUnit test in the repository.

**Usage:**
```powershell
.\test-all.ps1
```

This runs the complete test project without coverage collection. To run the same
suite with coverage enforcement, use `test-coverage.ps1`:

```powershell
.\test-coverage.ps1
```

### `test-coverage.ps1` — Tests with Coverage Enforcement

Runs the unit tests, stops immediately when `dotnet test` fails, and then requires
a Cobertura report with at least 80% line coverage.

### `validate-emulator-topology.ps1` — Emulator Topology Validation

Checks the emulator management endpoint, then runs the repository Verifier with
the Azure Service Bus administration SDK. It validates `contact.events`, all four
subscriptions, and their SQL filters. It exits nonzero when the emulator is
unavailable, an entity is missing, or a filter is wrong.

### `test-emulator-connectivity.ps1` — SDK Connectivity Probe

Checks the container runtime before starting the compose services, then runs the Verifier's SDK
probe. The probe creates a sender and message batch without publishing a message.
It uses no temporary project, downloaded package, or generated source file.

### `test-adyen-connectivity.ps1` — Adyen Checkout Connectivity Probe

Tests the configured Adyen Checkout `/sessions` endpoint without starting the
Commerce API. It loads the repository-root `.env` by default and sends a
one-cent synthetic session request using either `Adyen__ApiKey` or the optional
APIM subscription-key settings. It reports the HTTP status and response body
without printing credential values.

```powershell
.\test-adyen-connectivity.ps1
```

The script exits with code `0` for a successful session response and `1` when
Adyen rejects the request or cannot be reached. A successful probe creates only
a short-lived Checkout session; it does not authorise a payment.

`run-dashboard.ps1` automatically runs this probe before starting services when
`DigitalSite__PaymentGateway` is `Adyen` or `AdyenComponent`. The environment,
merchant account, endpoint, authentication method, and HTTP result are written
to the console and the run-dashboard transcript under `logs/`; credential values
are not printed. A failed probe is reported but does not prevent the local stack
from starting, because CommerceApi performs its own Stub fallback.

### `check-emulator-status.ps1` — Diagnostics

Reports runtime, container, TCP, AMQP, and recent log status. It is diagnostic only
and does not prove that the configured topic topology is correct.

### `wait-for-servicebus-emulator.ps1` — Readiness Helper

Defines `Wait-ServiceBusEmulatorReady` for scripts that need to wait for the AMQP
handshake. Dot-source it from another script; it does not start or stop containers.

### `producer-smoke-test.ps1` — Producer Smoke Test

Builds and runs the producer against a configured endpoint and publishes sample
messages. This is separate from the non-publishing SDK connectivity probe.

### Other supported lifecycle scripts

`run-dashboard.ps1`, `run-aspire.ps1`, `test-carwash-api.ps1`, and
`stop-service-bus-poc.ps1` cover application lifecycle, dashboard, API, and
cleanup workflows. Read each script's help before use; lifecycle scripts can start
processes or containers and may write to `logs/`.

## Archived scripts

The following legacy or duplicate experiments are preserved under `scripts/archive`
and are not part of the supported workflow:

- `test-emulator-startup.ps1`
- `measure-emulator-readiness.ps1`
- `test-simple.ps1`
- `test-ctrlc.ps1`
- `CTRLC_TEST.ps1`

They may contain obsolete assumptions and should not be used for validation.

### 1. `run-dashboard.ps1` — Interactive Dashboard + Services (Recommended for Development)

**Purpose:** Start the Dashboard with all services for interactive development and testing.

If you choose to stop an existing instance at startup, processes that have already
exited are skipped; other process-stop errors are reported rather than ignored.

**Use this when:**
- You want to see real-time service status and test filters interactively
- You're demonstrating the system or testing manually
- You want to publish events and watch consumers log them in real-time
- You need the browser-based dashboard for visibility

**Usage:**
```powershell
# Start everything with dashboard
.\run-dashboard.ps1

# Start without auto-opening browser
.\run-dashboard.ps1 -NoBrowser

# Use custom dashboard port
.\run-dashboard.ps1 -DashboardPort 8080

# Skip emulator (assume it's already running)
.\run-dashboard.ps1 -NoEmulator
```

The emulator startup reuses existing containers and local images. It does not
pull the `latest` images on every run. To explicitly refresh the images, open a WSL
shell in the repository's `infra/servicebus` folder and run:

```bash
PODMAN_COMPOSE_PROVIDER=podman-compose podman compose --env-file ../../.env pull
PODMAN_COMPOSE_PROVIDER=podman-compose podman compose --env-file ../../.env up -d
```

With Docker, run `docker compose` with the same arguments, without the `PODMAN_COMPOSE_PROVIDER` prefix.

Pressing `Ctrl+C` in `run-dashboard.ps1` still stops and removes the emulator
containers as part of normal cleanup.

**Output:**
- 🟢 Dashboard HTTP server on http://localhost:5100
- 🔵 Producer publishing sample events every 3 seconds
- 🟢 All 4 consumers listening and logging filtered messages
- Browser opens automatically to dashboard
- Press `Ctrl+C` to gracefully shutdown all

**Services Started:**
1. **Dashboard** — Node.js real-time status & event publishing UI (http://localhost:5100)
2. **Producer** — Publishes sample events every 3 seconds
3. **DigitalChannels** — Receives all events (no filter)
4. **Insurance** — Receives `hasInsurance=true`
5. **ParksResorts** — Receives `hasParksResorts=true`
6. **Carwash** — Receives `hasCarwashProduct=true`
7. **CommerceToolsStub** — commercetools HTTP API subset on http://localhost:5201; publishes CT messages to `commerce.events`
8. **CommerceApi** — Digital Site backend-for-frontend and Adyen webhook on http://localhost:5200
9. **CartProcessor** — `commerce.events`/`cart-processor`: creates orders from authorised payments, publishes `ProductHoldingChange`
10. **FulfilmentStub** — `commerce.events`/`fulfilment-d365-stub`: logs D365 provisioning and completes the order

See [ADR-014](../docs/decisions/014-digital-site-commerce-flow.md) and [`ui/README.md`](../ui/README.md#digital-site-tab).

---

### 2. `validate-emulator-topology.ps1` — Routing Verification (for CI/CD and PR Checks)

**Purpose:** Check the running emulator's topology and run all 8 contact-event routing scenarios through the Verifier.

**Use this when:**
- You changed the topology in `infra/servicebus/config.json` or subscription filters
- You're preparing a PR that touches routing or topology
- You need a pass/fail result that CI can read

**Usage:**
```powershell
# Confirm the emulator is running first
.\check-emulator-status.ps1

.\validate-emulator-topology.ps1
```

Exit code 0 means the topology and all routing scenarios passed. Any failure exits non-zero.

---

## Environment Variables

The launchers set these automatically, but you can override:

```powershell
# Service Bus configuration
$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost:5672/..."
$env:ServiceBus__TopicName = "contact.events"
$env:ServiceBus__Namespace = "sbemulatorns"
$env:ServiceBus__SubscriptionName = "insurance" # Consumer-specific

# Dashboard configuration (run-dashboard.ps1 only)
$env:Dashboard__Enabled = "true"
$env:Dashboard__Url = "http://localhost:5100"
$env:Dashboard__Port = "5100"
$env:Dashboard__HeartbeatIntervalSeconds = "5"

# Logging
$env:DOTNET_LOG_LEVEL = "Information"  # or "Debug" for verbose
$env:DOTNET_ENVIRONMENT = "Development"  # Required for the optional CommerceApi Adyen fallback; the launcher no longer sets it
$env:Carwash__ApiPort = "5000"

# Digital Site (run-dashboard.ps1 only; each app also gets its own ServiceBus__TopicName/SubscriptionName)
$env:CommerceTools__ApiUrl = "http://localhost:5201"
$env:CommerceTools__ProjectKey = "rac-rsa-poc"
$env:DIGITAL_SITE_API_BASE_URL = "http://localhost:5200"
# Set these yourself (never commit them) to use Adyen instead of the stub gateway:
# $env:DigitalSite__PaymentGateway = "Adyen"; $env:Adyen__MerchantAccount, Adyen__ApiKey, Adyen__ClientKey, Adyen__HmacKey
```

---

## Logs

All scripts save logs to `logs/` folder:

```
logs/
├── run-dashboard-20260916-155500.log   # From run-dashboard.ps1
└── emulator-20260915-154300.log        # Compose output (if saved)
```

**View logs in real-time:**
```powershell
# PowerShell tail equivalent
Get-Content -Path logs/run-dashboard-*.log -Wait
```

---

## Troubleshooting

### "Container runtime not found or not running"
→ Start Podman (or Docker, with `CONTAINER_RUNTIME=docker`) in WSL and retry
→ Confirm the WSL distribution: `CONTAINER_WSL_DISTRO` (default `Ubuntu`)

### ".NET SDK not found"
→ Install .NET 10 SDK from https://dotnet.microsoft.com/download

### "Solution file not found"
→ Run from project root: `cd C:\Users\dg15938\development\service-bus-poc`

### "Build failed"
→ Check `dotnet build` output in console; ensure all projects exist

### Applications start but don't receive messages
→ Verify Service Bus emulator is running: `.\scripts\check-emulator-status.ps1`
→ Check environment variables are set: `$env:ServiceBus__ConnectionString`
### Dashboard not opening in browser
→ Manually navigate to `http://localhost:5100` or `http://localhost:{DashboardPort}`
→ Check firewall is not blocking port 5100

### Emulator times out or doesn't start
→ Ensure the container runtime is running in WSL
→ Check disk space: `podman system df` (or `docker system df`)
→ Cleanup old containers: `podman system prune` (or `docker system prune`)

---

## Development Workflow

### For Interactive Testing with Dashboard (Recommended):
```powershell
# Single command starts everything with browser UI
.\run-dashboard.ps1

# Open dashboard in browser and:
# 1. Watch service status update in real-time
# 2. Click "Publish Event" and toggle capability flags
# 3. Watch consumer consoles log only matching messages
# 4. Press Ctrl+C here to stop all services
```

### For Routing Verification (CI/CD and PRs):
```powershell
# Requires the emulator to be running
.\validate-emulator-topology.ps1

# Exit code: 0 = topology and all 8 routing scenarios passed, non-zero = failure
$exitCode = $LASTEXITCODE
```

---

## Performance Optimization

| Setting | Impact | Usage |
|---------|--------|-------|
| `run-dashboard.ps1` (default) | Fast interactive testing | Best for development |
| `-NoEmulator` | Use external Service Bus | `run-dashboard.ps1 -NoEmulator` |

---

## Comparison: Which Script to Use?

| Goal | Script | Start Time | Best For |
|------|--------|-----------|----------|
| Interactive testing & demos | `run-dashboard.ps1` | 30 sec | Development, demos, manual testing |
| Routing verification (CI/PR) | `validate-emulator-topology.ps1` | Emulator running | Topology and all 8 routing scenarios |

---

## Next Steps

1. **Start Interactive:** Run `.\run-dashboard.ps1` to see live dashboard
2. **Manual Testing:** Publish events and watch filters work in real-time
3. **Routing Verification:** Run `.\validate-emulator-topology.ps1` in CI/CD or before merging routing changes
4. **Debugging:** Use the per-service logs from `run-dashboard.ps1`

---

## Additional Resources

- [DEVELOPER.md](../DEVELOPER.md) — Development guide (forthcoming)
- [docs/architecture/project-goal.md](../docs/architecture/project-goal.md) — System architecture
- [IMPLEMENTATION_PLAN.md](../IMPLEMENTATION_PLAN.md) — Full timeline and phases
- [docs/decisions/010-browser-dashboard.md](../docs/decisions/010-browser-dashboard.md) — Dashboard design decision

---

**Last Updated:** 2026-09-16  
**Maintained By:** Service Bus POC Team
