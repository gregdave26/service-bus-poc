namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>A commercetools payment.</summary>
public sealed class Payment
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the version.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the key; the Adyen merchant reference.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the planned amount.</summary>
    public Money AmountPlanned { get; set; } = new();

    /// <summary>Gets or sets the payment method information.</summary>
    public PaymentMethodInfo? PaymentMethodInfo { get; set; }

    /// <summary>Gets or sets the transactions.</summary>
    public List<Transaction> Transactions { get; set; } = [];

    /// <summary>Gets or sets the raw interactions with the payment service provider.</summary>
    public List<CustomFields> InterfaceInteractions { get; set; } = [];

    /// <summary>Gets or sets custom fields.</summary>
    public CustomFields? Custom { get; set; }

    /// <summary>Gets or sets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the last modification time.</summary>
    public DateTimeOffset LastModifiedAt { get; set; }
}

/// <summary>Information about the payment method and provider.</summary>
public sealed class PaymentMethodInfo
{
    /// <summary>Gets or sets the payment interface, for example <c>adyen</c>.</summary>
    public string? PaymentInterface { get; set; }

    /// <summary>Gets or sets the payment method, for example <c>scheme</c>.</summary>
    public string? Method { get; set; }
}

/// <summary>A financial transaction recorded on a payment.</summary>
public sealed class Transaction
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets when the transaction happened at the provider.</summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>Gets or sets the type; see <see cref="TransactionTypes"/>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public Money Amount { get; set; } = new();

    /// <summary>Gets or sets the provider identifier; the Adyen PSP reference.</summary>
    public string? InteractionId { get; set; }

    /// <summary>Gets or sets the state; see <see cref="TransactionStates"/>.</summary>
    public string State { get; set; } = TransactionStates.Initial;
}

/// <summary>Request body for creating a payment.</summary>
public sealed class PaymentDraft
{
    /// <summary>Gets or sets the key.</summary>
    public string? Key { get; set; }

    /// <summary>Gets or sets the planned amount.</summary>
    public Money AmountPlanned { get; set; } = new();

    /// <summary>Gets or sets the payment method information.</summary>
    public PaymentMethodInfo? PaymentMethodInfo { get; set; }

    /// <summary>Gets or sets custom fields.</summary>
    public CustomFieldsDraft? Custom { get; set; }
}

/// <summary>A transaction to add to a payment.</summary>
public sealed class TransactionDraft
{
    /// <summary>Gets or sets when the transaction happened at the provider.</summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>Gets or sets the type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public Money Amount { get; set; } = new();

    /// <summary>Gets or sets the provider identifier.</summary>
    public string? InteractionId { get; set; }

    /// <summary>Gets or sets the state.</summary>
    public string State { get; set; } = TransactionStates.Initial;
}
