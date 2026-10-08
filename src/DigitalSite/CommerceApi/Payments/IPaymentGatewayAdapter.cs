using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>
/// Keeps the Digital Site independent of a payment service provider. Only gateway-neutral models
/// cross this boundary.
/// </summary>
public interface IPaymentGatewayAdapter
{
    /// <summary>Gets the gateway this adapter talks to.</summary>
    PaymentGatewayMode Mode { get; }

    /// <summary>Creates a checkout session for the client payment component.</summary>
    /// <param name="request">The session request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The session.</returns>
    /// <exception cref="PaymentGatewayException">The gateway rejected the request.</exception>
    Task<CheckoutSession> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken);

    /// <summary>Continues a payment that needed a shopper action, such as a redirect or 3D Secure.</summary>
    /// <param name="submission">The details returned by the client component.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The interim result; the notification remains authoritative.</returns>
    /// <exception cref="PaymentGatewayException">The gateway rejected the request.</exception>
    Task<PaymentActionResult> HandlePaymentDetailsAsync(PaymentDetailsSubmission submission, CancellationToken cancellationToken);

    /// <summary>Parses a notification payload and verifies the signature of each item.</summary>
    /// <param name="payload">The raw request body.</param>
    /// <returns>The authentic items and the number rejected.</returns>
    /// <exception cref="FormatException">The payload is not a notification.</exception>
    WebhookVerification VerifyWebhook(string payload);

    /// <summary>Maps a verified notification to the commercetools change it implies.</summary>
    /// <param name="notification">The notification.</param>
    /// <returns>The payment update.</returns>
    PaymentUpdate MapWebhookToPaymentUpdate(PaymentNotification notification);
}
