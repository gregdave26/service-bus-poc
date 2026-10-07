namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Feature toggle selecting the payment gateway used by the Digital Site checkout.
/// </summary>
public enum PaymentGatewayMode
{
    /// <summary>An in-process gateway that simulates Adyen sessions and signed notifications.</summary>
    Stub,

    /// <summary>The Adyen Checkout API (directly or through APIM).</summary>
    Adyen
}
