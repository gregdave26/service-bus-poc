namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>The payment gateway rejected a request or could not be reached.</summary>
public sealed class PaymentGatewayException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PaymentGatewayException"/> class.</summary>
    /// <param name="message">A message that is safe to log; never include card data or keys.</param>
    /// <param name="httpStatusCode">The HTTP status returned by the gateway, when one was received.</param>
    public PaymentGatewayException(string message, int? httpStatusCode = null)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
    }

    /// <summary>Gets the HTTP status returned by the gateway, or <see langword="null"/> when no response was received.</summary>
    public int? HttpStatusCode { get; }
}
