namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>
/// Custom types and fields the Digital Site stores on commercetools resources. A real project must
/// define these types before use; the stub accepts any.
/// </summary>
public static class DigitalSiteCustomFields
{
    /// <summary>The custom type for Roadside Assistance carts; orders inherit its fields.</summary>
    public const string CartTypeKey = "rsa-cart";

    /// <summary>The custom type for Digital Site payments.</summary>
    public const string PaymentTypeKey = "rsa-payment";

    /// <summary>The interaction type recorded when a checkout session is created.</summary>
    public const string SessionInteractionTypeKey = "rsa-checkout-session-interaction";

    /// <summary>The POC correlation id, on carts, orders and payments.</summary>
    public const string CorrelationId = "correlationId";

    /// <summary>The cover SKU, on carts and orders.</summary>
    public const string CoverSku = "coverSku";

    /// <summary>How the vehicle was chosen: <c>RegoLookup</c> or <c>Skip</c>.</summary>
    public const string VehicleSearchChoice = "vehicleSearchChoice";

    /// <summary>The vehicle registration, on carts and orders.</summary>
    public const string VehicleRego = "vehicleRego";

    /// <summary>The cart a payment belongs to.</summary>
    public const string CartId = "cartId";

    /// <summary>When the shopper accepted the terms, on payments.</summary>
    public const string TermsAcceptedAt = "termsAcceptedAt";

    /// <summary>Why the cart processor did not create an order, on payments.</summary>
    public const string ProcessingResult = "processingResult";

    /// <summary>The ProductHoldingChange event id, on orders, once published.</summary>
    public const string HoldingEventId = "holdingEventId";
}
