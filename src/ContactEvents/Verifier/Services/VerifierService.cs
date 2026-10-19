using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

/// <summary>
/// Scenario verifier service stub for Phase 2 implementation.
/// Verifies that events are routed correctly according to subscription filters.
/// </summary>
public class VerifierService
{
    private readonly ILogger<VerifierService> _logger;
    private readonly IOptions<ServiceBusSettings> _serviceBusSettings;
    private readonly ITopologyValidator _topologyValidator;
    private readonly IDigitalChannelsRoutingVerifier _digitalChannelsRoutingVerifier;
    private readonly IFilteredChannelsRoutingVerifier _filteredChannelsRoutingVerifier;

    public VerifierService(
        ILogger<VerifierService> logger,
        IOptions<ServiceBusSettings> serviceBusSettings,
        ITopologyValidator topologyValidator,
        IDigitalChannelsRoutingVerifier digitalChannelsRoutingVerifier,
        IFilteredChannelsRoutingVerifier filteredChannelsRoutingVerifier)
    {
        _logger = logger;
        _serviceBusSettings = serviceBusSettings;
        _topologyValidator = topologyValidator;
        _digitalChannelsRoutingVerifier = digitalChannelsRoutingVerifier;
        _filteredChannelsRoutingVerifier = filteredChannelsRoutingVerifier;
    }

    /// <summary>
    /// Runs the verifier service.
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("Verifier service starting...");
        _logger.LogInformation("Service Bus namespace: {Namespace}", _serviceBusSettings.Value.Namespace);

        // Validate emulator topology (Phase 2.1)
        var topologyValid = await _topologyValidator.ValidateAsync();
        if (!topologyValid)
        {
            _logger.LogError("Topology validation failed");
            throw new InvalidOperationException("Topology validation failed.");
        }

        await _digitalChannelsRoutingVerifier.VerifyAsync();
        _logger.LogInformation("Digital-channels routing validation completed");
        await _filteredChannelsRoutingVerifier.VerifyAsync();
        _logger.LogInformation("Insurance and parks-resorts routing validation completed");
    }
}
