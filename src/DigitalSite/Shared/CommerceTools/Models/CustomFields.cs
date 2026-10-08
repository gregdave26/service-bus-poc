using System.Text.Json;

namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>Custom fields attached to a resource, or an interface interaction.</summary>
public sealed class CustomFields
{
    /// <summary>Gets or sets the custom type.</summary>
    public ResourceReference? Type { get; set; }

    /// <summary>Gets or sets the field values.</summary>
    public Dictionary<string, JsonElement> Fields { get; set; } = [];

    /// <summary>Gets a string field value.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when absent or not a string.</returns>
    public string? GetString(string name) =>
        Fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Gets a boolean field value.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The value; <see langword="false"/> when absent or not a boolean.</returns>
    public bool GetBoolean(string name) =>
        Fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.True;
}
