using Azure.Messaging.ServiceBus;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

/// <summary>
/// Provides the Service Bus operations required by the routing scenario verifier.
/// </summary>
public interface IRoutingScenarioTransport : IAsyncDisposable
{
    /// <summary>Publishes a message to the configured contact-events topic.</summary>
    Task PublishAsync(ServiceBusMessage message, CancellationToken cancellationToken);

    /// <summary>Receives one message from a named subscription.</summary>
    Task<ServiceBusReceivedMessage?> ReceiveAsync(
        string subscriptionName,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken);
}
