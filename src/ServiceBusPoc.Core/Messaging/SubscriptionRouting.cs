using ServiceBusPoc.Core.Contracts;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Mirrors the broker's subscription filters so a publisher can state which subscriptions
/// it <em>expects</em> to receive a message. The broker remains the only component that
/// actually routes messages; this projection exists for operator feedback and tests.
/// </summary>
public static class SubscriptionRouting
{
    /// <summary>
    /// Returns the subscriptions whose filters match the supplied capability flags.
    /// </summary>
    /// <param name="attributes">The contact capability flags, or <see langword="null"/> when none were supplied.</param>
    /// <returns>The expected subscription names, in topology order.</returns>
    public static IReadOnlyList<string> ExpectedSubscriptions(ContactAttributes? attributes)
    {
        var subscriptions = new List<string> { SubscriptionNames.DigitalChannels };

        if (attributes is null)
        {
            return subscriptions;
        }

        if (attributes.HasInsurance)
        {
            subscriptions.Add(SubscriptionNames.Insurance);
        }

        if (attributes.HasParksResorts)
        {
            subscriptions.Add(SubscriptionNames.ParksResorts);
        }

        if (attributes.HasCarwashProduct)
        {
            subscriptions.Add(SubscriptionNames.Carwash);
        }

        return subscriptions;
    }
}
