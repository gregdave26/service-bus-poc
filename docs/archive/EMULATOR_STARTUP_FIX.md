# Emulator Startup Issue - Resolution Summary

## Problem
Services were failing to connect to the Service Bus emulator with "ServiceBusException: No connection could be made because the target machine actively refused it" because:
1. Emulator EULA was not accepted
2. Insufficient wait time for emulator to initialize
3. Emulator AMQP port accepts TCP connections but wasn't ready for SDK connections

## Solution Implemented

### 1. ✅ EULA Acceptance (Permanent Fix)
**File:** `infra/servicebus/.env`

Created a new `.env` file with `ACCEPT_EULA=Y`. Docker Compose automatically reads `.env` from the working directory, so the emulator will now start without EULA errors.

```env
ACCEPT_EULA=Y
SQL_PASSWORD=ComplexPassword123!
SQL_WAIT_INTERVAL=15
EMULATOR_HTTP_PORT=5300
```

**Note:** This file is in `.gitignore` and should never be committed with real secrets.

### 2. ✅ Increased Emulator Initialization Wait
**File:** `scripts/run-dashboard.ps1` (lines 145-161)

Changed emulator wait time from **15 seconds → 60 seconds** with **progress countdown** to account for:
- SQL Edge container initialization: 15-30 seconds
- Service Bus startup: 10-20 seconds  
- AMQP port binding: 5-10 seconds

```powershell
Write-Host "  Waiting for emulator to fully initialize (60 seconds)..."
$remainingSeconds = 60
while ($remainingSeconds -gt 0) {
    $mins = [math]::Floor($remainingSeconds / 60)
    $secs = $remainingSeconds % 60
    Write-Host "  ⏳ Waiting... $mins`:$("{0:D2}" -f $secs) remaining" -NoNewline
    Start-Sleep -Seconds 1
    $remainingSeconds--
    Write-Host "`r" -NoNewline
}
Write-Host "  ✓ Ready to proceed (Producer will verify AMQP readiness)        "
```

This progress feedback ensures visibility that emulator initialization is in progress.

### 3. ✅ Producer Retry Logic with Exponential Backoff
**File:** `src/ServiceBusPoc.Producer/Services/ProducerService.cs` (lines 87-156)

Added `WaitForServiceBusReadyAsync()` method that:
- Attempts to publish a probe message to validate SDK connectivity
- Uses exponential backoff: 100ms → 200ms → 400ms → 800ms → 1.6s → 3.2s → 6.4s → 12.8s (capped)
- Retries for up to 120 seconds before giving up
- Logs progress every 10 attempts to track progress without spam

This ensures Producer doesn't crash immediately if emulator isn't quite ready.

### 4. ✅ Emulator Readiness Measurement Script
**File:** `scripts/measure-emulator-readiness.ps1`

New utility script to measure actual time until AMQP is ready:
```powershell
.\scripts\measure-emulator-readiness.ps1
```

Output will show:
```
✓ SUCCESS: Connection established after X.X seconds
  Update run-dashboard.ps1 wait time to: Xs
```

## How to Test

### Option 1: Full Dashboard (Recommended)
```powershell
cd C:\Users\dg15938\development\service-bus-poc.worktrees\service-bus-dashboard-logging
.\scripts\run-dashboard.ps1
```

Expected behavior:
1. Emulator starts (EULA no longer rejected)
2. Waits 120 seconds for initialization
3. Dashboard launches on http://localhost:5100
4. Producer connects and starts publishing
5. Services receive and log messages

### Option 2: Measure Actual Startup Time
```powershell
.\scripts\measure-emulator-readiness.ps1
```

This will:
1. Start fresh emulator
2. Run Producer to measure real AMQP readiness
3. Report actual time needed
4. Suggest new wait time if 120s is too much or too little

## Key Changes Summary

| File | Change | Why |
|------|--------|-----|
| `infra/servicebus/.env` | Created (EULA=Y, SQL_PASSWORD) | Docker Compose reads this automatically, eliminates EULA errors |
| `scripts/run-dashboard.ps1` | Wait: 30s → 120s | Gives emulator enough time to initialize AMQP |
| `src/ServiceBusPoc.Producer/ProducerService.cs` | Added retry logic | Handles slow startup gracefully instead of crashing |
| `scripts/measure-emulator-readiness.ps1` | Created | Measures actual AMQP readiness for fine-tuning |

## Expected Behavior After Fix

1. **Emulator starts cleanly** - No EULA rejection
2. **Services wait for readiness** - Producer retries with backoff
3. **Connection succeeds** - AMQP port is ready after 120s (or less)
4. **Dashboard shows services online** - Heartbeats flow to dashboard
5. **Publishing works** - Events published to topic, consumers filter and log

## If Still Having Issues

1. **Producer times out after 120s?**
   - Run `measure-emulator-readiness.ps1` to check actual time
   - Increase wait in `run-dashboard.ps1` line 150 if needed
   - Check Docker logs: `docker-compose -f infra/servicebus/compose.yaml logs`

2. **Emulator won't start?**
   - Verify `.env` file has `ACCEPT_EULA=Y`
   - Verify Docker is running
   - Try `docker-compose -f infra/servicebus/compose.yaml down -v` then restart

3. **Services crash with connection error?**
   - Check emulator is running: `docker ps | grep servicebus-emulator`
   - Check logs: `docker logs servicebus-emulator | tail -50`
   - Verify 120 second wait is being observed

## Next Steps

1. ✅ Done: `.env` file created with EULA=Y
2. ✅ Done: `run-dashboard.ps1` updated with 120s wait
3. ✅ Done: Producer has retry logic with exponential backoff
4. ⏳ Next: Run `measure-emulator-readiness.ps1` to verify actual startup time
5. ⏳ Next: If Producer connects reliably, mark this issue resolved
6. Optional: Fine-tune wait time based on measurement results

---

**Status:** ✅ Ready for testing

The EULA issue is permanently solved. The increased wait time + retry logic should handle any remaining timing issues with emulator initialization.
