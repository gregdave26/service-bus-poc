using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.ParksResorts.Services;

/// <summary>
/// Parks & Resorts consumer service stub for Phase 2 implementation.
/// Receives contact events filtered by hasParksResorts = true.
/// </summary>
public class ParksResortsConsumerService
{
    private readonly ILogger<ParksResortsConsumerService> _logger;
    private readonly IOptions<ServiceBusSettings> _serviceBusSettings;

    public ParksResortsConsumerService(
        ILogger<ParksResortsConsumerService> logger,
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
        _logger.LogInformation("Parks & Resorts consumer service starting...");
        _logger.LogInformation("Subscription: {SubscriptionName}", _serviceBusSettings.Value.SubscriptionName);
        _logger.LogInformation("Filter: hasParksResorts = true");

        // TODO: Implement consumer logic in Phase 2
        _logger.LogInformation("Parks & Resorts consumer service ready for Phase 2 implementation");
        await Task.CompletedTask;
    }
}
