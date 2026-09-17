using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Adapts the Azure Service Bus SDK receiver for the configured topic subscription.
/// </summary>
public sealed class ServiceBusReceiverAdapter : IServiceBusReceiver
{
    private readonly ServiceBusReceiver _receiver;

    public ServiceBusReceiverAdapter(ServiceBusClient client, IOptions<ServiceBusSettings> settings)
    {
        var topicName = settings.Value.TopicName;
        var subscriptionName = settings.Value.SubscriptionName;

        if (string.IsNullOrWhiteSpace(topicName))
        {
            throw new InvalidOperationException(
                "ServiceBus:TopicName must be configured before messages can be received.");
        }

        if (string.IsNullOrWhiteSpace(subscriptionName))
        {
            throw new InvalidOperationException(
                "ServiceBus:SubscriptionName must be configured before messages can be received.");
        }

        _receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
    }

    /// <inheritdoc />
    public Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default) =>
        _receiver.ReceiveMessageAsync(maxWaitTime, cancellationToken)!;

    /// <inheritdoc />
    public Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) =>
        _receiver.CompleteMessageAsync(message, cancellationToken);

    /// <inheritdoc />
    public Task AbandonMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) =>
        _receiver.AbandonMessageAsync(message, propertiesToModify: null, cancellationToken);

    /// <inheritdoc />
    public Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        string reason,
        string description,
        CancellationToken cancellationToken = default) =>
        _receiver.DeadLetterMessageAsync(message, reason, description, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _receiver.DisposeAsync();
}
