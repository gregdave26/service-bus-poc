namespace ServiceBusPoc.DigitalSite.CartProcessor;

/// <summary>Values recorded in the payment's <c>processingResult</c> custom field.</summary>
public static class ProcessingResults
{
    /// <summary>The order was created from the cart.</summary>
    public const string OrderCreated = "OrderCreated";

    /// <summary>The authorisation was refused, so no order is created.</summary>
    public const string PaymentRefused = "PaymentRefused";

    /// <summary>The authorised amount differs from the cart total.</summary>
    public const string AmountMismatch = "AmountMismatch";

    /// <summary>The payment does not reference a cart that exists.</summary>
    public const string CartNotFound = "CartNotFound";

    /// <summary>The cart was already ordered by a different payment.</summary>
    public const string CartAlreadyOrdered = "CartAlreadyOrdered";
}
