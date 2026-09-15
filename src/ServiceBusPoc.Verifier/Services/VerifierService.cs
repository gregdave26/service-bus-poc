using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.Verifier.Services;

/// <summary>
/// Scenario verifier service stub for Phase 2 implementation.
/// Verifies that events are routed correctly according to subscription filters.
/// </summary>
public class VerifierService
{
    private readonly ILogger<VerifierService> _logger;
    private readonly IOptions<ServiceBusSettings> _serviceBusSettings;

    public VerifierService(
        ILogger<VerifierService> logger,
        IOptions<ServiceBusSettings> serviceBusSettings)
    {
        _logger = logger;
        _serviceBusSettings = serviceBusSettings;
    }

    /// <summary>
    /// Runs the verifier service.
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("Verifier service starting...");
        _logger.LogInformation("Service Bus namespace: {Namespace}", _serviceBusSettings.Value.Namespace);

        // TODO: Implement verification logic in Phase 2
        _logger.LogInformation("Verifier service ready for Phase 2 implementation");
        await Task.CompletedTask;
    }
}
