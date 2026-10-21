using Azure.Messaging.ServiceBus;
using ServiceBusPoc.Core.Contracts;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

public interface IFilteredChannelsRoutingTransport
{
    Task PublishAsync(
        ContactData contact,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<ServiceBusReceivedMessage?> ReceiveAsync(
        string subscriptionName,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default);
}
