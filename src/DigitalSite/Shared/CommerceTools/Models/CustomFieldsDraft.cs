namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>Custom fields to set when creating a resource.</summary>
public sealed class CustomFieldsDraft
{
    /// <summary>Gets or sets the custom type identifier.</summary>
    public ResourceReference Type { get; set; } = new();

    /// <summary>Gets or sets the field values.</summary>
    public Dictionary<string, object?> Fields { get; set; } = [];
}
