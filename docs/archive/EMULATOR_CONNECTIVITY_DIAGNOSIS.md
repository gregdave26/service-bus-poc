# Service Bus Emulator AMQP Connection Failure - Diagnosis Report

**Date:** 2026-09-17  
**Status:** ⚠️ **BLOCKED** - Service Bus emulator not accepting AMQP connections  
**Error:** `No connection could be made because the target machine actively refused it` (Error 10061)

---

## Executive Summary

The Producer service cannot connect to the Service Bus emulator on port 5672 (AMQP). The connection is being actively refused by the target machine, indicating **the emulator is either not running or has not yet bound to the AMQP port**. Current wait time (15 seconds) is insufficient for emulator initialization.

---

## Investigation Findings

### ✅ Configuration Status (CORRECT)

| Component | Status | Details |
|-----------|--------|---------|
| `.env` file | ✅ Present | Located: `infra/servicebus/.env` |
| ACCEPT_EULA | ✅ Y | Correctly set in .env |
| SQL_PASSWORD | ✅ Configured | `ComplexPassword123!` (meets requirements) |
| SQL_WAIT_INTERVAL | ✅ 15 seconds | Set in .env |
| EMULATOR_HTTP_PORT | ✅ 5300 | Set in .env |
| Connection String | ✅ Correct | `Endpoint=sb://localhost:5672/;...` (line 182 of run-dashboard.ps1) |
| compose.yaml | ✅ Valid | Port mapping: `5672:5672` (line 10) |
| config.json | ✅ Valid | Topic `contact.events` and 4 subscriptions configured |

---

## Root Cause Analysis

### Problem: Connection Refused on Port 5672

**Evidence:**
1. **Error:** `System.Net.Sockets.SocketException (10061)` - "No connection could be made"
2. **Timing:** Connection attempt fails after ~27.8 seconds of waiting
3. **Location:** Producer-stdout.log, 2026-09-17T10:51:00 (28 seconds after startup)
4. **Source Code:** `ContactEventPublisher.PublishContactUpdatedAsync()` → `_sender.SendMessageAsync()`

**Stack trace location:** Failure at AMQP transport layer during initial connection attempt:
```
at Microsoft.Azure.Amqp.Transport.AmqpTransportInitiator.ConnectAsyncResult.End()
at Azure.Messaging.ServiceBus.Amqp.AmqpConnectionScope.CreateAndOpenConnectionAsync()
```

### Two Possible Causes

#### 1. **Docker/Containers Not Running** (Most Likely)

**Indicators:**
- Previous command failure: `docker version` failed with access/permission error
- Message: `BaseContainer is unavailable; DACL fallback requires write-DAC permission`
- Docker service may not be running or accessible to current user

**Impact:**
- `docker-compose up -d` may have silently failed
- Emulator container never started
- Port 5672 never bound, causing immediate refusal

**Verification Steps Needed:**
```powershell
# Run with admin elevation
docker ps
docker-compose -f infra/servicebus/compose.yaml ps
docker logs servicebus-emulator
docker logs sqledge
```

#### 2. **Insufficient Startup Wait Time** (Secondary)

**Timeline:**
- `run-dashboard.ps1` waits 15 seconds after `docker-compose up -d` (line 147-148)
- This is a **background start**, NOT a readiness wait
- Emulator actually needs to:
  1. Start SQL Edge container
  2. Initialize SQL Edge database (~15 seconds)
  3. Start emulator service
  4. Load config.json
  5. Bind to AMQP port 5672
  6. Become ready for connections

**Expected Timeline:**
- SQL Edge initialization: 15-30 seconds
- Service Bus emulator startup: 10-20 seconds
- **Total: 25-50+ seconds** (15 seconds is too short)

**Evidence from test script:**
- `test-emulator-startup.ps1` (line 52) defines `$maxWait = 300` (5 minutes)
- Includes polling loop checking port 5672 readiness (lines 65-94)
- This indicates emulator readiness is **NOT guaranteed** within 15 seconds

---

## Diagnosis Summary

### What's Happening

1. **run-dashboard.ps1** starts docker-compose and waits 15 seconds
2. **SQL Edge container** is still initializing (needs ~15-30 seconds)
3. **Service Bus emulator** cannot start until SQL Edge is ready
4. **Port 5672** is not yet listening when Producer attempts connection
5. **TCP connection refused** - emulator not bound to port

### What Should Happen

1. Emulator containers start with `docker-compose up -d`
2. **System waits for port 5672 to become accessible** (not just arbitrary 15 seconds)
3. Producer connects and publishes events
4. Consumers receive filtered messages

---

## Required Fixes

### Fix 1: Verify Docker is Running (BLOCKING)

**Step 1:** Check Docker status with admin privileges
```powershell
# Run PowerShell as Administrator
docker version
docker ps
```

**Expected Output:**
```
Client:
 Version:   27.x.x
 OS/Arch:   windows/amd64

Containers:
CONTAINER ID   IMAGE                                          STATUS
abcd1234       mcr.microsoft.com/azure-messaging/servicebus   Up X seconds
efgh5678       mcr.microsoft.com/azure-sql-edge               Up X seconds
```

**If Docker is not running:**
- Start Docker Desktop
- Verify WSL 2 backend is enabled (Windows)
- Verify containers are properly initialized

---

### Fix 2: Increase Emulator Startup Wait (REQUIRED)

**Current:** 15 seconds (insufficient)  
**Recommended:** 45-60 seconds (safe for most systems)

**Location:** `scripts/run-dashboard.ps1`, line 147-148

**Change:**
```powershell
# BEFORE (WRONG)
Write-Host "  Waiting 15 seconds for minimal initialization..."
Start-Sleep -Seconds 15

# AFTER (CORRECT)
Write-Host "  Waiting for emulator AMQP port to become available..."
$maxAttempts = 120  # 120 seconds maximum
$checkInterval = 1
$portReady = $false
for ($i = 0; $i -lt $maxAttempts; $i++) {
    try {
        $socket = New-Object System.Net.Sockets.TcpClient
        $async = $socket.BeginConnect('localhost', 5672, $null, $null)
        if ($async.AsyncWaitHandle.WaitOne(1000)) {
            $socket.EndConnect($async)
            $portReady = $true
            $socket.Close()
            break
        }
    } catch {}
    if (-not $portReady) {
        Write-Host "    [$($i+1)s] Emulator port not ready..." -ForegroundColor Gray
        Start-Sleep -Seconds 1
    }
}
if ($portReady) {
    Write-Host "  ✓ Emulator AMQP port is ready"
} else {
    Write-Error "❌ Emulator did not become ready within 120 seconds"
    exit 1
}
```

**Better Alternative:** Use the existing `test-emulator-startup.ps1` script as a function in `run-dashboard.ps1` to ensure port 5672 is listening before proceeding.

---

### Fix 3: Update run-local-poc.ps1 Similarly (REQUIRED)

**Location:** `scripts/run-local-poc.ps1`, line 133-134

**Same Fix:** Replace 15-second sleep with port readiness check

---

## Verification Steps (After Fixes)

### Step 1: Manually Test Emulator Startup

```powershell
# From elevated PowerShell
cd infra/servicebus
docker-compose down  # Clean up any stale containers
docker-compose up -d
Start-Sleep -Seconds 5

# Check container status
docker ps
# Expected: Both servicebus-emulator and sqledge should show "Up X seconds"

# Check logs for "Emulator Ready"
docker logs servicebus-emulator | Select-String -Pattern "Ready|listening|5672|AMQP" -Context 2

# Test port connectivity
$socket = New-Object System.Net.Sockets.TcpClient
$socket.Connect('localhost', 5672)
$socket.Close()
Write-Host "✓ Port 5672 is accessible"
```

### Step 2: Run Dashboard with Updated Script

```powershell
.\scripts\run-dashboard.ps1
# Should now:
# 1. Start emulator containers
# 2. Poll port 5672 until ready
# 3. Start Producer (which should connect successfully)
# 4. Dashboard shows all services Online
```

### Step 3: Verify Producer Connects

**Check logs:** `logs/Producer-stdout.log` should show:
```
info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Producer service starting...
info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 1 (elapsed: X.Xs)... [ABOUT TO TRY]
info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection successful!
info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Publishing sample contact events...
```

---

## Docker Troubleshooting (If Still Failing)

### Docker Not Running

**Symptoms:** `docker: not found` or permission errors

**Fix:**
```powershell
# Check if Docker Desktop is installed
Get-Command docker

# Start Docker Desktop (GUI)
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"
Start-Sleep -Seconds 30

# Verify WSL 2 backend
wsl --list -v
# Expected: * Ubuntu (or distro) with Version 2

# Test Docker
docker run hello-world
```

### Container Won't Start

**Symptoms:** `docker-compose up -d` returns but containers not running

**Fix:**
```powershell
cd infra/servicebus

# Check for existing containers/volumes
docker-compose ps -a
docker volume ls

# Clean slate
docker-compose down -v

# Start with verbose output
docker-compose up  # (not -d, so you see logs)
```

### SQL Edge Won't Initialize

**Symptoms:** Emulator logs show SQL connection errors

**Possible Causes:**
1. SQL_PASSWORD doesn't meet complexity requirements (change in .env)
2. Port 1433 (SQL) already in use
3. Not enough disk space

**Fix:**
```powershell
# Change SQL password to something more complex
# Edit infra/servicebus/.env
# SQL_PASSWORD=NewP@ssw0rd123!

# Check port availability
netstat -ano | findstr :1433  # SQL port
netstat -ano | findstr :5672  # AMQP port

# If ports in use, stop conflicting service or change docker-compose ports
```

---

## Summary of Changes Needed

| Item | Current | Required | Priority |
|------|---------|----------|----------|
| Docker Status | ❓ Unknown | ✅ Verify Running | **BLOCKING** |
| Emulator Wait Time | 15 seconds (sleep) | 45-120 seconds (poll) | **HIGH** |
| Port Readiness Check | ❌ None | ✅ Check 5672 listening | **HIGH** |
| Error Handling | Generic sleep | Specific port check | **MEDIUM** |
| Retry Logic | ✅ Exists in Producer | ✅ Keep as-is | LOW |

---

## Next Steps for User

1. **FIRST:** Verify Docker is running (with admin privileges)
   ```powershell
   # Run as Administrator
   docker version
   docker ps
   ```

2. **SECOND:** Apply fixes to `run-dashboard.ps1` and `run-local-poc.ps1` (wait logic)

3. **THIRD:** Test emulator startup in isolation
   ```powershell
   .\scripts\test-emulator-startup.ps1
   ```

4. **FOURTH:** Run full scenario
   ```powershell
   .\scripts\run-dashboard.ps1
   ```

5. **Monitor:** Check `logs/Producer-stdout.log` for successful connection

---

## Reference Materials

- **Azure SDK Troubleshooting:** https://aka.ms/azsdk/net/servicebus/exceptions/troubleshoot
- **Service Bus Emulator Docs:** https://github.com/Azure/azure-service-bus-emulator-installer
- **Docker Compose:** https://docs.docker.com/compose/
- **AMQP Port:** 5672 (standard, configured in compose.yaml line 10)

---

## Files to Review/Modify

```
scripts/
├── run-dashboard.ps1           (Fix: Add port readiness check, line 147-148)
├── run-local-poc.ps1            (Fix: Add port readiness check, line 133-134)
├── test-emulator-startup.ps1    (Reference: Port check logic, lines 65-94)
└── measure-emulator-readiness.ps1 (Reference: Alternative diagnostic)

infra/servicebus/
├── compose.yaml                 (OK: Port 5672 mapped correctly)
├── .env                         (OK: ACCEPT_EULA=Y, passwords configured)
└── config.json                  (OK: Topic and subscriptions defined)

src/
└── ServiceBusPoc.Producer/
    └── Services/ProducerService.cs  (Reference: Retry logic at line 44)
```

---

## Risk Assessment

- **Severity:** 🔴 **CRITICAL** - System cannot start producers
- **Blast Radius:** All consumers blocked (depend on producer connection)
- **Time to Fix:** 15-30 minutes (apply wait logic + test)
- **Rollback:** N/A (diagnostic, non-destructive)

---

*Generated: 2026-09-17 @ 10:55 UTC+8*  
*Diagnosis Author: Remy (Producer)*
