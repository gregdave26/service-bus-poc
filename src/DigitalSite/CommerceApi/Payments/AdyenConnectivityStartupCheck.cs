using Microsoft.Extensions.Hosting;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>Checks Adyen connectivity before the CommerceApi accepts requests.</summary>
public sealed class AdyenConnectivityStartupCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PaymentGatewayRuntimeState _state;
    private readonly ILogger<AdyenConnectivityStartupCheck> _logger;

    /// <summary>Initializes the startup check.</summary>
    public AdyenConnectivityStartupCheck(
        IServiceScopeFactory scopeFactory,
        PaymentGatewayRuntimeState state,
        ILogger<AdyenConnectivityStartupCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _state.UseStub();
            _logger.LogError(exception, "Adyen connectivity check failed; falling back to the Stub payment gateway");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
