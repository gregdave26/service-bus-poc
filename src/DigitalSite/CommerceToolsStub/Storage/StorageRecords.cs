using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;

/// <summary>A resource to insert or replace.</summary>
/// <param name="TypeId">The resource type.</param>
/// <param name="Id">The resource id.</param>
/// <param name="Key">The optional unique key.</param>
/// <param name="Resource">The resource document.</param>
public sealed record ResourceWrite(string TypeId, string Id, string? Key, object Resource);

/// <summary>A message to record in the outbox; its sequence number is assigned on commit.</summary>
/// <param name="Message">The message.</param>
/// <param name="CorrelationId">The correlation id carried as Service Bus metadata.</param>
public sealed record PendingMessage(CommerceMessage Message, string? CorrelationId);

/// <summary>A message waiting in the outbox.</summary>
/// <param name="Message">The message with its sequence number.</param>
/// <param name="CorrelationId">The correlation id.</param>
public sealed record OutboxMessage(CommerceMessage Message, string? CorrelationId);
