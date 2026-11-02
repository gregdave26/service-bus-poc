using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>Checks Adyen connectivity before the CommerceApi accepts requests.</summary>
public sealed class AdyenConnectivityStartupCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PaymentGatewayRuntimeState _state;
    private readonly IOptions<DigitalSiteSettings> _settings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AdyenConnectivityStartupCheck> _logger;

    /// <summary>Initializes the startup check.</summary>
    /// <param name="scopeFactory">Creates the scope for the real gateway.</param>
    /// <param name="state">Records the configured and effective gateways.</param>
    /// <param name="settings">Controls the development-only fallback permission.</param>
    /// <param name="environment">Identifies the hosting environment.</param>
    /// <param name="logger">Reports connectivity failure and simulated payments.</param>
    public AdyenConnectivityStartupCheck(
        IServiceScopeFactory scopeFactory,
        PaymentGatewayRuntimeState state,
        IOptions<DigitalSiteSettings> settings,
        IHostEnvironment environment,
        ILogger<AdyenConnectivityStartupCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _settings = settings;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var allowFallback = _settings.Value.AllowDevelopmentAdyenFallback;
        if (allowFallback && !_environment.IsDevelopment())
        {
            throw new OptionsValidationException(
                Options.DefaultName,
                typeof(DigitalSiteSettings),
                ["DigitalSite:AllowDevelopmentAdyenFallback is permitted only in the Development hosting environment."]);
        }

        if (_state.EffectiveMode is not (PaymentGatewayMode.Adyen or PaymentGatewayMode.AdyenComponent))
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var adyen = scope.ServiceProvider.GetRequiredService<AdyenPaymentAdapter>();
            await adyen.CheckConnectivityAsync(cancellationToken);
            _logger.LogInformation("Adyen connectivity check succeeded; using the {PaymentGateway} payment gateway", _state.EffectiveMode);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsFallbackEligible(exception))
        {
            if (!allowFallback)
            {
                _logger.LogError(
                    "Adyen startup connectivity check failed ({FailureType}); fallback is disabled. Startup will fail with configured gateway {ConfiguredGateway}",
                    exception.GetType().Name, _state.ConfiguredMode);
                throw;
            }

            _state.UseStartupFallback();
            _logger.LogError(
                "Adyen connection failed ({FailureType}); switching from {ConfiguredGateway} to Stub fallback in Development. All payments are simulated; no real payment will be taken",
                exception.GetType().Name, _state.ConfiguredMode);
            _logger.LogWarning(
                "STUB PAYMENT FALLBACK ACTIVE. Restart the application after fixing the Adyen connection to retry Adyen; automatic recovery is disabled");
        }
    }

    private static bool IsFallbackEligible(Exception exception) => exception switch
    {
        HttpRequestException or TaskCanceledException => true,
        PaymentGatewayException { HttpStatusCode: var status } => status is 408 or 429 or >= 500,
        _ => false,
    };

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
