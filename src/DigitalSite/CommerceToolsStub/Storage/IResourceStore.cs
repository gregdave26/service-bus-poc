namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;

/// <summary>Persists stub resources as JSON documents with an outbox of subscription messages.</summary>
public interface IResourceStore
{
    /// <summary>Gets a resource by id.</summary>
    Task<T?> GetAsync<T>(string typeId, string id, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Gets a resource by key.</summary>
    Task<T?> GetByKeyAsync<T>(string typeId, string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Lists all resources of a type.</summary>
    Task<IReadOnlyList<T>> ListAsync<T>(string typeId, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Gets a value indicating whether any resource of the type exists.</summary>
    Task<bool> AnyAsync(string typeId, CancellationToken cancellationToken = default);

    /// <summary>Atomically writes resources and appends their messages to the outbox.</summary>
    /// <returns>The messages with their assigned sequence numbers.</returns>
    Task<IReadOnlyList<OutboxMessage>> CommitAsync(
        IReadOnlyList<ResourceWrite> writes,
        IReadOnlyList<PendingMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a resource.</summary>
    Task DeleteAsync(string typeId, string id, CancellationToken cancellationToken = default);

    /// <summary>Gets unpublished outbox messages, oldest first.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetUnpublishedMessagesAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>Marks an outbox message as published.</summary>
    Task MarkPublishedAsync(string messageId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default);
}
