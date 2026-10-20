using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

/// <summary>
/// Azure Service Bus implementation of the routing scenario transport.
/// </summary>
public sealed class ServiceBusRoutingScenarioTransport : IRoutingScenarioTransport
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSettings _settings;

    public ServiceBusRoutingScenarioTransport(
        ServiceBusClient client,
        IOptions<ServiceBusSettings> settings)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public async Task PublishAsync(ServiceBusMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var sender = _client.CreateSender(RequiredSetting(_settings.TopicName, "TopicName"));
        await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ServiceBusReceivedMessage?> ReceiveAsync(
        string subscriptionName,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscriptionName))
        {
            throw new ArgumentException("Subscription name is required.", nameof(subscriptionName));
        }

        await using var receiver = _client.CreateReceiver(
            RequiredSetting(_settings.TopicName, "TopicName"),
            subscriptionName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
        return await receiver.ReceiveMessageAsync(maxWaitTime, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string RequiredSetting(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"ServiceBus:{name} must be configured.")
            : value;
}
