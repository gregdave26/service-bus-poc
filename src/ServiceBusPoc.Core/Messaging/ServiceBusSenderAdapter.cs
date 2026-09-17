using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Adapts the Azure Service Bus SDK sender for the configured <c>contact.events</c> topic.
/// </summary>
public sealed class ServiceBusSenderAdapter : IServiceBusSender
{
    private readonly ServiceBusSender _sender;

    public ServiceBusSenderAdapter(ServiceBusClient client, IOptions<ServiceBusSettings> settings)
    {
        var topicName = settings.Value.TopicName;
        if (string.IsNullOrWhiteSpace(topicName))
        {
            throw new InvalidOperationException(
                "ServiceBus:TopicName must be configured before messages can be published.");
        }

        _sender = client.CreateSender(topicName);
    }

    /// <inheritdoc />
    public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default) =>
        _sender.SendMessageAsync(message, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
