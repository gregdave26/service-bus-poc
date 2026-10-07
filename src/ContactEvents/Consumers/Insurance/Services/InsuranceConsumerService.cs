using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.ContactEvents.Consumers.Insurance.Services;

/// <summary>
/// Consumes contact events from the <c>insurance</c> subscription.
/// Only receives messages where <c>hasInsurance = true</c> (filtering done by the broker).
/// Logs each received message to the console and reports heartbeats to the dashboard.
/// </summary>
public sealed class InsuranceConsumerService : AbstractConsumerService
{
    public InsuranceConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger<InsuranceConsumerService> logger)
        : base(consumerRunner, logger)
    {
    }

    protected override ConsumerDescriptor Descriptor =>
        new("insurance", "hasInsurance = true");

    protected override string ServiceName => "Insurance";

    /// <summary>
    /// Runs the consumer service, listening for messages on the insurance subscription.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await RunConsumerAsync(cancellationToken);
    }
}
