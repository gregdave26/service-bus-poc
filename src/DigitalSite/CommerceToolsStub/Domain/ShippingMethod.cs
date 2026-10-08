namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>A shipping method; the POC only has <c>digital</c>.</summary>
public sealed class ShippingMethod
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the key.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
}
