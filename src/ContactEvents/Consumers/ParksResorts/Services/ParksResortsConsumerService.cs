using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.ContactEvents.Consumers.ParksResorts.Services;

/// <summary>
/// Consumes contact events from the <c>parks-resorts</c> subscription.
/// Only receives messages where <c>hasParksResorts = true</c> (filtering done by the broker).
/// Logs each received message to the console and reports heartbeats to the dashboard.
/// </summary>
public sealed class ParksResortsConsumerService : AbstractConsumerService
{
    public ParksResortsConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger<ParksResortsConsumerService> logger)
        : base(consumerRunner, logger)
    {
    }

    protected override ConsumerDescriptor Descriptor =>
        new("parks-resorts", "hasParksResorts = true");

    protected override string ServiceName => "Parks & Resorts";

    /// <summary>
    /// Runs the consumer service, listening for messages on the parks-resorts subscription.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await RunConsumerAsync(cancellationToken);
    }
}
