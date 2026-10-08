namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>A commercetools cart.</summary>
public sealed class Cart
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the version used for optimistic concurrency.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the anonymous id; the POC stores the member CRM id here.</summary>
    public string? AnonymousId { get; set; }

    /// <summary>Gets or sets the cart state; see <see cref="CartStates"/>.</summary>
    public string CartState { get; set; } = CartStates.Active;

    /// <summary>Gets or sets the line items.</summary>
    public List<LineItem> LineItems { get; set; } = [];

    /// <summary>Gets or sets the total price.</summary>
    public Money TotalPrice { get; set; } = new();

    /// <summary>Gets or sets the shipping address.</summary>
    public Address? ShippingAddress { get; set; }

    /// <summary>Gets or sets the payments added to the cart.</summary>
    public PaymentInfo? PaymentInfo { get; set; }

    /// <summary>Gets or sets the custom fields.</summary>
    public CustomFields? Custom { get; set; }

    /// <summary>Gets or sets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the last modification time.</summary>
    public DateTimeOffset LastModifiedAt { get; set; }
}

/// <summary>A cart or order line item.</summary>
public sealed class LineItem
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the product id.</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Gets or sets the product key.</summary>
    public string? ProductKey { get; set; }

    /// <summary>Gets or sets the localized product name.</summary>
    public Dictionary<string, string> Name { get; set; } = [];

    /// <summary>Gets or sets the variant.</summary>
    public ProductVariant Variant { get; set; } = new();

    /// <summary>Gets or sets the quantity.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets the unit price.</summary>
    public Price Price { get; set; } = new();

    /// <summary>Gets or sets the line total.</summary>
    public Money TotalPrice { get; set; } = new();
}

/// <summary>The payments associated with a cart or order.</summary>
public sealed class PaymentInfo
{
    /// <summary>Gets or sets the payment references.</summary>
    public List<ResourceReference> Payments { get; set; } = [];
}

/// <summary>Request body for creating a cart.</summary>
public sealed class CartDraft
{
    /// <summary>Gets or sets the currency.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the anonymous id.</summary>
    public string? AnonymousId { get; set; }

    /// <summary>Gets or sets the country used for price selection.</summary>
    public string? Country { get; set; }

    /// <summary>Gets or sets the shipping method.</summary>
    public ResourceReference? ShippingMethod { get; set; }

    /// <summary>Gets or sets the shipping address.</summary>
    public Address? ShippingAddress { get; set; }

    /// <summary>Gets or sets the line items.</summary>
    public List<LineItemDraft> LineItems { get; set; } = [];

    /// <summary>Gets or sets custom fields.</summary>
    public CustomFieldsDraft? Custom { get; set; }
}

/// <summary>A line item to add, by SKU or product id.</summary>
public sealed class LineItemDraft
{
    /// <summary>Gets or sets the product id.</summary>
    public string? ProductId { get; set; }

    /// <summary>Gets or sets the variant SKU.</summary>
    public string? Sku { get; set; }

    /// <summary>Gets or sets the quantity; defaults to 1.</summary>
    public long? Quantity { get; set; }
}
