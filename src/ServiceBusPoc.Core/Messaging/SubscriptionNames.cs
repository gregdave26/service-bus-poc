namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Canonical <c>contact.events</c> subscription names.
/// These must stay identical to the subscriptions declared in the broker topology
/// (<c>infra/servicebus/config.json</c> and the Bicep templates).
/// </summary>
public static class SubscriptionNames
{
    /// <summary>Subscription without a filter; receives every message.</summary>
    public const string DigitalChannels = "digital-channels";

    /// <summary>Subscription filtered by <c>hasInsurance = true</c>.</summary>
    public const string Insurance = "insurance";

    /// <summary>Subscription filtered by <c>hasParksResorts = true</c>.</summary>
    public const string ParksResorts = "parks-resorts";

    /// <summary>Subscription filtered by <c>hasCarwashProduct = true</c>.</summary>
    public const string Carwash = "carwash";
}
