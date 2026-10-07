namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>commercetools cart states.</summary>
public static class CartStates
{
    /// <summary>The cart can be changed.</summary>
    public const string Active = "Active";

    /// <summary>The cart was converted to an order.</summary>
    public const string Ordered = "Ordered";
}

/// <summary>commercetools order states.</summary>
public static class OrderStates
{
    /// <summary>The order was created.</summary>
    public const string Open = "Open";

    /// <summary>The order was confirmed.</summary>
    public const string Confirmed = "Confirmed";

    /// <summary>The order was fulfilled.</summary>
    public const string Complete = "Complete";

    /// <summary>The order was cancelled.</summary>
    public const string Cancelled = "Cancelled";
}

/// <summary>commercetools payment transaction types.</summary>
public static class TransactionTypes
{
    /// <summary>Funds reserved.</summary>
    public const string Authorization = "Authorization";

    /// <summary>Reservation released.</summary>
    public const string CancelAuthorization = "CancelAuthorization";

    /// <summary>Funds captured.</summary>
    public const string Charge = "Charge";

    /// <summary>Funds returned.</summary>
    public const string Refund = "Refund";

    /// <summary>Funds reclaimed by the issuer.</summary>
    public const string Chargeback = "Chargeback";
}

/// <summary>commercetools payment transaction states.</summary>
public static class TransactionStates
{
    /// <summary>Created but not yet sent to the provider.</summary>
    public const string Initial = "Initial";

    /// <summary>Awaiting the provider outcome.</summary>
    public const string Pending = "Pending";

    /// <summary>Succeeded.</summary>
    public const string Success = "Success";

    /// <summary>Failed.</summary>
    public const string Failure = "Failure";
}
