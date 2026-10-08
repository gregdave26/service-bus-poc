using System.Text.Json;

namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>A product type, for example Roadside Assistance.</summary>
public sealed class ProductType
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the version.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the key.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>The current, published view of a product.</summary>
public sealed class ProductProjection
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the version.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the key.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the product type reference.</summary>
    public ResourceReference ProductType { get; set; } = new();

    /// <summary>Gets or sets the localized name.</summary>
    public Dictionary<string, string> Name { get; set; } = [];

    /// <summary>Gets or sets the localized description.</summary>
    public Dictionary<string, string>? Description { get; set; }

    /// <summary>Gets or sets the master variant.</summary>
    public ProductVariant MasterVariant { get; set; } = new();
}

/// <summary>A sellable variant of a product.</summary>
public sealed class ProductVariant
{
    /// <summary>Gets or sets the variant id within the product.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the SKU.</summary>
    public string? Sku { get; set; }

    /// <summary>Gets or sets the variant key.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the prices.</summary>
    public List<Price> Prices { get; set; } = [];

    /// <summary>Gets or sets the attributes.</summary>
    public List<ProductAttribute> Attributes { get; set; } = [];

    /// <summary>Gets the key of an enum attribute value.</summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The enum key, or <see langword="null"/> when absent.</returns>
    public string? GetEnumKey(string name) =>
        Attributes.FirstOrDefault(attribute => attribute.Name == name) is { } attribute
        && attribute.Value.ValueKind == JsonValueKind.Object
        && attribute.Value.TryGetProperty("key", out var key)
            ? key.GetString()
            : null;
}

/// <summary>A product attribute value.</summary>
public sealed class ProductAttribute
{
    /// <summary>Gets or sets the attribute name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the raw value; enum values are <c>{ key, label }</c>.</summary>
    public JsonElement Value { get; set; }
}

/// <summary>A product price.</summary>
public sealed class Price
{
    /// <summary>Gets or sets the price id.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public Money Value { get; set; } = new();
}
