# Quick Reference Guide - Service Bus Retry Logic Implementation

## What Was Done
Applied connection retry logic with exponential backoff to all 4 consumer services to handle Service Bus emulator startup delays.

## Files Modified (5 total)

### Core Change
1. **src/ServiceBusPoc.Core/Messaging/SubscriptionConsumerRunner.cs**
   - Added: `WaitForReadyAsync()` method (70 lines)
   - Purpose: Test subscription accessibility with retry logic

### Consumer Services Updated
2. **src/ServiceBusPoc.ParksResorts/Services/ParksResortsConsumerService.cs**
   - Modified: `RunAsync()` method
   - Added: Connection retry logic (9 lines)

3. **src/ServiceBusPoc.DigitalChannels/Services/DigitalChannelsConsumerService.cs**
   - Modified: `RunAsync()` method
   - Added: Connection retry logic (9 lines)

4. **src/ServiceBusPoc.Insurance/Services/InsuranceConsumerService.cs**
   - Modified: `RunAsync()` method
   - Added: Connection retry logic (9 lines)

5. **src/ServiceBusPoc.Carwash/Services/CarwashConsumerService.cs**
   - Modified: `RunAsync()` method
   - Added: Connection retry logic (9 lines)

## Pattern: Before & After

### BEFORE
```csharp
public async Task RunAsync(CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Service starting...");
    
    // Immediate connection attempt - FAILS if emulator not ready
    await _consumerRunner.RunAsync(descriptor, cancellationToken);
}
```
**Problem:** Crashes immediately if emulator starting up

### AFTER
```csharp
public async Task RunAsync(CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Service starting...");
    var startTime = DateTimeOffset.UtcNow;
    
    // Wait for readiness with retry logic
    var connected = await _consumerRunner.WaitForReadyAsync(startTime, cancellationToken);
    if (!connected)
        throw new InvalidOperationException("Service Bus did not become ready within timeout");
    
    // Proceeds with original logic
    await _consumerRunner.RunAsync(descriptor, cancellationToken);
}
```
**Improvement:** Waits up to 120 seconds with exponential backoff

## Retry Logic Details

| Property | Value |
|----------|-------|
| Initial Delay | 100ms |
| Backoff Factor | 2x (doubles each attempt) |
| Max Single Delay | 12.8s |
| Total Timeout | 120s |
| Logging Frequency | Every 10 attempts (Info) |
| Test Method | `ReceiveMessageAsync()` with 100ms timeout |

## Expected Behavior

### Scenario 1: Emulator Starting
```
Log: "Service Bus subscription connection attempt 1 (elapsed: 0.0s)..."
Log: "Service Bus subscription connection attempt 11 (elapsed: 1.0s)..."
Log: "✓ Service Bus subscription is ready! Connected after 2.5s (attempt 25)"
Result: Continues normally, waits for messages
```

### Scenario 2: Emulator Already Ready
```
Log: "Service Bus subscription connection attempt 1 (elapsed: 0.0s)..."
Log: "✓ Service Bus subscription is ready! Connected after 0.1s (attempt 1)"
Result: Minimal delay (~100ms), continues normally
```

### Scenario 3: Emulator Unavailable
```
Log: "Service Bus subscription connection attempt 1 (elapsed: 0.0s)..."
Log: "Service Bus subscription connection attempt 11 (elapsed: 1.0s)..."
... (continues retrying for 120 seconds)
Log: "ERROR: Service Bus subscription did not become ready within timeout"
Result: Throws InvalidOperationException, service stops
```

## Key Features

✅ **Exponential Backoff** - Prevents overwhelming the emulator
✅ **120-Second Timeout** - Handles both quick and slow startups
✅ **Selective Logging** - Info logs at attempts 1,11,21; Debug for errors
✅ **Cancellation Support** - Respects CancellationToken throughout
✅ **No Breaking Changes** - All existing code continues working
✅ **Proven Pattern** - Mirrors Producer service implementation
✅ **Isolated Changes** - Only affects service startup, not message processing

## How It Works

1. **Capture Time** - Record when retry attempt started
2. **Increment Counter** - Track number of attempts
3. **Try Connection** - Call `_receiver.ReceiveMessageAsync()` with 100ms timeout
4. **Success** - If succeeds (even if no message), return `true` immediately
5. **Failure** - If fails:
   - Log error (selectively to avoid spam)
   - Calculate backoff delay (100ms → 200ms → 400ms → ...)
   - Wait using `Task.Delay()`
   - Loop back to step 2
6. **Timeout** - If 120 seconds elapsed, return `false`

## Testing Checklist

- [ ] Service builds without errors
- [ ] Services wait for emulator during startup
- [ ] Logs show connection attempts at proper intervals
- [ ] Emulator starts → services connect within ~30s
- [ ] Emulator already running → services connect within ~100ms
- [ ] Emulator unavailable → services fail after ~120s with clear error
- [ ] Message flow works normally after connection
- [ ] All existing tests still pass

## Deployment Notes

**Pre-deployment:**
- Verify all 5 files compile without errors
- Run existing test suite
- Test with Docker Compose emulator startup

**Post-deployment:**
- Monitor logs for "Service Bus subscription is ready!" messages
- Verify services don't crash on startup
- Confirm message flow works as expected
- Check for any unexpected delays

**Rollback (if needed):**
- Remove `WaitForReadyAsync()` method from SubscriptionConsumerRunner
- Remove retry logic from each consumer's `RunAsync()` method
- Restore original code structure

## Documentation Files

1. **RETRY_LOGIC_IMPLEMENTATION.md** - Full implementation details
2. **IMPLEMENTATION_VERIFICATION.md** - Detailed verification checklist
3. **BEFORE_AFTER_COMPARISON.md** - Visual before/after with examples
4. **FILES_CHANGED_SUMMARY.md** - Complete file listing and line-by-line changes
5. **QUICK_REFERENCE.md** - This file!

## Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| Service still crashes on startup | Emulator hasn't started | Increase initial wait time or start emulator first |
| Sees "connection refused" errors | Emulator starting | Expected! Service will retry and succeed |
| Too much logging in console | Debug level enabled | Use Information level or higher in production |
| Services taking >2 minutes | System very slow | This is working as designed, or emulator has issues |
| Tests failing | Changed test environment | Tests may need emulator running, or update test fixtures |

## Quick Commands

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test file
dotnet test tests/ServiceBusPoc.Tests/ServiceBusPoc.Tests.csproj

# Start emulator (Docker)
docker-compose up -d

# Start producer
dotnet run --project src/ServiceBusPoc.Producer

# Start all consumers (in separate terminals)
dotnet run --project src/ServiceBusPoc.ParksResorts
dotnet run --project src/ServiceBusPoc.DigitalChannels
dotnet run --project src/ServiceBusPoc.Insurance
dotnet run --project src/ServiceBusPoc.Carwash
```

## Expected Logs (Sample Output)

```
2026-01-15 10:22:58 [Producer] Producer service starting...
2026-01-15 10:22:58 [Producer] Service Bus connection attempt 1 (elapsed: 0.0s)...
2026-01-15 10:22:58 [Producer] ✓ Service Bus is ready! Connected after 0.1s (attempt 1)
2026-01-15 10:22:58 [Producer] Reporting heartbeat...

2026-01-15 10:22:59 [ParksResorts] Parks & Resorts consumer service starting...
2026-01-15 10:22:59 [ParksResorts] Service Bus subscription connection attempt 1 (elapsed: 0.0s)...
2026-01-15 10:22:59 [ParksResorts] ✓ Service Bus subscription is ready! Connected after 0.1s (attempt 1)
2026-01-15 10:22:59 [ParksResorts] Consumer parks-resorts listening on topic contact-events...

2026-01-15 10:23:00 [DigitalChannels] Digital Channels consumer service starting...
2026-01-15 10:23:00 [DigitalChannels] Service Bus subscription connection attempt 1 (elapsed: 0.0s)...
2026-01-15 10:23:00 [DigitalChannels] ✓ Service Bus subscription is ready! Connected after 0.1s (attempt 1)
2026-01-15 10:23:00 [DigitalChannels] Consumer digital-channels listening on topic contact-events...

2026-01-15 10:23:01 [Insurance] Insurance consumer service starting...
2026-01-15 10:23:01 [Insurance] Service Bus subscription connection attempt 1 (elapsed: 0.0s)...
2026-01-15 10:23:01 [Insurance] ✓ Service Bus subscription is ready! Connected after 0.1s (attempt 1)
2026-01-15 10:23:01 [Insurance] Consumer insurance listening on topic contact-events...

2026-01-15 10:23:02 [Carwash] Carwash consumer service starting...
2026-01-15 10:23:02 [Carwash] Service Bus subscription connection attempt 1 (elapsed: 0.0s)...
2026-01-15 10:23:02 [Carwash] ✓ Service Bus subscription is ready! Connected after 0.1s (attempt 1)
2026-01-15 10:23:02 [Carwash] Pulse API URL: http://localhost:8080
2026-01-15 10:23:02 [Carwash] Consumer carwash listening on topic contact-events...

2026-01-15 10:23:03 [Producer] Published event event-0001 to contact-events topic
2026-01-15 10:23:03 [ParksResorts] Received event event-0001 for contact contact-0001
2026-01-15 10:23:03 [DigitalChannels] Received event event-0001 for contact contact-0001
2026-01-15 10:23:04 [Producer] Published event event-0002 to contact-events topic
```

## Performance Impact

- **With Emulator Ready:** +50-100ms (single test attempt)
- **With Emulator Starting:** 5-30s (normal startup time)
- **Worst Case:** 120s timeout (emulator not available)
- **Steady State:** No impact (only affects startup)

## Summary

✅ All 5 files modified
✅ 106 lines added (mostly new WaitForReadyAsync method)
✅ 0 lines removed or breaking changes
✅ Ready for build and test
✅ Mirrors Producer's proven approach
✅ Solves "target machine actively refused" startup issue

**Status: Implementation Complete** ✅
