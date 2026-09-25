using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.DigitalChannels.Services;

/// <summary>
/// Consumes all contact events from the <c>digital-channels</c> subscription.
/// DigitalChannels has no capability filter; receives all contact updates.
/// Logs each received message to the console and reports heartbeats to the dashboard.
/// </summary>
public sealed class DigitalChannelsConsumerService : AbstractConsumerService
{
    public DigitalChannelsConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger<DigitalChannelsConsumerService> logger)
        : base(consumerRunner, logger)
    {
    }

    protected override ConsumerDescriptor Descriptor =>
        new("digital-channels", ConsumerDescriptor.NoFilter);

    protected override string ServiceName => "Digital Channels";

    /// <summary>
    /// Runs the consumer service, listening for messages on the digital-channels subscription.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await RunConsumerAsync(cancellationToken);
    }
}
