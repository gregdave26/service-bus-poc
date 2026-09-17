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
