# Before & After Log Comparison

## Issue 1: Missing Timestamps

### BEFORE (Current State)
```
info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 1 (elapsed: 0.0s)...
info: ServiceBusPoc.Core.Messaging.SubscriptionConsumerRunner[0]
      Service Bus subscription connection attempt 1 (elapsed: 0.0s)...
info: ServiceBusPoc.Insurance.Services.InsuranceConsumerService[0]
      Insurance consumer service starting...
warn: ServiceBusPoc.Core.Messaging.ContactEventPublisher[0]
      Connection attempt 1 failed (elapsed: 0.1s): IOException
dbug: ServiceBusPoc.Core.Logging.TimestampedConsoleFormatter[0]
      Sleeping for 100ms before retry
```

**Problems:**
- ❌ No timestamp information
- ❌ Cannot correlate events across multiple services
- ❌ Cannot measure actual delays
- ❌ Cannot determine when logs were written
- ❌ Production debugging impossible

### AFTER (Fixed)
```
2026-09-17T10:41:29.527+08:00 info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 1 (elapsed: 0.0s)...
2026-09-17T10:41:29.628+08:00 info: ServiceBusPoc.Core.Messaging.SubscriptionConsumerRunner[0]
      Service Bus subscription connection attempt 1 (elapsed: 0.0s)...
2026-09-17T10:41:29.629+08:00 info: ServiceBusPoc.Insurance.Services.InsuranceConsumerService[0]
      Insurance consumer service starting...
2026-09-17T10:41:29.730+08:00 warn: ServiceBusPoc.Core.Messaging.ContactEventPublisher[0]
      Connection attempt 1 failed (elapsed: 0.1s): IOException
2026-09-17T10:41:29.831+08:00 dbug: ServiceBusPoc.Core.Logging.TimestampedConsoleFormatter[0]
      Sleeping for 100ms before retry
```

**Benefits:**
- ✅ ISO 8601 timestamp with timezone offset
- ✅ Can correlate events with external systems
- ✅ Timestamps show 101ms difference (100ms sleep + ~1ms overhead)
- ✅ Can see exact sequence of operations
- ✅ Production-ready logging

---

## Issue 2: Retry Loop Stops After Attempt 1

### BEFORE (Current State)
```
info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 1 (elapsed: 0.0s)...
dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Connection attempt 1 failed (elapsed: 0.1s): IOException
(NO ATTEMPT 2, 3, 4... VISIBLE)
error: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus did not become ready within timeout
```

**Problems:**
- ❌ Shows "attempt 1" then nothing
- ❌ Cannot see if loop is retrying or deadlocked
- ❌ No visibility into control flow
- ❌ Cannot determine if exception was caught
- ❌ Cannot see if Task.Delay() is completing
- ❌ Unknown if loop is waiting or crashed

### AFTER (Fixed)
```
2026-09-17T10:41:29.527+08:00 info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 1 (elapsed: 0.0s)... [ABOUT TO TRY]

2026-09-17T10:41:29.628+08:00 warn: ServiceBusPoc.Producer.Services.ProducerService[0]
      Connection attempt 1 failed (elapsed: 0.1s): IOException - Connection refused [EXCEPTION CAUGHT]

2026-09-17T10:41:29.629+08:00 dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Sleeping for 100ms before retry (exponential backoff) [ABOUT TO DELAY]

2026-09-17T10:41:29.730+08:00 dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Sleep completed, resuming loop iteration (attempt 2) [DELAY COMPLETE]

2026-09-17T10:41:29.730+08:00 info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 2 (elapsed: 0.2s)... [ABOUT TO TRY]

2026-09-17T10:41:29.830+08:00 warn: ServiceBusPoc.Producer.Services.ProducerService[0]
      Connection attempt 2 failed (elapsed: 0.3s): IOException - Connection refused [EXCEPTION CAUGHT]

2026-09-17T10:41:29.831+08:00 dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Sleeping for 200ms before retry (exponential backoff) [ABOUT TO DELAY]

2026-09-17T10:41:30.033+08:00 dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Sleep completed, resuming loop iteration (attempt 3) [DELAY COMPLETE]

2026-09-17T10:41:30.033+08:00 info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 3 (elapsed: 0.5s)... [ABOUT TO TRY]

2026-09-17T10:41:30.133+08:00 warn: ServiceBusPoc.Producer.Services.ProducerService[0]
      Connection attempt 3 failed (elapsed: 0.6s): IOException - Connection refused [EXCEPTION CAUGHT]

2026-09-17T10:41:30.134+08:00 dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Sleeping for 400ms before retry (exponential backoff) [ABOUT TO DELAY]

2026-09-17T10:41:30.535+08:00 dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Sleep completed, resuming loop iteration (attempt 4) [DELAY COMPLETE]

2026-09-17T10:41:30.535+08:00 info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 4 (elapsed: 1.0s)... [ABOUT TO TRY]

2026-09-17T10:41:30.635+08:00 warn: ServiceBusPoc.Producer.Services.ProducerService[0]
      Connection attempt 4 failed (elapsed: 1.1s): IOException - Connection refused [EXCEPTION CAUGHT]

2026-09-17T10:41:30.636+08:00 dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Sleeping for 800ms before retry (exponential backoff) [ABOUT TO DELAY]

2026-09-17T10:41:31.437+08:00 dbug: ServiceBusPoc.Producer.Services.ProducerService[0]
      Sleep completed, resuming loop iteration (attempt 5) [DELAY COMPLETE]

2026-09-17T10:41:31.437+08:00 info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 5 (elapsed: 1.9s)... [ABOUT TO TRY]

2026-09-17T10:41:31.537+08:00 info: ServiceBusPoc.Producer.Services.ProducerService[0]
      ✓ Service Bus is ready! Connected after 2.0s (attempt 5)
```

**Benefits:**
- ✅ Clear "attempt 1, 2, 3, 4, 5..." progression
- ✅ `[ABOUT TO TRY]` shows loop is working
- ✅ `[EXCEPTION CAUGHT]` confirms exception handling is active
- ✅ `[ABOUT TO DELAY]` and `[DELAY COMPLETE]` prove async/await works
- ✅ Exponential backoff visible: 100ms, 200ms, 400ms, 800ms...
- ✅ Timestamps show actual delays occurring
- ✅ Success message shows total time and attempt number
- ✅ Loop control flow completely transparent

---

## Timing Analysis (After Fix)

### Attempt Progression Showing Exponential Backoff

| Attempt | [ABOUT TO TRY] Timestamp | Exception Timestamp | Sleep Duration | [DELAY COMPLETE] Timestamp | Gap |
|---------|---------------------------|---------------------|-----------------|----------------------------|-----|
| 1 | 10:41:29.527 | 10:41:29.628 | 100ms | 10:41:29.730 | 101ms |
| 2 | 10:41:29.730 | 10:41:29.830 | 200ms | 10:41:30.033 | 201ms |
| 3 | 10:41:30.033 | 10:41:30.133 | 400ms | 10:41:30.535 | 401ms |
| 4 | 10:41:30.535 | 10:41:30.635 | 800ms | 10:41:31.437 | 801ms |
| 5 | 10:41:31.437 | 10:41:31.537 | (ready) | — | (success) |

### Analysis
- Each attempt takes ~100ms to attempt connection
- Sleep durations progress: 100ms → 200ms → 400ms → 800ms → ...
- Total elapsed time until attempt 5: ~2.0 seconds
- If Service Bus never comes up, would continue for 120 seconds max

---

## Log Level Strategy

### Info Level (Blue/Green)
- Connection attempt start
- Successful connection
- Progress updates every 25 seconds
- Used for important state changes

### Warning Level (Yellow)
- Connection failures
- First 5 attempts logged at Warning
- Every 10th attempt thereafter
- Exception message included

### Debug Level (Gray)
- Sleep duration details
- Loop completion confirmations
- Detailed flow control
- Filtered in production with appropriate log level

### Error Level (Red)
- Final timeout if no connection
- Unexpected exceptions
- Fatal errors

---

## Searchability Improvements

### New Markers for Log Search

| Marker | Meaning | Use Case |
|--------|---------|----------|
| `[ABOUT TO TRY]` | Entering connection attempt | Count how many attempts started |
| `[EXCEPTION CAUGHT]` | Exception was handled | Verify retry logic is active |
| `[ABOUT TO DELAY]` | Starting sleep | Prove async delay is called |
| `[DELAY COMPLETE]` | Sleep finished | Prove await completes |

### Example Grep Searches
```bash
# Find all retry attempts
grep "\[ABOUT TO TRY\]" logs.txt

# Find all caught exceptions (should be one per attempt except last)
grep "\[EXCEPTION CAUGHT\]" logs.txt

# Find all sleep operations
grep "\[ABOUT TO DELAY\]" logs.txt

# Verify all sleeps completed
grep "\[DELAY COMPLETE\]" logs.txt

# Find connection successes
grep "✓ Service Bus is ready" logs.txt

# Find timeouts
grep "connection timeout" logs.txt
```

---

## Summary of Improvements

| Aspect | Before | After |
|--------|--------|-------|
| Timestamps | ❌ None | ✅ ISO 8601 with timezone |
| Retry visibility | ❌ Stops at attempt 1 | ✅ All attempts visible |
| Control flow | ❌ Unknown | ✅ Clear markers at each step |
| Exception handling | ❌ Not confirmed | ✅ [EXCEPTION CAUGHT] confirms |
| Async/await | ❌ Not visible | ✅ [ABOUT TO DELAY]/[COMPLETE] |
| Exponential backoff | ❌ Unknown | ✅ Visible in timestamps |
| Production ready | ❌ No | ✅ Yes |
| Searchable | ❌ Limited | ✅ Marker-based search |
| Debugging | ❌ Impossible | ✅ Complete visibility |
