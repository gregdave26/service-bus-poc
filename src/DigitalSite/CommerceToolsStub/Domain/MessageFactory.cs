using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>Creates subscription messages in the commercetools <c>MessageDeliveryPayload</c> shape.</summary>
public sealed class MessageFactory
{
    private readonly string _projectKey;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="MessageFactory"/> class.</summary>
    public MessageFactory(IOptions<CommerceToolsStubSettings> settings, TimeProvider timeProvider)
    {
        _projectKey = settings.Value.ProjectKey;
        _timeProvider = timeProvider;
    }

    /// <summary>Creates a message; the store assigns its sequence number.</summary>
    /// <param name="type">The message type.</param>
    /// <param name="resource">The changed resource.</param>
    /// <param name="resourceVersion">The resource version after the change.</param>
    /// <param name="fields">Message-specific fields.</param>
    /// <param name="correlationId">The correlation id carried as Service Bus metadata.</param>
    /// <returns>The pending message.</returns>
    public PendingMessage Create(
        string type,
        ResourceReference resource,
        long resourceVersion,
        IReadOnlyDictionary<string, object?> fields,
        string? correlationId) =>
        new(
            new CommerceMessage
            {
                ProjectKey = _projectKey,
                Id = Guid.NewGuid().ToString(),
                Resource = resource,
                ResourceVersion = resourceVersion,
                Type = type,
                CreatedAt = _timeProvider.GetUtcNow(),
                Extensions = fields.ToDictionary(field => field.Key, field => StubResources.ToElement(field.Value))
            },
            correlationId);
}
