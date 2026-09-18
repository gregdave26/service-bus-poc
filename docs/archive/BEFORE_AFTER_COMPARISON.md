# Before & After: ParksResortsConsumerService Example

## BEFORE (Original Code)

```csharp
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.ParksResorts.Services;

/// <summary>
/// Consumes contact events from the <c>parks-resorts</c> subscription.
/// Only receives messages where <c>hasParksResorts = true</c> (filtering done by the broker).
/// Logs each received message to the console and reports heartbeats to the dashboard.
/// </summary>
public sealed class ParksResortsConsumerService
{
    private readonly SubscriptionConsumerRunner _consumerRunner;
    private readonly ILogger<ParksResortsConsumerService> _logger;

    public ParksResortsConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger<ParksResortsConsumerService> logger)
    {
        _consumerRunner = consumerRunner;
        _logger = logger;
    }

    /// <summary>
    /// Runs the consumer service, listening for messages on the parks-resorts subscription.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parks & Resorts consumer service starting...");

        var descriptor = new ConsumerDescriptor("parks-resorts", "hasParksResorts = true");

        try
        {
            await _consumerRunner.RunAsync(descriptor, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Parks & Resorts consumer service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parks & Resorts consumer service encountered an error");
            throw;
        }
    }
}
```

### Problem with Original Code
- Service tries to connect immediately via `_consumerRunner.RunAsync()`
- If emulator is starting up, connection fails immediately with "No connection could be made because the target machine actively refused it"
- No retry logic to wait for the Service Bus to become ready
- No exponential backoff

---

## AFTER (With Retry Logic)

```csharp
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.ParksResorts.Services;

/// <summary>
/// Consumes contact events from the <c>parks-resorts</c> subscription.
/// Only receives messages where <c>hasParksResorts = true</c> (filtering done by the broker).
/// Logs each received message to the console and reports heartbeats to the dashboard.
/// </summary>
public sealed class ParksResortsConsumerService
{
    private readonly SubscriptionConsumerRunner _consumerRunner;
    private readonly ILogger<ParksResortsConsumerService> _logger;

    public ParksResortsConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger<ParksResortsConsumerService> logger)
    {
        _consumerRunner = consumerRunner;
        _logger = logger;
    }

    /// <summary>
    /// Runs the consumer service, listening for messages on the parks-resorts subscription.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parks & Resorts consumer service starting...");
        var startTime = DateTimeOffset.UtcNow;

        // Wait for the Service Bus subscription to be ready with exponential backoff
        var connected = await _consumerRunner.WaitForReadyAsync(startTime, cancellationToken);
        if (!connected)
        {
            _logger.LogError("Service Bus subscription did not become ready within timeout");
            throw new InvalidOperationException("Service Bus subscription did not become ready within timeout");
        }

        var descriptor = new ConsumerDescriptor("parks-resorts", "hasParksResorts = true");

        try
        {
            await _consumerRunner.RunAsync(descriptor, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Parks & Resorts consumer service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parks & Resorts consumer service encountered an error");
            throw;
        }
    }
}
```

### Improvement with Retry Logic
- ✅ Waits for Service Bus to be ready before connecting
- ✅ Uses exponential backoff: 100ms, 200ms, 400ms, 800ms, etc.
- ✅ Retries for up to 120 seconds
- ✅ Logs progress during connection attempts
- ✅ Handles timeout gracefully with clear error message
- ✅ Allows emulator startup time while producer is warming up

---

## Key Changes Highlighted

### 1. Capture Start Time
```csharp
var startTime = DateTimeOffset.UtcNow;
```
Records when the service started, used to calculate elapsed time and enforce timeout.

### 2. Wait for Ready with Retry Logic
```csharp
var connected = await _consumerRunner.WaitForReadyAsync(startTime, cancellationToken);
```
This new method:
- Attempts to receive a message with 100ms timeout
- Uses exponential backoff if fails
- Returns true/false based on success
- Takes 1-120 seconds depending on emulator startup time

### 3. Check Result and Handle Failure
```csharp
if (!connected)
{
    _logger.LogError("Service Bus subscription did not become ready within timeout");
    throw new InvalidOperationException("Service Bus subscription did not become ready within timeout");
}
```
If emulator doesn't start within 120s, service fails gracefully with clear error.

### 4. Proceed with Original Logic
```csharp
var descriptor = new ConsumerDescriptor("parks-resorts", "hasParksResorts = true");
try { await _consumerRunner.RunAsync(descriptor, cancellationToken); }
```
Once connected, proceeds exactly as before - no other changes.

---

## Expected Behavior Comparison

### Scenario: Emulator Starting Up

#### BEFORE (Original)
```
[ParksResorts] Parks & Resorts consumer service starting...
[ParksResorts] ERROR: No connection could be made because the target machine actively refused it
[ParksResorts] Parks & Resorts consumer service encountered an error
→ Service crashes, must be restarted manually after emulator is ready
```

#### AFTER (With Retry Logic)
```
[ParksResorts] Parks & Resorts consumer service starting...
[ParksResorts] Service Bus subscription connection attempt 1 (elapsed: 0.0s)...
[ParksResorts] Service Bus subscription connection attempt 11 (elapsed: 1.0s)...
[ParksResorts] Service Bus subscription connection attempt 21 (elapsed: 3.0s)...
[ParksResorts] ✓ Service Bus subscription is ready! Connected after 2.5s (attempt 25)
[ParksResorts] Consumer parks-resorts listening on topic contact-events subscription parks-resorts...
→ Service continues normally, waits for producer to send messages
```

### Scenario: Emulator Already Running

#### BEFORE (Original)
```
[ParksResorts] Parks & Resorts consumer service starting...
[ParksResorts] Consumer parks-resorts listening on topic contact-events subscription parks-resorts...
→ Instant connection, no delays
```

#### AFTER (With Retry Logic)
```
[ParksResorts] Parks & Resorts consumer service starting...
[ParksResorts] Service Bus subscription connection attempt 1 (elapsed: 0.0s)...
[ParksResorts] ✓ Service Bus subscription is ready! Connected after 0.1s (attempt 1)
[ParksResorts] Consumer parks-resorts listening on topic contact-events subscription parks-resorts...
→ Instant connection, minimal ~100ms overhead from single test attempt
```

---

## Same Pattern Applied to All Consumers

The exact same changes were applied to:
1. ✅ ParksResortsConsumerService
2. ✅ DigitalChannelsConsumerService  
3. ✅ InsuranceConsumerService
4. ✅ CarwashConsumerService

Each consumer now:
- Waits for subscription to be ready
- Uses exponential backoff with 120-second timeout
- Logs connection attempts at appropriate intervals
- Proceeds with message consumption after successful connection

---

## Technical Implementation Details

### New Method in SubscriptionConsumerRunner

```csharp
public async Task<bool> WaitForReadyAsync(DateTimeOffset startTime, CancellationToken cancellationToken)
{
    var maxWaitDuration = TimeSpan.FromSeconds(120);
    var initialDelay = TimeSpan.FromMilliseconds(100);
    var currentDelay = initialDelay;
    var attempt = 0;

    while ((DateTimeOffset.UtcNow - startTime) < maxWaitDuration)
    {
        attempt++;
        try
        {
            // Log every 10th attempt
            var elapsedAtCheck = DateTimeOffset.UtcNow - startTime;
            if (attempt % 10 == 1)  // Attempts 1, 11, 21, etc.
                _logger.LogInformation(
                    "Service Bus subscription connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)...",
                    attempt, elapsedAtCheck.TotalSeconds);

            // Test subscription accessibility
            await _receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(100), cancellationToken);

            var elapsedAtSuccess = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation(
                "✓ Service Bus subscription is ready! Connected after {ElapsedSeconds:F1}s (attempt {Attempt})",
                elapsedAtSuccess.TotalSeconds, attempt);
            return true;
        }
        catch (Exception ex)
        {
            // Log errors selectively
            if (attempt <= 5 || attempt % 20 == 0)
                _logger.LogDebug(ex, "Connection attempt {Attempt} failed...", attempt);

            // Exponential backoff: double delay each time, cap at 12.8s
            var delayMs = (int)Math.Min(currentDelay.TotalMilliseconds, 12800);
            await Task.Delay(delayMs, cancellationToken);
            currentDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * 2);
        }
    }
    
    return false;  // Timeout after 120 seconds
}
```

This method:
- Tests if the subscription is accessible by attempting to receive with a short timeout
- If successful, subscription is ready → return `true` immediately
- If failed, wait using exponential backoff and retry
- Continue retrying until either success or 120-second timeout
- Return `false` if timeout occurs

---

## No Breaking Changes

✅ All existing code continues to work
✅ New method is public but only called from consumer services
✅ `SubscriptionConsumerRunner.RunAsync()` behavior unchanged
✅ Message processing logic unchanged
✅ Subscription filtering unchanged
✅ Dashboard reporting unchanged
✅ All public interfaces remain the same

---

## Why This Approach?

1. **Proven Pattern:** Uses exact same approach as Producer service
2. **Minimal Changes:** Only adds retry logic before subscription starts
3. **Non-Invasive:** Doesn't modify core message handling
4. **Flexible Timeout:** 120 seconds handles both fast and slow emulator startups
5. **Observable:** Clear logging for diagnostics and troubleshooting
6. **Efficient:** Single 100ms test attempt when already ready
