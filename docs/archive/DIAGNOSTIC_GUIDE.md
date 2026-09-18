# Diagnostic Guide: Single Attempt Issue

## Changes Implemented ✅

1. **Timestamps Added** - All logs now include ISO 8601 format with timezone
   - Format: `2026-09-17T10:41:29.527+08:00 info: ...`
   
2. **Defensive Logging Added** - Retry loop has markers at each step
   - `[ABOUT TO TRY]` - Before connection attempt
   - `[EXCEPTION CAUGHT]` - Exception in catch block
   - `[ABOUT TO DELAY]` - Before Task.Delay
   - `[DELAY COMPLETE]` - After Task.Delay completes

3. **Detailed Error Logging** - Exception type and message logged
   - Line 150-154 in ProducerService
   - Line 90-100 in SubscriptionConsumerRunner

## Issue: Only One Attempt Logged

**Possible Causes:**

### 1. Log Buffering
When logs are redirected to files, they might not flush in real-time.

**Test:**
```powershell
# Run with terminal output (not redirected)
.\scripts\run-dashboard.ps1 -Terminals $true
```

This runs each service in its own window instead of redirecting to files. You should see logs stream in real-time.

### 2. Process Exiting
The service might be crashing/exiting after the first attempt.

**Check:**
- Look for error message after attempt 1
- Check if process is still running
- Look for exit code in output

### 3. Async Deadlock
The `Task.Delay` or `PublishAsync`/`ReceiveAsync` calls might be hanging.

**Diagnostic Steps:**

1. **Check with timestamps:**
   ```
   Expected: 2026-09-17T10:41:29.527 [ABOUT TO TRY]
            2026-09-17T10:41:29.628 [EXCEPTION CAUGHT]
            2026-09-17T10:41:29.629 [ABOUT TO DELAY]
            2026-09-17T10:41:29.730 [DELAY COMPLETE]
            2026-09-17T10:41:29.730 [ABOUT TO TRY]
   
   If you see: [ABOUT TO DELAY] at 29.629 but no [DELAY COMPLETE] at 29.730
             → Indicates Task.Delay is hanging
   ```

2. **Run Producer directly in terminal:**
   ```powershell
   cd src/ServiceBusPoc.Producer
   $env:ServiceBus__ConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE"
   $env:ServiceBus__TopicName = "contact.events"
   $env:Dashboard__Enabled = "false"
   
   dotnet run
   ```
   
   Let it run for 60 seconds. You should see:
   - Timestamps on each line
   - Attempt 1, 2, 3, 4, 5... progression
   - `[ABOUT TO TRY]` on each attempt
   - `[EXCEPTION CAUGHT]` after failures
   - `[ABOUT TO DELAY]` before sleep
   - `[DELAY COMPLETE]` after sleep
   - `Still waiting...` every 25 seconds
   - Eventually `✓ Service Bus is ready!` when emulator accepts connection

3. **Check emulator status:**
   ```powershell
   docker ps
   docker logs servicebus-emulator | tail -20
   ```

## What Should Happen

### Timeline (Fast Path - Emulator Ready in ~10s)
```
2026-09-17T10:41:29.527 info: Producer service starting...
2026-09-17T10:41:29.528 info: Service Bus connection attempt 1 (elapsed: 0.0s)... [ABOUT TO TRY]
2026-09-17T10:41:29.629 warn: Connection attempt 1 failed (elapsed: 0.1s): ServiceBusException [EXCEPTION CAUGHT]
2026-09-17T10:41:29.630 dbug: Sleeping for 100ms before retry [ABOUT TO DELAY]
2026-09-17T10:41:29.730 dbug: Sleep completed, resuming loop iteration (attempt 2) [DELAY COMPLETE]
2026-09-17T10:41:29.730 info: Service Bus connection attempt 2 (elapsed: 0.2s)... [ABOUT TO TRY]
2026-09-17T10:41:29.830 warn: Connection attempt 2 failed (elapsed: 0.3s): ServiceBusException [EXCEPTION CAUGHT]
2026-09-17T10:41:29.831 dbug: Sleeping for 200ms before retry [ABOUT TO DELAY]
2026-09-17T10:41:30.031 dbug: Sleep completed, resuming loop iteration (attempt 3) [DELAY COMPLETE]
... [continues] ...
2026-09-17T10:41:39.500 info: ✓ Service Bus is ready! Connected after 10.0s (attempt 57)
```

### Timeline (Slow Path - Emulator Ready in ~30s)
```
[Same as above through attempt 10...]
2026-09-17T10:41:34.500 info: Service Bus connection attempt 15 (elapsed: 5.0s)...
2026-09-17T10:41:34.600 warn: Connection attempt 15 failed...
...
2026-09-17T10:41:54.500 info: Still waiting for Service Bus... (elapsed: 25.0s, attempt 125)
...
2026-09-17T10:41:59.500 info: ✓ Service Bus is ready! Connected after 30.0s (attempt 175)
```

## If Only Seeing One Attempt

1. **Verify timestamps are there:**
   ```
   BAD:   attempt 1 (elapsed: 0.0s)...
          [silence]
   
   GOOD:  2026-09-17T10:41:29.528 info: attempt 1 (elapsed: 0.0s)...
          2026-09-17T10:41:29.629 warn: Connection attempt 1 failed...
          2026-09-17T10:41:29.630 dbug: Sleeping for 100ms...
   ```

2. **Look for markers:**
   ```
   grep "\[ABOUT TO TRY\]" logs.txt       # Should see multiple
   grep "\[EXCEPTION CAUGHT\]" logs.txt   # Should see multiple
   grep "\[DELAY COMPLETE\]" logs.txt     # Should see multiple
   ```

3. **Check timestamps progression:**
   - If timestamps jump from 29.628 to 29.730 (100ms gap) → Delay working
   - If only one timestamp → Process may have exited

## Recommended Test

Run with `-Terminals $true` to see real-time output:

```powershell
.\scripts\run-dashboard.ps1 -Terminals $true
```

Watch the separate service windows - you should see:
- Producer window: Multiple retry attempts every second or so
- Consumer windows: Similar retry attempts

If you still see only 1 line, something is blocking the retry loop or the process is exiting.

## Next Debugging Steps

If issue persists after running with -Terminals $true:

1. **Add even more logging** - Log at method entry/exit
2. **Check if emulator is actually running** - Verify port 5672 listening
3. **Try with mock Service Bus** - Bypass emulator entirely
4. **Run with debugger** - Step through retry loop
5. **Check system logs** - Windows event viewer for crashes

---

**Status:** All code changes complete. Now need to run with `-Terminals $true` to see real-time output and validate retry progression.
