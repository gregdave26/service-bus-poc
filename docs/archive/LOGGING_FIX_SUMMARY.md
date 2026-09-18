# Retry Logging Fix Summary

## Issue
Producer shows "attempt 1" but then no further retry attempts are logged. Users see a single line followed by silence while the system is still waiting for Service Bus to become ready.

## Root Cause
- **Attempt logging threshold too high:** Only logs attempts 1, 11, 21, 31... (every 10th, starting at 1)
- **Error logging too sparse:** Only logs errors on attempts 1-5 and every 20th after
- **No intermediate progress feedback:** After initial log, no indication that retries are ongoing

## Solution Implemented

### File Modified
`src/ServiceBusPoc.Producer/Services/ProducerService.cs` (lines 95-170)

### Changes Made

#### 1. **Improved Attempt Logging** (lines 108-115)
```csharp
// OLD:
if (attempt % 10 == 1)  // Log attempts 1, 11, 21, etc

// NEW:
// Log first 10 attempts, then every 5th attempt (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 25...)
if (attempt <= 10 || attempt % 5 == 0)
```
**Result:** Instead of seeing only attempts 1, 11, 21..., users now see:
- All first 10 attempts (1-10) → rapid feedback on initial failures
- Then every 5th attempt (15, 20, 25...) → reduced spam for longer waits

#### 2. **Better Error Logging** (lines 140-150)
```csharp
// OLD:
if (attempt <= 5 || attempt % 20 == 0)

// NEW:
if (attempt <= 10 || attempt % 10 == 0)
```
**Result:** Errors logged for:
- First 10 attempts (detailed diagnostics for early failures)
- Then every 10th (20, 30, 40...) → shows problems if retry loop continues

#### 3. **Added Intermediate Progress Logging** (lines 97-98, 152-160)
```csharp
// NEW: Track progress logging interval
var lastProgressLogTime = startTime;
const int ProgressLogIntervalSeconds = 25;

// NEW: Log every 25 seconds if still retrying
if ((elapsedAtError - TimeSpan.FromSeconds(ProgressLogIntervalSeconds)) >= (lastProgressLogTime - startTime))
{
    _logger.LogInformation(
        "Still waiting for Service Bus... (elapsed: {ElapsedSeconds:F1}s, attempt {Attempt})",
        elapsedAtError.TotalSeconds,
        attempt);
    lastProgressLogTime = DateTimeOffset.UtcNow;
}
```
**Result:** If Service Bus takes >25 seconds to be ready, user sees periodic "Still waiting..." messages confirming the app hasn't hung.

## Expected Output

### Before (Silent After Attempt 1)
```
info: Service Bus connection attempt 1 (elapsed: 0.0s)...
[... 30 seconds of silence while retrying ...]
```

### After (Continuous Feedback)
```
info: Service Bus connection attempt 1 (elapsed: 0.0s)...
debug: Connection attempt 1 failed (elapsed: 0.1s): ServiceBusException
info: Service Bus connection attempt 2 (elapsed: 0.2s)...
debug: Connection attempt 2 failed (elapsed: 0.3s): ServiceBusException
info: Service Bus connection attempt 3 (elapsed: 0.5s)...
debug: Connection attempt 3 failed (elapsed: 0.6s): ServiceBusException
[... continues for first 10 attempts ...]
info: Service Bus connection attempt 10 (elapsed: 3.0s)...
debug: Connection attempt 10 failed (elapsed: 3.1s): ServiceBusException
info: Service Bus connection attempt 15 (elapsed: 4.0s)...
debug: Connection attempt 15 failed (elapsed: 4.1s): ServiceBusException
info: Still waiting for Service Bus... (elapsed: 25.0s, attempt 50)
info: Service Bus connection attempt 20 (elapsed: 5.0s)...
[... continues ...]
info: ✓ Service Bus is ready! Connected after 45.3s (attempt 200)
```

## Testing

The fix should be verified by:
1. **Building the project:** `dotnet build src/ServiceBusPoc.Producer/`
2. **Running the producer with Service Bus emulator not yet ready:**
   - Start the app while emulator is down
   - Observe: Attempts 1-10, then every 5th, plus 25-second progress markers
   - Start the emulator after a few attempts
   - Observe: Success message with total elapsed time
3. **Running with normal start:** Verify no additional log spam when Service Bus starts immediately

## Benefits

- ✅ **User feedback:** Clear visibility into retry loop progress
- ✅ **Diagnostics:** More detailed error logging in first 10 attempts helps troubleshooting
- ✅ **Anti-hang confirmation:** 25-second intervals prevent "is it frozen?" confusion
- ✅ **Scalable logging:** Reduces spam for long retry sequences (every 5 instead of every 10 after first 10)
- ✅ **Maintains performance:** No additional overhead beyond time tracking

## Code Quality
- No breaking changes
- Uses existing logger interface
- Time tracking is lightweight (single DateTimeOffset comparison)
- Consistent with .NET logging best practices
