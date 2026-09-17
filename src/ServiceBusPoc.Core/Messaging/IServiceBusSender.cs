using Azure.Messaging.ServiceBus;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Abstraction over the Service Bus sender so publishing logic stays unit-testable.
/// </summary>
public interface IServiceBusSender : IAsyncDisposable
{
    /// <summary>
    /// Sends a single message to the configured topic.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Token used to cancel the send.</param>
    Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default);
}
