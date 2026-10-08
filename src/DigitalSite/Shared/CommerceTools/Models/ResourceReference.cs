namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>
/// A commercetools reference or resource identifier: a type plus an id or key.
/// </summary>
public sealed record ResourceReference
{
    /// <summary>Gets the referenced resource type, for example <c>cart</c> or <c>payment</c>.</summary>
    public string TypeId { get; init; } = string.Empty;

    /// <summary>Gets the referenced resource id.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the referenced resource key.</summary>
    public string? Key { get; init; }

    /// <summary>Creates a reference by id.</summary>
    /// <param name="typeId">The resource type.</param>
    /// <param name="id">The resource id.</param>
    /// <returns>The reference.</returns>
    public static ResourceReference ById(string typeId, string id) => new() { TypeId = typeId, Id = id };

    /// <summary>Creates a resource identifier by key.</summary>
    /// <param name="typeId">The resource type.</param>
    /// <param name="key">The resource key.</param>
    /// <returns>The resource identifier.</returns>
    public static ResourceReference ByKey(string typeId, string key) => new() { TypeId = typeId, Key = key };
}
