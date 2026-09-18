using Azure.Messaging.ServiceBus;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Abstraction over the Service Bus subscription receiver so consumer logic stays unit-testable.
/// </summary>
public interface IServiceBusReceiver : IAsyncDisposable
{
    /// <summary>
    /// Waits for the next message delivered to this subscription by the broker.
    /// </summary>
    /// <param name="maxWaitTime">How long to wait before returning <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the receive.</param>
    /// <returns>The received message, or <see langword="null"/> when no message arrived in time.</returns>
    Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(TimeSpan maxWaitTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a successfully processed message.
    /// </summary>
    Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a message to the subscription so it can be redelivered.
    /// </summary>
    Task AbandonMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an unprocessable message to the dead-letter queue.
    /// </summary>
    Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        string reason,
        string description,
        CancellationToken cancellationToken = default);
}
