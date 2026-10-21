using Azure.Messaging.ServiceBus;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Coordinates message reception, processing, and error handling.
/// Allows receive-loop logic to be shared across multiple runner implementations.
/// </summary>
public interface IMessageReceiveOrchestrator
{
    /// <summary>
    /// Processes a message received from the subscription.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">Token used to cancel processing.</param>
    /// <returns>A task that completes when processing is done.</returns>
    Task ProcessMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Handles the timeout case where no message was received in the wait interval.
    /// Implementations may report heartbeat or update metrics here.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel operations.</param>
    /// <returns>A task that completes when timeout handling is done.</returns>
    Task OnEmptyReceiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Handles a failure during message reception (network error, broker issue, etc).
    /// Implementations may log, update metrics, or take corrective action.
    /// </summary>
    /// <param name="exception">The exception that occurred during receive.</param>
    /// <param name="cancellationToken">Token used to cancel operations.</param>
    /// <returns>A task that completes when failure handling is done.</returns>
    Task OnReceiveFailureAsync(ServiceBusException exception, CancellationToken cancellationToken);
}
