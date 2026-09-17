using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Carwash.Services;

/// <summary>
/// Consumes contact events from the <c>carwash</c> subscription.
/// Only receives messages where <c>hasCarwashProduct = true</c> (filtering done by the broker).
/// Logs each received message to the console and reports heartbeats to the dashboard.
/// </summary>
public sealed class CarwashConsumerService
{
    private readonly SubscriptionConsumerRunner _consumerRunner;
    private readonly ILogger<CarwashConsumerService> _logger;
    private readonly IOptions<CarwashSettings> _carwashSettings;

    public CarwashConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger<CarwashConsumerService> logger,
        IOptions<CarwashSettings> carwashSettings)
    {
        _consumerRunner = consumerRunner;
        _logger = logger;
        _carwashSettings = carwashSettings;
    }

    /// <summary>
    /// Runs the consumer service, listening for messages on the carwash subscription.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Carwash consumer service starting...");
        _logger.LogInformation("Pulse API URL: {ApiUrl}", _carwashSettings.Value.ApiUrl);
        _logger.LogInformation("Mock mode: {MockMode}", _carwashSettings.Value.MockMode);

        var descriptor = new ConsumerDescriptor("carwash", "hasCarwashProduct = true");

        try
        {
            await _consumerRunner.RunAsync(descriptor, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Carwash consumer service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Carwash consumer service encountered an error");
            throw;
        }
    }
}
