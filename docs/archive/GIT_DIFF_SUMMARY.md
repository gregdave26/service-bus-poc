# Git Diff Summary - Logging Diagnostics Fix

## File Changes Overview

### NEW FILES (1)
```
src/ServiceBusPoc.Core/Logging/TimestampedConsoleFormatter.cs
  - New custom console formatter with ISO 8601 timestamps
  - 74 lines of code
  - Formats: "2026-09-17T10:41:29.527+08:00 info: Category[EventId] Message"
```

### MODIFIED FILES (3)

#### 1. src/ServiceBusPoc.Core/Logging/LoggingExtensions.cs
```diff
  using Microsoft.Extensions.Logging;
+ using Microsoft.Extensions.Logging.Console;

  namespace ServiceBusPoc.Core.Logging;

  public static class LoggingExtensions
  {
-     public static ILoggingBuilder AddStructuredConsoleLogging(this ILoggingBuilder builder)
+     /// <summary>
+     /// Adds console logging with structured format suitable for development and debugging.
+     /// Includes ISO 8601 timestamps with timezone offset.
+     /// </summary>
+     public static ILoggingBuilder AddStructuredConsoleLogging(this ILoggingBuilder builder)
      {
          builder.ClearProviders();
-         builder.AddConsole(options =>
+         
+         // Register the custom timestamped formatter
+         builder.AddConsole(options =>
          {
-             options.FormatterName = "simple";
+             options.FormatterName = TimestampedConsoleFormatter.FormatterName;
          });
+         
+         // Add the formatter configuration
+         builder.AddConsoleFormatter<TimestampedConsoleFormatter, SimpleConsoleFormatterOptions>();

          return builder;
      }
  }
```

Changes:
- Added `using Microsoft.Extensions.Logging.Console;` import
- Updated docstring to mention timestamps
- Changed formatter name from "simple" to `TimestampedConsoleFormatter.FormatterName`
- Added formatter registration via `AddConsoleFormatter<...>()`

#### 2. src/ServiceBusPoc.Producer/Services/ProducerService.cs
```diff
  private async Task<bool> WaitForServiceBusReadyAsync(DateTimeOffset startTime, CancellationToken cancellationToken)
  {
      // ... setup code ...
      while ((DateTimeOffset.UtcNow - startTime) < maxWaitDuration)
      {
          attempt++;
+         var elapsedAtCheck = DateTimeOffset.UtcNow - startTime;
+         
          try
          {
-             // Log connection attempts with better visibility
-             var elapsedAtCheck = DateTimeOffset.UtcNow - startTime;
-             
-             // Log first 10 attempts, then every 5th attempt...
-             if (attempt <= 10 || attempt % 5 == 0)
-             {
-                 _logger.LogInformation(
-                     "Service Bus connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)...",
-                     attempt,
-                     elapsedAtCheck.TotalSeconds);
-             }
+             // Log connection attempt start
+             _logger.LogInformation(
+                 "Service Bus connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)... [ABOUT TO TRY]",
+                 attempt,
+                 elapsedAtCheck.TotalSeconds);

              // Try to create a test contact to verify the Service Bus is ready
              var testContact = new ContactData
              {
                  ContactId = $"probe-{attempt}",
                  FirstName = "Probe",
                  LastName = "Test"
              };

              await _publisher.PublishContactUpdatedAsync(
                  testContact,
                  "producer-probe",
                  correlationId: $"probe-{attempt}",
                  cancellationToken);

              var elapsedAtSuccess = DateTimeOffset.UtcNow - startTime;
              _logger.LogInformation(
                  "✓ Service Bus is ready! Connected after {ElapsedSeconds:F1}s (attempt {Attempt})",
                  elapsedAtSuccess.TotalSeconds,
                  attempt);
              return true;
          }
+         catch (OperationCanceledException ex)
+         {
+             // Log cancellation
+             _logger.LogInformation(
+                 ex,
+                 "Service Bus connection attempt {Attempt} cancelled (elapsed: {ElapsedSeconds:F1}s)",
+                 attempt,
+                 elapsedAtCheck.TotalSeconds);
+             throw;
+         }
          catch (Exception ex)
          {
-             // Log errors for first 10 attempts + every 10th after...
+             // Log exception immediately after catch
              var elapsedAtError = DateTimeOffset.UtcNow - startTime;
-             if (attempt <= 10 || attempt % 10 == 0)
-             {
-                 _logger.LogDebug(
-                     ex,
-                     "Connection attempt {Attempt} failed (elapsed: {ElapsedSeconds:F1}s): {ExceptionType}",
-                     attempt,
-                     elapsedAtError.TotalSeconds,
-                     ex.GetType().Name);
-             }
+             _logger.LogWarning(
+                 ex,
+                 "Connection attempt {Attempt} failed (elapsed: {ElapsedSeconds:F1}s): {ExceptionType} - {Message} [EXCEPTION CAUGHT]",
+                 attempt,
+                 elapsedAtError.TotalSeconds,
+                 ex.GetType().Name,
+                 ex.Message);

              // Log intermediate progress every 25 seconds...
              if ((elapsedAtError - TimeSpan.FromSeconds(ProgressLogIntervalSeconds)) >= (lastProgressLogTime - startTime))
              {
                  _logger.LogInformation(
                      "Still waiting for Service Bus... (elapsed: {ElapsedSeconds:F1}s, attempt {Attempt})",
                      elapsedAtError.TotalSeconds,
                      attempt);
                  lastProgressLogTime = DateTimeOffset.UtcNow;
              }

              // Exponential backoff: cap at ~12.8s to avoid excessive waits
              var delayMs = (int)Math.Min(currentDelay.TotalMilliseconds, 12800);
+             
+             _logger.LogDebug(
+                 "Sleeping for {DelayMs}ms before retry (exponential backoff) [ABOUT TO DELAY]",
+                 delayMs);
+             
              await Task.Delay(delayMs, cancellationToken);
+             
+             _logger.LogDebug(
+                 "Sleep completed, resuming loop iteration (attempt {Attempt}) [DELAY COMPLETE]",
+                 attempt + 1);

              currentDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * 2);
          }
      }

-     return false;
+     _logger.LogError(
+         "Service Bus connection timeout after {TotalSeconds:F1}s and {Attempts} attempts",
+         maxWaitDuration.TotalSeconds,
+         attempt);
+
+     return false;
```

Changes:
- Moved `elapsedAtCheck` declaration outside try block
- Updated attempt start log to include `[ABOUT TO TRY]` marker
- Added separate `catch (OperationCanceledException)` block
- Changed exception log from Debug to Warning level
- Added exception message to exception log
- Added `[EXCEPTION CAUGHT]` marker to exception log
- Added `[ABOUT TO DELAY]` log before Task.Delay()
- Added `[DELAY COMPLETE]` log after Task.Delay()
- Added final timeout error log

#### 3. src/ServiceBusPoc.Core/Messaging/SubscriptionConsumerRunner.cs
```diff
  public async Task<bool> WaitForReadyAsync(DateTimeOffset startTime, CancellationToken cancellationToken)
  {
      var maxWaitDuration = TimeSpan.FromSeconds(120);
      var initialDelay = TimeSpan.FromMilliseconds(100);
      var currentDelay = initialDelay;
      var attempt = 0;

      while ((DateTimeOffset.UtcNow - startTime) < maxWaitDuration)
      {
          attempt++;
+         var elapsedAtCheck = DateTimeOffset.UtcNow - startTime;
+         
          try
          {
-             // Log every attempt
-             var elapsedAtCheck = DateTimeOffset.UtcNow - startTime;
-             if (attempt % 10 == 1)  // Log attempts 1, 11, 21, etc
+             // Log connection attempt start - every attempt for first 5, then every 10th
+             if (attempt <= 5 || attempt % 10 == 0)
              {
                  _logger.LogInformation(
-                     "Service Bus subscription connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)...",
+                     "Service Bus subscription connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)... [ABOUT TO TRY]",
                      attempt,
                      elapsedAtCheck.TotalSeconds);
              }
-             else if (attempt % 10 == 0)
+             else
              {
                  _logger.LogDebug(
                      "Service Bus subscription connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)...",
                      attempt,
                      elapsedAtCheck.TotalSeconds);
              }

              // Try to receive with a short timeout to verify the subscription is accessible
              await _receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(100), cancellationToken);

              var elapsedAtSuccess = DateTimeOffset.UtcNow - startTime;
              _logger.LogInformation(
                  "✓ Service Bus subscription is ready! Connected after {ElapsedSeconds:F1}s (attempt {Attempt})",
                  elapsedAtSuccess.TotalSeconds,
                  attempt);
              return true;
          }
+         catch (OperationCanceledException ex)
+         {
+             // Log cancellation
+             _logger.LogInformation(
+                 ex,
+                 "Service Bus subscription connection attempt {Attempt} cancelled (elapsed: {ElapsedSeconds:F1}s)",
+                 attempt,
+                 elapsedAtCheck.TotalSeconds);
+             throw;
+         }
          catch (Exception ex)
          {
-             // Only log errors less frequently to avoid spam
-             if (attempt <= 5 || attempt % 20 == 0)
+             // Log exceptions immediately after catch for first 5 attempts and every 10th
+             var elapsedAtError = DateTimeOffset.UtcNow - startTime;
+             if (attempt <= 5 || attempt % 10 == 0)
              {
-                 var elapsedAtError = DateTimeOffset.UtcNow - startTime;
-                 _logger.LogDebug(
+                 _logger.LogWarning(
                      ex,
-                     "Connection attempt {Attempt} failed (elapsed: {ElapsedSeconds:F1}s): {ExceptionType}",
+                     "Connection attempt {Attempt} failed (elapsed: {ElapsedSeconds:F1}s): {ExceptionType} - {Message} [EXCEPTION CAUGHT]",
                      attempt,
                      elapsedAtError.TotalSeconds,
-                     ex.GetType().Name);
+                     ex.GetType().Name,
+                     ex.Message);
+             }
+             else
+             {
+                 _logger.LogDebug(
+                     ex,
+                     "Connection attempt {Attempt} failed (elapsed: {ElapsedSeconds:F1}s): {ExceptionType}",
+                     attempt,
+                     elapsedAtError.TotalSeconds,
+                     ex.GetType().Name);
              }

              // Exponential backoff: cap at ~12.8s to avoid excessive waits
              var delayMs = (int)Math.Min(currentDelay.TotalMilliseconds, 12800);
+             
+             _logger.LogDebug(
+                 "Sleeping for {DelayMs}ms before retry (exponential backoff) [ABOUT TO DELAY]",
+                 delayMs);
+             
              await Task.Delay(delayMs, cancellationToken);
+             
+             _logger.LogDebug(
+                 "Sleep completed, resuming loop iteration (attempt {Attempt}) [DELAY COMPLETE]",
+                 attempt + 1);

              currentDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * 2);
          }
      }

+     _logger.LogError(
+         "Service Bus subscription connection timeout after {TotalSeconds:F1}s and {Attempts} attempts",
+         maxWaitDuration.TotalSeconds,
+         attempt);
+
      return false;
  }
```

Changes:
- Moved `elapsedAtCheck` declaration outside try block
- Updated logging condition from `attempt % 10 == 1` to `attempt <= 5 || attempt % 10 == 0`
- Added `[ABOUT TO TRY]` marker to attempt log
- Added separate `catch (OperationCanceledException)` block
- Changed exception log from Debug to Warning for first 5 and every 10th
- Added else clause to log Debug for remaining attempts
- Added exception message to warning logs
- Added `[EXCEPTION CAUGHT]` marker to exception logs
- Added `[ABOUT TO DELAY]` log before Task.Delay()
- Added `[DELAY COMPLETE]` log after Task.Delay()
- Added final timeout error log

---

## Statistics

### Code Changes
- **New files**: 1
- **Modified files**: 3
- **Lines added**: ~150
- **Lines removed**: ~40
- **Net change**: ~110 lines added

### Test Coverage
- Both retry methods now have defensive logging
- Both producer and consumer services affected
- Changes are backward compatible
- No public API changes

### Performance Impact
- Minimal - logging is already async
- Timestamp computation using `DateTimeOffset.Now` is efficient
- No new I/O operations added
- No blocking operations added

---

## Deployment Notes

### Prerequisites
- .NET 8.0 or later (unchanged)
- No NuGet package updates required
- No configuration changes needed

### Breaking Changes
- None - fully backward compatible
- Existing applications will automatically get timestamps
- No code changes needed in calling code

### Rollback Plan
- Revert to commit before this change
- Change formatter name back to "simple" in LoggingExtensions.cs
- Delete TimestampedConsoleFormatter.cs

### Verification
Run locally:
```bash
cd src/ServiceBusPoc.Producer
dotnet run
```

Expected output: All logs have ISO 8601 timestamp prefix

---

## Related Issues

- Fixes: Cannot see retry progression in Service Bus connection
- Fixes: No timestamp information for debugging
- Enables: Production diagnostics of startup delays
- Enables: Cross-service event correlation
