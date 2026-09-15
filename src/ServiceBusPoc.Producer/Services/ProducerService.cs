using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.Producer.Services;

/// <summary>
/// Producer service stub for Phase 2 implementation.
/// Responsible for publishing contact events to the Service Bus topic.
/// </summary>
public class ProducerService
{
    private readonly ILogger<ProducerService> _logger;
    private readonly IOptions<ServiceBusSettings> _serviceBusSettings;

    public ProducerService(
        ILogger<ProducerService> logger,
        IOptions<ServiceBusSettings> serviceBusSettings)
    {
        _logger = logger;
        _serviceBusSettings = serviceBusSettings;
    }

    /// <summary>
    /// Runs the producer service.
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("Producer service starting...");
        _logger.LogInformation("Service Bus namespace: {Namespace}", _serviceBusSettings.Value.Namespace);
        _logger.LogInformation("Topic name: {TopicName}", _serviceBusSettings.Value.TopicName);

        // TODO: Implement producer logic in Phase 2
        _logger.LogInformation("Producer service ready for Phase 2 implementation");
        await Task.CompletedTask;
    }
}
