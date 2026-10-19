using Azure.Messaging.ServiceBus;
using ServiceBusPoc.Core.Contracts;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

public interface IDigitalChannelsRoutingTransport
{
    Task PublishAsync(
        ContactData contact,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ServiceBusReceivedMessage?> ReceiveAsync(
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default);
}
