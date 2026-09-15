# Debug & Startup Scripts - Summary

**Date Created:** 2026-09-15  
**Status:** ✅ Ready for use after Phase 1 implementation

---

## 📁 Scripts Created

Three production-ready PowerShell scripts have been created in the `scripts/` folder:

### 1. **debug-run.ps1** (11 KB)
**Purpose:** Start the entire application in debug mode for local development

**Key Features:**
- Starts all 7 applications as background jobs
- Real-time log streaming to console
- Structured logs saved to file
- Hot-reload ready for development
- Graceful shutdown on Ctrl+C
- Prerequisites validation (.NET, Docker)

**Usage:**
```powershell
# Start all applications
.\scripts\debug-run.ps1

# Start specific roles only
.\scripts\debug-run.ps1 -Role insurance,carwash

# Skip emulator (use external Service Bus)
.\scripts\debug-run.ps1 -Emulator $false

# Custom log location
.\scripts\debug-run.ps1 -LogPath "./my-logs/debug.log"
```

**Output:**
- Console: Real-time logs with color coding
- File: `logs/debug-run-{timestamp}.log`
- Keeps running until Ctrl+C pressed

---

### 2. **run-local-poc.ps1** (9.3 KB)
**Purpose:** Run complete end-to-end POC with automatic scenario validation

**Key Features:**
- Orchestrated multi-step execution
- Automatic scenario verification
- Structured test results
- CI/CD-friendly output
- Auto-cleanup of resources
- Progress reporting at each step

**Usage:**
```powershell
# Run complete POC (30 second setup)
.\scripts\run-local-poc.ps1

# Longer setup time, keep services running
.\scripts\run-local-poc.ps1 -RunTime 60 -NoCleanup

# Verbose logging (debug output)
.\scripts\run-local-poc.ps1 -Verbose
```

**Output:**
- Console: Step-by-step progress
- File: `logs/poc-run-{timestamp}.log` (full logs)
- File: `logs/poc-report-{timestamp}.txt` (summary)
- Auto-cleanup (unless -NoCleanup specified)

---

### 3. **scripts/README.md** (6.4 KB)
**Purpose:** Complete documentation for all scripts

**Contents:**
- Prerequisites checklist
- Detailed usage examples
- Environment variable reference
- Log file locations
- Troubleshooting guide
- Performance optimization tips
- Development workflow recommendations

**Start here when running scripts!**

---

## 🚀 Quick Reference

| Script | Use For | Command |
|--------|---------|---------|
| **debug-run.ps1** | Local development | `.\scripts\debug-run.ps1` |
| **run-local-poc.ps1** | Testing/CI/CD | `.\scripts\run-local-poc.ps1` |

---

## 🔍 What These Scripts Do

### debug-run.ps1 Workflow
```
Prerequisites Check (Docker, .NET, projects exist)
         ↓
Start Azure Service Bus Emulator
         ↓
Build Solution (dotnet build)
         ↓
Set Environment Variables
         ↓
Start 7 Applications (as background jobs)
├─ ServiceBusPoc.DigitalChannels
├─ ServiceBusPoc.Insurance
├─ ServiceBusPoc.ParksResorts
├─ ServiceBusPoc.Carwash
├─ ServiceBusPoc.Producer
└─ ServiceBusPoc.Verifier
         ↓
Stream Real-Time Logs to Console
         ↓
Wait for Ctrl+C (Graceful Shutdown)
         ↓
Stop All Jobs & Emulator
```

### run-local-poc.ps1 Workflow
```
Prerequisites Check
      ↓
Step 1: Start Emulator
      ↓
Step 2: Build Solution
      ↓
Step 3: Configure Environment
      ↓
Step 4: Start Consumers (4 apps)
      ↓
Step 5: Run Verification/Scenarios
      ↓
Step 6: Collect Results → Report
      ↓
Step 7: Cleanup (optional)
```

---

## 💻 System Requirements

- **PowerShell 7+** (supports Windows, macOS, Linux)
- **.NET 10 SDK** (`dotnet --version` should show 10.x)
- **Docker Desktop** (for Azure Service Bus emulator)
- **4 GB free disk space** (for emulator container)

---

## 📊 Feature Matrix

| Feature | debug-run.ps1 | run-local-poc.ps1 |
|---------|---------------|-------------------|
| Prerequisites Check | ✅ | ✅ |
| Build Solution | ✅ | ✅ |
| Start Emulator | ✅ | ✅ |
| Start Consumers | ✅ | ✅ |
| Start Producer | ✅ | ✅ (via verification) |
| Real-time Logs | ✅ | ✅ (buffered) |
| Scenario Validation | ✅ (Verifier) | ✅ (Verifier) |
| Structured Output | ✅ (log file) | ✅ (report file) |
| Auto Cleanup | ❌ | ✅ |
| Exit Codes | ❌ | ✅ (for CI/CD) |
| Verbose Mode | ⏳ | ✅ |

---

## 📝 Log Files

Both scripts save logs to `logs/` folder:

```
logs/
├── debug-run-20260915-154201.log
│   └─ Output from debug-run.ps1 execution
│   
├── poc-run-20260915-154300.log
│   └─ Full logs from run-local-poc.ps1
│   
└── poc-report-20260915-154300.txt
    └─ Test results summary (human-readable)
```

**Viewing logs:**
```powershell
# View most recent log
Get-Content logs/debug-run-*.log -Wait

# View report summary
Get-Content logs/poc-report-*.txt

# View all logs from today
Get-ChildItem logs/ | Where-Object { $_.LastWriteTime -gt (Get-Date).AddHours(-24) }
```

---

## ⚙️ Environment Variables (Auto-Set by Scripts)

```powershell
# Service Bus Configuration
$env:ServiceBusConnectionString = "Endpoint=sb://localhost:5672/..."
$env:ServiceBusTopicName = "contact.events"

# Logging
$env:DOTNET_LOG_LEVEL = "Information"      # or "Debug" for verbose
$env:DOTNET_Environment = "Development"

# Carwash API
$env:CarwashApiPort = "5000"
```

Both scripts set these automatically. You can override before running:
```powershell
$env:DOTNET_LOG_LEVEL = "Debug"
.\scripts\debug-run.ps1
```

---

## 🎯 Common Use Cases

### 1. Active Development Loop
```powershell
# Start all apps in debug mode
.\scripts\debug-run.ps1

# In another terminal, make code changes
# Code changes auto-reload (when hot-reload is configured)

# Press Ctrl+C to shutdown
```

### 2. PR Validation Before Submission
```powershell
# Run complete POC validation
.\scripts\run-local-poc.ps1

# Review report (logs/poc-report-*.txt)
# If all scenarios pass → Ready to open PR
```

### 3. Focused Testing of Single Component
```powershell
# Test only Insurance consumer
.\scripts\debug-run.ps1 -Role insurance

# Or test multiple components
.\scripts\debug-run.ps1 -Role insurance,carwash
```

### 4. CI/CD Pipeline Integration
```powershell
# In CI/CD workflow
.\scripts\run-local-poc.ps1 -Verbose

# Check exit code
if ($LASTEXITCODE -eq 0) {
    Write-Host "All scenarios passed"
}
else {
    Write-Host "Verification failed"
    exit 1
}
```

---

## 🔧 Customization

### Modify Emulator Connection
Edit `debug-run.ps1` or `run-local-poc.ps1` line ~70:
```powershell
$env:ServiceBusConnectionString = "Endpoint=sb://your-endpoint/..."
```

### Add New Application
In either script, add to the app list:
```powershell
$apps = @(
    # ... existing apps
    @{ Name = 'YourApp'; Project = 'ServiceBusPoc.YourApp' }
)
```

### Change Emulator Startup Time
Adjust the `-WaitSeconds` parameter:
```powershell
.\scripts\debug-run.ps1 -WaitSeconds 30   # Wait 30 seconds
```

---

## ⚠️ Important Notes

### These Scripts Require Phase 1 to Be Complete
The scripts expect:
- ✅ .NET 10 solution (`ServiceBusPoc.sln`)
- ✅ 7 projects in `src/` folder
- ✅ Docker Compose file at `infra/servicebus/compose.yaml`
- ✅ Configuration classes in place

**Status:** Phase 1 is in planning. After Phase 1 is implemented, these scripts become immediately usable.

### Docker Compose File Required
Ensure `infra/servicebus/compose.yaml` exists with emulator topology.

### Service Bus Emulator Limitations
- Single instance only (not HA)
- TCP only (no WebSocket)
- Local testing only
- Data not persisted across restarts

---

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| "Docker not found" | Install Docker Desktop, ensure it's running |
| ".NET SDK not found" | Install .NET 10 SDK |
| "Build failed" | Check `dotnet build` output; review project files |
| "Solution file not found" | Run from project root |
| "Applications start but don't connect" | Verify emulator is running: `docker ps` |
| "Slow startup (>2 min)" | Increase `-WaitSeconds` parameter |
| "Port already in use" | Check for existing containers: `docker ps` |

**More help:** See `scripts/README.md`

---

## 📚 Related Documentation

- [scripts/README.md](./README.md) — Detailed usage guide
- [DEVELOPER.md](../DEVELOPER.md) — Development workflow (forthcoming)
- [PHASE_1_PLAN.md](../PHASE_1_PLAN.md) — Current phase details
- [docs/architecture/project-goal.md](../docs/architecture/project-goal.md) — System architecture

---

## 📅 Timeline

| Phase | Status | Scripts Ready? |
|-------|--------|----------------|
| Phase 1 | 🟡 Planning | ✅ Yes (templates) |
| Phase 2 | ⏳ Next | ✅ Ready to use |
| Phase 3 | ⏳ Future | ✅ Ready to use |
| Phase 4+ | ⏳ Future | 🔄 May need updates for cloud |

---

## ✅ Verification Checklist

Before using scripts, confirm:

- [ ] Project is at `c:\Users\dg15938\development\service-bus-poc`
- [ ] PowerShell 7+ installed (`pwsh --version`)
- [ ] .NET 10 SDK installed (`dotnet --version`)
- [ ] Docker Desktop installed and running
- [ ] At least 4 GB free disk space
- [ ] Phase 1 implementation complete (7 projects exist)
- [ ] Read `scripts/README.md` for parameters

---

**Created:** 2026-09-15  
**Status:** ✅ Production-ready templates  
**Maintained By:** Service Bus POC Team
