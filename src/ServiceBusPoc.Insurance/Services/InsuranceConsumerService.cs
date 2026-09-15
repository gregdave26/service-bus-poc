using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.Insurance.Services;

/// <summary>
/// Insurance consumer service stub for Phase 2 implementation.
/// Receives contact events filtered by hasInsurance = true.
/// </summary>
public class InsuranceConsumerService
{
    private readonly ILogger<InsuranceConsumerService> _logger;
    private readonly IOptions<ServiceBusSettings> _serviceBusSettings;

    public InsuranceConsumerService(
        ILogger<InsuranceConsumerService> logger,
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
        _logger.LogInformation("Insurance consumer service starting...");
        _logger.LogInformation("Subscription: {SubscriptionName}", _serviceBusSettings.Value.SubscriptionName);
        _logger.LogInformation("Filter: hasInsurance = true");

        // TODO: Implement consumer logic in Phase 2
        _logger.LogInformation("Insurance consumer service ready for Phase 2 implementation");
        await Task.CompletedTask;
    }
}
