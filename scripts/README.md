# Service Bus POC - Scripts

This folder contains the supported PowerShell entry points for running, testing, and
debugging the Service Bus POC application. Run them from the repository root or from
the `scripts` directory; each script resolves paths relative to its own location.

## Prerequisites

- PowerShell 7+ (cross-platform)
- .NET 10 SDK
- Node.js 20+
- Docker Desktop (for emulator)
- 4 GB free disk space (for emulator container)
- For emulator checks, Docker Desktop must be running with Linux containers and the
  emulator EULA accepted in `infra/servicebus/.env`.
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

Checks Docker before starting the compose services, then runs the Verifier's SDK
probe. The probe creates a sender and message batch without publishing a message.
It uses no temporary project, downloaded package, or generated source file.

### `check-emulator-status.ps1` — Diagnostics

Reports Docker, container, TCP, AMQP, and recent log status. It is diagnostic only
and does not prove that the configured topic topology is correct.

### `wait-for-servicebus-emulator.ps1` — Readiness Helper

Defines `Wait-ServiceBusEmulatorReady` for scripts that need to wait for the AMQP
handshake. Dot-source it from another script; it does not start or stop containers.

### `producer-smoke-test.ps1` — Producer Smoke Test

Builds and runs the producer against a configured endpoint and publishes sample
messages. This is separate from the non-publishing SDK connectivity probe.

### Other supported lifecycle scripts

`run-local-poc.ps1`, `run-dashboard.ps1`, `debug-run.ps1`, `test-carwash-api.ps1`,
and `stop-service-bus-poc.ps1` cover application lifecycle, dashboard, API, and
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

---

### 2. `debug-run.ps1` — Debug Start (for Development Without Dashboard)

**Purpose:** Start the entire POC in debug mode with all 7 applications running locally.

**Use this when:**
- You're actively developing and want hot-reload capability
- You need to debug a single consumer or producer
- You want to see real-time logs from all applications
- You prefer console-based testing over the browser dashboard

**Usage:**
```powershell
# Start everything in debug mode
.\debug-run.ps1

# Start only specific roles (Insurance consumer + DigitalChannels)
.\debug-run.ps1 -Role insurance,digitalchannels

# Start without emulator (assume external Service Bus)
.\debug-run.ps1 -Emulator $false

# Save logs to custom location
.\debug-run.ps1 -LogPath "./my-logs/debug.log"
```

**Output:**
- Real-time logs from all applications printed to console
- Structured logs saved to `logs/debug-run-{timestamp}.log`
- All 7 apps running as PowerShell background jobs
- Press `Ctrl+C` to gracefully shutdown all

**Applications Started:**
1. **DigitalChannels** — Receives all events (no filter)
2. **Insurance** — Receives events with `hasInsurance=true`
3. **ParksResorts** — Receives events with `hasParksResorts=true`
4. **Carwash** — Receives events with `hasCarwashProduct=true`
5. **Producer** — Publishes contact events (manual trigger)
6. **Verifier** — Scenario validator

---

### 3. `run-local-poc.ps1` — Local POC Run (for Verification & CI/CD)

**Purpose:** Run the complete end-to-end POC with automatic scenario verification.

**Use this when:**
- You want to validate the entire system in one command
- You're preparing a PR and need to verify all scenarios
- You want CI/CD-friendly output
- You need to generate test reports

**Usage:**
```powershell
# Run complete POC with default settings (30 sec run time)
.\run-local-poc.ps1

# Run with longer setup time (60 seconds) and keep services running
.\run-local-poc.ps1 -RunTime 60 -NoCleanup

# Run with verbose logging (shows debug output)
.\run-local-poc.ps1 -Verbose
```

**Output:**
- Step-by-step progress (emulator start → build → verify)
- Verification report: `logs/poc-report-{timestamp}.txt`
- Full execution logs: `logs/poc-run-{timestamp}.log`
- Structured results for CI/CD parsing
- Auto cleanup (containers stopped, processes killed)

**Flow:**
1. Start Azure Service Bus emulator
2. Build entire solution
3. Start all 4 consumer applications
4. Run Verifier with test scenarios
5. Collect results and generate report
6. Cleanup (optional: keep running with `-NoCleanup`)

---

## Environment Variables

Both scripts set these automatically, but you can override:

```powershell
# Service Bus configuration
$env:ServiceBus__ConnectionString = "Endpoint=sb://localhost:5672/..."
$env:ServiceBus__TopicName = "contact.events"
$env:ServiceBus__Namespace = "localhost"
$env:ServiceBus__SubscriptionName = "insurance" # Consumer-specific

# Dashboard configuration (run-dashboard.ps1 only)
$env:Dashboard__Enabled = "true"
$env:Dashboard__Url = "http://localhost:5100"
$env:Dashboard__Port = "5100"
$env:Dashboard__HeartbeatIntervalSeconds = "5"

# Logging
$env:DOTNET_LOG_LEVEL = "Information"  # or "Debug" for verbose
$env:DOTNET_Environment = "Development"

# Carwash API
$env:Carwash__ApiPort = "5000"
```

---

## Logs

All scripts save logs to `logs/` folder:

```
logs/
├── run-dashboard-20260916-155500.log   # From run-dashboard.ps1
├── debug-run-20260915-154201.log       # From debug-run.ps1
├── poc-run-20260915-154300.log         # From run-local-poc.ps1
├── poc-report-20260915-154300.txt      # Test results summary
└── emulator-20260915-154300.log        # Docker Compose output (if saved)
```

**View logs in real-time:**
```powershell
# PowerShell tail equivalent
Get-Content -Path logs/run-dashboard-*.log -Wait
```

---

## Troubleshooting

### "Docker not found or not running"
→ Start Docker Desktop and retry

### ".NET SDK not found"
→ Install .NET 10 SDK from https://dotnet.microsoft.com/download

### "Solution file not found"
→ Run from project root: `cd C:\Users\dg15938\development\service-bus-poc`

### "Build failed"
→ Check `dotnet build` output in console; ensure all projects exist

### Applications start but don't receive messages
→ Verify Service Bus emulator is running: `docker-compose ps`
→ Check environment variables are set: `$env:ServiceBus__ConnectionString`
### Dashboard not opening in browser
→ Manually navigate to `http://localhost:5100` or `http://localhost:{DashboardPort}`
→ Check firewall is not blocking port 5100

### Emulator times out or doesn't start
→ Ensure Docker Desktop is running
→ Check disk space: `docker system df`
→ Cleanup old containers: `docker system prune`

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

### For Local Console-Based Testing:
```powershell
# Terminal 1: Start debug session (keeps running)
.\debug-run.ps1

# Terminal 2: Run producer to emit test events
# (trigger via PowerShell or REST API)
```

### For CI/CD Validation:
```powershell
# Single command validates entire system
.\run-local-poc.ps1

# Exit code: 0 = all scenarios passed, non-zero = failure
$exitCode = $LASTEXITCODE
```

### For Specific Role Testing:
```powershell
# Debug only Insurance consumer
.\debug-run.ps1 -Role insurance

# or start multiple specific roles
.\debug-run.ps1 -Role insurance,carwash
```

---

## Performance Optimization

| Setting | Impact | Usage |
|---------|--------|-------|
| `run-dashboard.ps1` (default) | Fast interactive testing | Best for development |
| `-RunTime 60` | Longer setup (slower machines) | `run-local-poc.ps1 -RunTime 60` |
| `-Verbose` | More logging (slower) | `debug-run.ps1 -Verbose` |
| Single role | Faster startup | `debug-run.ps1 -Role insurance` |
| `-NoCleanup` | Keep services running | Skip restart time for multiple runs |
| `-NoEmulator` | Use external Service Bus | `run-dashboard.ps1 -NoEmulator` |

---

## Comparison: Which Script to Use?

| Goal | Script | Start Time | Best For |
|------|--------|-----------|----------|
| Interactive testing & demos | `run-dashboard.ps1` | 30 sec | Development, demos, manual testing |
| Console debugging | `debug-run.ps1` | 30 sec | Debugging, specific role testing |
| Automated validation (CI/CD) | `run-local-poc.ps1` | 60 sec | PR validation, automated testing |

---

## Next Steps

1. **Start Interactive:** Run `.\run-dashboard.ps1` to see live dashboard
2. **Manual Testing:** Publish events and watch filters work in real-time
3. **Automated Validation:** Run `.\run-local-poc.ps1` in CI/CD for regression tests
4. **Debugging:** Use `.\debug-run.ps1 -Verbose` for detailed troubleshooting

---

## Additional Resources

- [DEVELOPER.md](../DEVELOPER.md) — Development guide (forthcoming)
- [docs/architecture/project-goal.md](../docs/architecture/project-goal.md) — System architecture
- [IMPLEMENTATION_PLAN.md](../IMPLEMENTATION_PLAN.md) — Full timeline and phases
- [docs/decisions/010-browser-dashboard.md](../docs/decisions/010-browser-dashboard.md) — Dashboard design decision

---

**Last Updated:** 2026-09-16  
**Maintained By:** Service Bus POC Team
