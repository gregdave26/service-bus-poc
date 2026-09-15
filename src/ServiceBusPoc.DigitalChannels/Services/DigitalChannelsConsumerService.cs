using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.DigitalChannels.Services;

/// <summary>
/// Digital Channels consumer service stub for Phase 2 implementation.
/// Receives all contact events from the subscription.
/// </summary>
public class DigitalChannelsConsumerService
{
    private readonly ILogger<DigitalChannelsConsumerService> _logger;
    private readonly IOptions<ServiceBusSettings> _serviceBusSettings;

    public DigitalChannelsConsumerService(
        ILogger<DigitalChannelsConsumerService> logger,
        IOptions<ServiceBusSettings> serviceBusSettings)
    {
        _logger = logger;
        _serviceBusSettings = serviceBusSettings;
    }

    /// <summary>
    /// Runs the consumer service.
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("Digital Channels consumer service starting...");
        _logger.LogInformation("Subscription: {SubscriptionName}", _serviceBusSettings.Value.SubscriptionName);

        // TODO: Implement consumer logic in Phase 2
        _logger.LogInformation("Digital Channels consumer service ready for Phase 2 implementation");
        await Task.CompletedTask;
    }
}
