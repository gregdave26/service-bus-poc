using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>Tracks the payment gateway selected after the startup connectivity check.</summary>
public sealed class PaymentGatewayRuntimeState
{
    /// <summary>Initializes the state with the configured gateway.</summary>
    public PaymentGatewayRuntimeState(PaymentGatewayMode configuredMode) => EffectiveMode = configuredMode;

    /// <summary>Gets the gateway currently used by the application.</summary>
    public PaymentGatewayMode EffectiveMode { get; private set; }

    /// <summary>Switches payment operations to the local Stub gateway.</summary>
    public void UseStub() => EffectiveMode = PaymentGatewayMode.Stub;
}
