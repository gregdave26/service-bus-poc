using Microsoft.Extensions.Logging;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Provides the common lifecycle for a service that consumes one Service Bus subscription.
/// </summary>
public abstract class AbstractConsumerService
{
    private readonly SubscriptionConsumerRunner _consumerRunner;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a consumer service.
    /// </summary>
    /// <param name="consumerRunner">Runner that receives and processes subscription messages.</param>
    /// <param name="logger">Logger for lifecycle events.</param>
    protected AbstractConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger logger)
    {
        _consumerRunner = consumerRunner;
        _logger = logger;
    }

    /// <summary>
    /// Gets the descriptor used to identify this consumer.
    /// </summary>
    protected abstract ConsumerDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the name used in lifecycle log messages.
    /// </summary>
    protected abstract string ServiceName { get; }

    /// <summary>
    /// Waits for the subscription and runs the consumer until cancellation.
    /// </summary>
    /// <param name="cancellationToken">Token used to stop the consumer.</param>
    /// <returns>A task representing the consumer lifecycle.</returns>
    protected async Task RunConsumerAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{ServiceName} consumer service starting...", ServiceName);
        var startTime = DateTimeOffset.UtcNow;

        var connected = await _consumerRunner.WaitForReadyAsync(startTime, cancellationToken);
        if (!connected)
        {
            _logger.LogError("Service Bus subscription did not become ready within timeout");
            throw new InvalidOperationException("Service Bus subscription did not become ready within timeout");
        }

        try
        {
            await _consumerRunner.RunAsync(Descriptor, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("{ServiceName} consumer service cancelled", ServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ServiceName} consumer service encountered an error", ServiceName);
            throw;
        }
    }
}
