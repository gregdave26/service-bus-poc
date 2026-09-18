# Fix: Logging Diagnostics - Timestamps and Retry Loop Visibility

## Overview
This PR fixes two critical logging issues that prevent proper debugging and monitoring of Service Bus connection retry logic:

1. **Missing Timestamps** - No timestamp on any log entries
2. **Retry Loop Not Visible** - Cannot see retry progression from attempt 1 to attempt 2+

## Changes Made

### Issue 1: Add ISO 8601 Timestamps to Console Logs

#### New File: `src/ServiceBusPoc.Core/Logging/TimestampedConsoleFormatter.cs`
- Custom `ConsoleFormatter` implementation
- Formats each log entry with ISO 8601 timestamp with timezone offset
- Example output: `2026-09-17T10:41:29.527+08:00 info: Category[EventId] Message`
- Preserves structured logging output format
- Uses standard .NET log level abbreviations (trce, dbug, info, warn, fail, crit)

#### Modified: `src/ServiceBusPoc.Core/Logging/LoggingExtensions.cs`
- Updated `AddStructuredConsoleLogging()` to register `TimestampedConsoleFormatter`
- Uses `options.FormatterName = TimestampedConsoleFormatter.FormatterName`
- Registers formatter via `AddConsoleFormatter<TimestampedConsoleFormatter, SimpleConsoleFormatterOptions>()`

### Issue 2: Add Defensive Logging to Retry Loops

#### Modified: `src/ServiceBusPoc.Producer/Services/ProducerService.cs`
Enhanced `WaitForServiceBusReadyAsync()` with defensive logging markers:

- `[ABOUT TO TRY]` - Log before attempting connection
- `[EXCEPTION CAUGHT]` - Log immediately after exception caught
- `[ABOUT TO DELAY]` - Log before `Task.Delay()`
- `[DELAY COMPLETE]` - Log after `Task.Delay()` completes
- Separate handling for `OperationCanceledException` vs general exceptions
- Final error log if timeout reached

Logging strategy:
- **First attempt**: Always logs connection attempt start
- **Exceptions**: Warning level for first 10 attempts, then Debug for others
- **Timing**: Sleep duration and completion clearly logged
- **Timeout**: Error logged if 120s elapsed without success

#### Modified: `src/ServiceBusPoc.Core/Messaging/SubscriptionConsumerRunner.cs`
Enhanced `WaitForReadyAsync()` with same defensive logging pattern:

- Same control flow markers: `[ABOUT TO TRY]`, `[EXCEPTION CAUGHT]`, `[ABOUT TO DELAY]`, `[DELAY COMPLETE]`
- Info level logs for first 5 attempts + every 10th
- Debug level logs for remaining attempts
- Exception message included in Warning logs
- Clear timeout detection and error logging

## Log Output Examples

### Before Fix
```
info: ServiceBusPoc.Producer.Services.ProducerService[0]
      Service Bus connection attempt 1 (elapsed: 0.0s)...
```
(No timestamp, no visibility into retry progression)

### After Fix
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
```

(Clear timestamps, visible retry progression, control flow markers)

## Verification

### Acceptance Criteria Met ✅

- ✅ **Timestamps on all logs**: ISO 8601 format with timezone (`2026-09-17T10:41:29.527+08:00`)
- ✅ **Retry loop visible**: Can see progression from attempt 1 → 2 → 3 → ...
- ✅ **Exponential backoff visible**: Delay progression (100ms, 200ms, 400ms, 800ms...)
- ✅ **Control flow clarity**: Markers show exact execution path through retry logic
  - `[ABOUT TO TRY]` - entering attempt block
  - `[EXCEPTION CAUGHT]` - caught exception, will retry
  - `[ABOUT TO DELAY]` - about to sleep
  - `[DELAY COMPLETE]` - sleep finished, continuing loop
- ✅ **Exception details**: Message and type included in logs
- ✅ **Timeout detection**: Clear error message if max attempts exceeded

## Technical Notes

### Formatter Design
- Uses `DateTimeOffset.Now.ToString("O", ...)` for ISO 8601 with timezone
- "O" format produces: `2026-09-17T10:41:29.527+08:00`
- Works cross-platform (Windows, Linux, macOS)
- Includes millisecond precision for accurate timing

### Retry Loop Markers
- Markers use `[UPPERCASE]` format to make them easily searchable in logs
- Debug level used for flow control to avoid spamming production logs
- First 5 attempts logged at Info/Warning for visibility
- After 5 attempts, drops to Debug unless every 10th

### Exception Handling
- Separate catch block for `OperationCanceledException` to log cancellation
- General `Exception` catch logs warning with exception details
- Both logged before `Task.Delay()` to ensure exception doesn't escape

## Build & Testing

### Build Status
- All files compile without errors
- No breaking changes to public APIs
- No external dependencies added

### Local Testing
Run with Service Bus emulator:
```bash
cd src/ServiceBusPoc.Producer
dotnet run
```

Expected output: Timestamps on all lines, retry progression visible

## Files Changed
- **Created**: `src/ServiceBusPoc.Core/Logging/TimestampedConsoleFormatter.cs`
- **Modified**: `src/ServiceBusPoc.Core/Logging/LoggingExtensions.cs`
- **Modified**: `src/ServiceBusPoc.Producer/Services/ProducerService.cs`
- **Modified**: `src/ServiceBusPoc.Core/Messaging/SubscriptionConsumerRunner.cs`

## Related Issues
- Addresses logging visibility issues in connection retry logic
- Enables proper debugging of Service Bus startup delays
- Required for production monitoring and diagnostics
