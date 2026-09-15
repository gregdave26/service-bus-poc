# Service Bus POC - Scripts

This folder contains PowerShell scripts for running and debugging the Service Bus POC application.

## Prerequisites

- PowerShell 7+ (cross-platform)
- .NET 10 SDK
- Docker Desktop (for emulator)
- 4 GB free disk space (for emulator container)

## Available Scripts

### 1. `debug-run.ps1` — Debug Start (Recommended for Development)

**Purpose:** Start the entire POC in debug mode with all 7 applications running locally.

**Use this when:**
- You're actively developing and want hot-reload capability
- You need to debug a single consumer or producer
- You want to see real-time logs from all applications

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

### 2. `run-local-poc.ps1` — Local POC Run (Recommended for Testing)

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
$env:ServiceBusConnectionString = "Endpoint=sb://localhost:5672/..."
$env:ServiceBusTopicName = "contact.events"

# Logging
$env:DOTNET_LOG_LEVEL = "Information"  # or "Debug" for verbose
$env:DOTNET_Environment = "Development"

# Carwash API
$env:CarwashApiPort = "5000"
```

---

## Logs

All scripts save logs to `logs/` folder:

```
logs/
├── debug-run-20260915-154201.log          # From debug-run.ps1
├── poc-run-20260915-154300.log            # From run-local-poc.ps1
├── poc-report-20260915-154300.txt         # Test results summary
└── emulator-20260915-154300.log           # Docker Compose output (if saved)
```

**View logs in real-time:**
```powershell
# PowerShell tail equivalent
Get-Content -Path logs/debug-run-*.log -Wait
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
→ Check `dotnet build` output in console; ensure all projects exist after Phase 1

### Applications start but don't receive messages
→ Verify Service Bus emulator is running: `docker-compose ps`
→ Check environment variables are set: `$env:ServiceBusConnectionString`

### Emulator times out or doesn't start
→ Ensure Docker Desktop is running
→ Check disk space: `docker system df`
→ Cleanup old containers: `docker system prune`

---

## Development Workflow

### For Local Testing:
```powershell
# Terminal 1: Start debug session (keeps running)
.\debug-run.ps1

# Terminal 2: Run producer to emit test events
# (trigger via API once Carwash API is implemented)
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
| `-RunTime 60` | Longer setup (slower machines) | `run-local-poc.ps1 -RunTime 60` |
| `-Verbose $true` | More logging (slower) | `debug-run.ps1 -Verbose` |
| Single role | Faster startup | `debug-run.ps1 -Role insurance` |
| `-NoCleanup` | Keep services running | Skip restart time for multiple runs |

---

## Next Steps

1. **Phase 1 Complete:** Run `.\debug-run.ps1` to verify all 7 projects build and start
2. **Phase 2 Started:** Use `.\debug-run.ps1 -Role producer` to test producer implementation
3. **Phase 3 Tests:** Use `.\run-local-poc.ps1` in CI/CD for automated scenario validation
4. **Phase 4+ Cloud:** Scripts can be adapted for real Azure Service Bus (update connection string)

---

## Additional Resources

- [DEVELOPER.md](../DEVELOPER.md) — Development guide (forthcoming)
- [docs/architecture/project-goal.md](../docs/architecture/project-goal.md) — System architecture
- [PHASE_1_PLAN.md](../PHASE_1_PLAN.md) — Current phase details
- [IMPLEMENTATION_PLAN.md](../IMPLEMENTATION_PLAN.md) — Full timeline and phases

---

**Last Updated:** 2026-09-15  
**Maintained By:** Service Bus POC Team
