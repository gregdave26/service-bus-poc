using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

public sealed class FilteredChannelsRoutingTransport(
    ServiceBusClient client,
    IOptions<ServiceBusSettings> settings,
    ContactEventPublisher publisher) : IFilteredChannelsRoutingTransport, IAsyncDisposable
{
    private readonly Dictionary<string, ServiceBusReceiver> _receivers = new(StringComparer.OrdinalIgnoreCase);

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
        string subscriptionName,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.TopicName))
        {
            throw new InvalidOperationException("ServiceBus:TopicName must be configured.");
        }

        if (!_receivers.TryGetValue(subscriptionName, out var receiver))
        {
            receiver = client.CreateReceiver(
                settings.Value.TopicName,
                subscriptionName,
                new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
            _receivers.Add(subscriptionName, receiver);
        }

        return receiver.ReceiveMessageAsync(maxWaitTime, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var receiver in _receivers.Values)
        {
            await receiver.DisposeAsync();
        }

        _receivers.Clear();
    }
}
