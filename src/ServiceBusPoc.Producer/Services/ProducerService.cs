using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Producer.Services;

/// <summary>
/// Publishes sample contact events to the Service Bus topic.
/// Demonstrates the messaging contract by publishing events with various capability flags.
/// Heartbeats are automatically reported via the injected IDashboardReporter.
/// </summary>
public sealed class ProducerService
{
    private readonly ContactEventPublisher _publisher;
    private readonly IDashboardReporter _dashboardReporter;
    private readonly ILogger<ProducerService> _logger;
    private readonly TimeProvider _timeProvider;

    public ProducerService(
        ContactEventPublisher publisher,
        IDashboardReporter dashboardReporter,
        ILogger<ProducerService> logger,
        TimeProvider timeProvider)
    {
        _publisher = publisher;
        _dashboardReporter = dashboardReporter;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Publishes sample events at regular intervals.
    /// Includes retry logic with exponential backoff to handle emulator startup time.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Producer service starting...");
        var startTime = _timeProvider.GetUtcNow();

        // Wait for the Service Bus to be ready with exponential backoff
        var connected = await WaitForServiceBusReadyAsync(startTime, cancellationToken);
        if (!connected)
        {
            _logger.LogError("Service Bus did not become ready within timeout");
            throw new InvalidOperationException("Service Bus did not become ready within timeout");
        }

        await ReportHeartbeatAsync(ServiceState.Online, 0, null, null);

        try
        {
            var messageCount = 0L;
            var delayBetweenPublishes = TimeSpan.FromSeconds(3);

            while (!cancellationToken.IsCancellationRequested)
            {
                await PublishSampleEventAsync(messageCount, cancellationToken);
                messageCount++;

                await ReportHeartbeatAsync(
                    ServiceState.Online,
                    messageCount,
                    _timeProvider.GetUtcNow(),
                    $"event-{messageCount}");

                await Task.Delay(delayBetweenPublishes, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Producer service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Producer service encountered an error");
            throw;
        }
        finally
        {
            await ReportHeartbeatAsync(ServiceState.Offline, 0, null, null);
        }
    }

    /// <summary>
    /// Waits for the Service Bus to be ready by attempting to publish a probe message.
    /// Uses exponential backoff: 100ms, 200ms, 400ms, 800ms, 1.6s, 3.2s, 6.4s, 12.8s, 25.6s, 51.2s max.
    /// </summary>
    private async Task<bool> WaitForServiceBusReadyAsync(DateTimeOffset startTime, CancellationToken cancellationToken)
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
                // Log every attempt
                var elapsedAtCheck = DateTimeOffset.UtcNow - startTime;
                if (attempt % 10 == 1)  // Log attempts 1, 11, 21, etc
                {
                    _logger.LogInformation(
                        "Service Bus connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)...",
                        attempt,
                        elapsedAtCheck.TotalSeconds);
                }
                else if (attempt % 10 == 0)
                {
                    _logger.LogDebug(
                        "Service Bus connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)...",
                        attempt,
                        elapsedAtCheck.TotalSeconds);
                }

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
            catch (Exception ex)
            {
                // Only log errors less frequently to avoid spam
                if (attempt <= 5 || attempt % 20 == 0)
                {
                    var elapsedAtError = DateTimeOffset.UtcNow - startTime;
                    _logger.LogDebug(
                        ex,
                        "Connection attempt {Attempt} failed (elapsed: {ElapsedSeconds:F1}s): {ExceptionType}",
                        attempt,
                        elapsedAtError.TotalSeconds,
                        ex.GetType().Name);
                }

                // Exponential backoff: cap at ~12.8s to avoid excessive waits
                var delayMs = (int)Math.Min(currentDelay.TotalMilliseconds, 12800);
                await Task.Delay(delayMs, cancellationToken);

                currentDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * 2);
            }
        }

        return false;
    }

    private async Task PublishSampleEventAsync(long index, CancellationToken cancellationToken)
    {
        // Vary the capabilities across published events to exercise subscription filtering
        var hasInsurance = index % 3 == 0;
        var hasParksResorts = index % 2 == 0;
        var hasCarwashProduct = index % 5 == 0;

        var contact = new ContactData
        {
            ContactId = $"contact-{index:D4}",
            FirstName = $"Customer{index % 10}",
            LastName = $"Test{index % 100}",
            Phone = $"+1-555-{1000 + (index % 9000):D4}",
            Email = $"contact{index}@example.com",
            Attributes = new ContactAttributes
            {
                HasInsurance = hasInsurance,
                HasParksResorts = hasParksResorts,
                HasCarwashProduct = hasCarwashProduct
            }
        };

        try
        {
            await _publisher.PublishContactUpdatedAsync(contact, "producer", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish sample event {Index}", index);
            throw;
        }
    }

    private async Task ReportHeartbeatAsync(
        ServiceState state,
        long messagesPublished,
        DateTimeOffset? lastMessageAt,
        string? lastEventId)
    {
        var heartbeat = new ServiceHeartbeat
        {
            ServiceName = "producer",
            State = state,
            SentAt = _timeProvider.GetUtcNow(),
            MessagesHandled = messagesPublished,
            LastMessageAt = lastMessageAt,
            LastEventId = lastEventId
        };

        await _dashboardReporter.ReportAsync(heartbeat);
    }
}
