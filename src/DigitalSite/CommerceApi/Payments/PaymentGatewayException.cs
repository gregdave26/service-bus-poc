namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>The payment gateway rejected a request or could not be reached.</summary>
public sealed class PaymentGatewayException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PaymentGatewayException"/> class.</summary>
    /// <param name="message">A message that is safe to log; never include card data or keys.</param>
    public PaymentGatewayException(string message)
        : base(message)
    {
    }
}
