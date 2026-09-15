using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.Carwash.Services;

/// <summary>
/// Carwash consumer service stub for Phase 2 implementation.
/// Receives contact events filtered by hasCarwashProduct = true.
/// Integrates with the Pulse Contact CRUD API to sync matching contacts.
/// </summary>
public class CarwashConsumerService
{
    private readonly ILogger<CarwashConsumerService> _logger;
    private readonly IOptions<ServiceBusSettings> _serviceBusSettings;
    private readonly IOptions<Core.Configuration.CarwashSettings> _carwashSettings;

    public CarwashConsumerService(
        ILogger<CarwashConsumerService> logger,
        IOptions<ServiceBusSettings> serviceBusSettings,
        IOptions<Core.Configuration.CarwashSettings> carwashSettings)
    {
        _logger = logger;
        _serviceBusSettings = serviceBusSettings;
        _carwashSettings = carwashSettings;
    }

    /// <summary>
    /// Runs the consumer service.
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("Carwash consumer service starting...");
        _logger.LogInformation("Subscription: {SubscriptionName}", _serviceBusSettings.Value.SubscriptionName);
        _logger.LogInformation("Filter: hasCarwashProduct = true");
        _logger.LogInformation("Pulse API URL: {ApiUrl}", _carwashSettings.Value.ApiUrl);
        _logger.LogInformation("Mock mode: {MockMode}", _carwashSettings.Value.MockMode);

        // TODO: Implement consumer and Pulse API integration in Phase 2
        _logger.LogInformation("Carwash consumer service ready for Phase 2 implementation");
        await Task.CompletedTask;
    }
}
