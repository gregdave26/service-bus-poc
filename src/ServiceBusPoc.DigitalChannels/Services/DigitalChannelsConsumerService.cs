using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.DigitalChannels.Services;

/// <summary>
/// Consumes all contact events from the <c>digital-channels</c> subscription.
/// DigitalChannels has no capability filter; receives all contact updates.
/// Logs each received message to the console and reports heartbeats to the dashboard.
/// </summary>
public sealed class DigitalChannelsConsumerService
{
    private readonly SubscriptionConsumerRunner _consumerRunner;
    private readonly ILogger<DigitalChannelsConsumerService> _logger;

    public DigitalChannelsConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger<DigitalChannelsConsumerService> logger)
    {
        _consumerRunner = consumerRunner;
        _logger = logger;
    }

    /// <summary>
    /// Runs the consumer service, listening for messages on the digital-channels subscription.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Digital Channels consumer service starting...");
        var startTime = DateTimeOffset.UtcNow;

        // Wait for the Service Bus subscription to be ready with exponential backoff
        var connected = await _consumerRunner.WaitForReadyAsync(startTime, cancellationToken);
        if (!connected)
        {
            _logger.LogError("Service Bus subscription did not become ready within timeout");
            throw new InvalidOperationException("Service Bus subscription did not become ready within timeout");
        }

        var descriptor = new ConsumerDescriptor("digital-channels", ConsumerDescriptor.NoFilter);

        try
        {
            await _consumerRunner.RunAsync(descriptor, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Digital Channels consumer service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Digital Channels consumer service encountered an error");
            throw;
        }
    }
}
