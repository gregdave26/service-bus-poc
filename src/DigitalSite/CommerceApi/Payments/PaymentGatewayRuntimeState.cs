using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>Tracks the payment gateway selected after the startup connectivity check.</summary>
public sealed class PaymentGatewayRuntimeState
{
    /// <summary>Initializes the state with the configured gateway.</summary>
    /// <param name="configuredMode">The gateway requested by configuration.</param>
    public PaymentGatewayRuntimeState(PaymentGatewayMode configuredMode)
    {
        ConfiguredMode = configuredMode;
        EffectiveMode = configuredMode;
    }

    /// <summary>Gets the gateway requested by configuration.</summary>
    public PaymentGatewayMode ConfiguredMode { get; }

    /// <summary>Gets the gateway currently used by the application.</summary>
    public PaymentGatewayMode EffectiveMode { get; private set; }

    /// <summary>Gets whether the startup check selected simulated payments after an Adyen failure.</summary>
    public bool FallbackActive => FallbackReason is not null;

    /// <summary>Gets a credential-free reason code for automatic fallback.</summary>
    public string? FallbackReason { get; private set; }

    /// <summary>Records the startup connectivity failure and selects the local Stub gateway.</summary>
    internal void UseStartupFallback()
    {
        EffectiveMode = PaymentGatewayMode.Stub;
        FallbackReason = "AdyenStartupConnectivityFailed";
    }
}
