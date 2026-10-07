using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.ContactEvents.Consumers.Carwash.Services;

/// <summary>
/// Consumes contact events from the <c>carwash</c> subscription.
/// Only receives messages where <c>hasCarwashProduct = true</c> (filtering done by the broker).
/// Logs each received message to the console and reports heartbeats to the dashboard.
/// </summary>
public sealed class CarwashConsumerService : AbstractConsumerService
{
    private readonly ILogger<CarwashConsumerService> _logger;
    private readonly IOptions<CarwashSettings> _carwashSettings;

    public CarwashConsumerService(
        SubscriptionConsumerRunner consumerRunner,
        ILogger<CarwashConsumerService> logger,
        IOptions<CarwashSettings> carwashSettings)
        : base(consumerRunner, logger)
    {
        _logger = logger;
        _carwashSettings = carwashSettings;
    }

    protected override ConsumerDescriptor Descriptor =>
        new("carwash", "hasCarwashProduct = true");

    protected override string ServiceName => "Carwash";

    /// <summary>
    /// Runs the consumer service, listening for messages on the carwash subscription.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Pulse API URL: {ApiUrl}", _carwashSettings.Value.ApiUrl);
        _logger.LogInformation("Mock mode: {MockMode}", _carwashSettings.Value.MockMode);
        await RunConsumerAsync(cancellationToken);
    }
}
