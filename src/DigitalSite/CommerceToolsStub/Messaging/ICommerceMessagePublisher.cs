using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Messaging;

/// <summary>Delivers subscription messages, as a commercetools Azure Service Bus subscription would.</summary>
public interface ICommerceMessagePublisher
{
    /// <summary>Publishes one message.</summary>
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
