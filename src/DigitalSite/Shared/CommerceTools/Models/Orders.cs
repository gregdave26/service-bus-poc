namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>A commercetools order.</summary>
public sealed class Order
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the version.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the human-readable order number.</summary>
    public string? OrderNumber { get; set; }

    /// <summary>Gets or sets the anonymous id copied from the cart.</summary>
    public string? AnonymousId { get; set; }

    /// <summary>Gets or sets the state; see <see cref="OrderStates"/>.</summary>
    public string OrderState { get; set; } = OrderStates.Open;

    /// <summary>Gets or sets the cart the order was created from.</summary>
    public ResourceReference? Cart { get; set; }

    /// <summary>Gets or sets the line items.</summary>
    public List<LineItem> LineItems { get; set; } = [];

    /// <summary>Gets or sets the total price.</summary>
    public Money TotalPrice { get; set; } = new();

    /// <summary>Gets or sets the payments.</summary>
    public PaymentInfo? PaymentInfo { get; set; }

    /// <summary>Gets or sets custom fields.</summary>
    public CustomFields? Custom { get; set; }

    /// <summary>Gets or sets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the last modification time.</summary>
    public DateTimeOffset LastModifiedAt { get; set; }
}

/// <summary>Request body for creating an order from a cart.</summary>
public sealed class OrderFromCartDraft
{
    /// <summary>Gets or sets the cart.</summary>
    public ResourceReference Cart { get; set; } = new();

    /// <summary>Gets or sets the expected cart version.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the order number.</summary>
    public string? OrderNumber { get; set; }
}
