using Azure.Messaging.ServiceBus;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

public sealed class DigitalChannelsRoutingTransport(
    ContactEventPublisher publisher,
    IServiceBusReceiver receiver) : IDigitalChannelsRoutingTransport
{
    public Task PublishAsync(
        ContactData contact,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        publisher.PublishContactUpdatedAsync(
            contact,
            "routing-verifier",
            correlationId,
            cancellationToken);

    public Task<ServiceBusReceivedMessage?> ReceiveAsync(
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default) =>
        receiver.ReceiveMessageAsync(maxWaitTime, cancellationToken);

    public Task CompleteAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default) =>
        receiver.CompleteMessageAsync(message, cancellationToken);
}
