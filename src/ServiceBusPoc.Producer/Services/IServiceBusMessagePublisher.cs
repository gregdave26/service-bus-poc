using Azure.Messaging.ServiceBus;

namespace ServiceBusPoc.Producer.Services;

/// <summary>
/// Publishes messages to the configured Service Bus topic.
/// </summary>
public interface IServiceBusMessagePublisher
{
    /// <summary>
    /// Publishes a message asynchronously.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishAsync(ServiceBusMessage message, CancellationToken cancellationToken);
}
