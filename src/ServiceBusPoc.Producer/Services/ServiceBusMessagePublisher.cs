using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.Producer.Services;

/// <summary>
/// Publishes messages with the Azure Service Bus client.
/// </summary>
public sealed class ServiceBusMessagePublisher(IOptions<ServiceBusSettings> serviceBusOptions)
    : IServiceBusMessagePublisher
{
    private readonly ServiceBusSettings _settings =
        serviceBusOptions?.Value ?? throw new ArgumentNullException(nameof(serviceBusOptions));

    /// <inheritdoc />
    public async Task PublishAsync(
        ServiceBusMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            throw new InvalidOperationException("Service Bus connection string is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.TopicName))
        {
            throw new InvalidOperationException("Service Bus topic name is not configured.");
        }

        var clientOptions = new ServiceBusClientOptions
        {
            RetryOptions =
            {
                Mode = ServiceBusRetryMode.Exponential,
                MaxRetries = 3
            }
        };

        await using var client = new ServiceBusClient(_settings.ConnectionString, clientOptions);
        await using var sender = client.CreateSender(_settings.TopicName);
        await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
