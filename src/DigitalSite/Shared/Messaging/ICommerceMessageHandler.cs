using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.Shared.Messaging;

/// <summary>
/// Handles one commercetools subscription message. Handlers must be idempotent: the broker
/// redelivers on failure and commercetools may deliver a message more than once.
/// </summary>
public interface ICommerceMessageHandler
{
    /// <summary>Gets the service name reported to the dashboard and logs.</summary>
    string ServiceName { get; }

    /// <summary>Handles the message. Throwing abandons it for redelivery.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Token used to cancel handling.</param>
    /// <returns>A task that completes when the message is handled.</returns>
    Task HandleAsync(CommerceMessage message, CancellationToken cancellationToken);
}
