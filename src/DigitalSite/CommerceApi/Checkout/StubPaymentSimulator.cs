using ServiceBusPoc.DigitalSite.CommerceApi.Notifications;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Checkout;

/// <summary>
/// Plays the part of Adyen in stub mode: the chosen outcome becomes a signed webhook that goes
/// through the real notification module.
/// </summary>
public sealed class StubPaymentSimulator
{
    private readonly StubPaymentGateway _gateway;
    private readonly AdyenNotificationModule _notifications;
    private readonly CheckoutService _checkout;

    /// <summary>Initializes a new instance of the <see cref="StubPaymentSimulator"/> class.</summary>
    /// <param name="gateway">The stub gateway that signs notifications.</param>
    /// <param name="notifications">The notification module.</param>
    /// <param name="checkout">The checkout service.</param>
    public StubPaymentSimulator(StubPaymentGateway gateway, AdyenNotificationModule notifications, CheckoutService checkout)
    {
        _gateway = gateway;
        _notifications = notifications;
        _checkout = checkout;
    }

    /// <summary>Simulates a payment outcome.</summary>
    /// <param name="paymentId">The payment id.</param>
    /// <param name="outcome">The outcome name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>How the notification was applied.</returns>
    public async Task<NotificationOutcome> SimulateAsync(string paymentId, string? outcome, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<StubPaymentOutcome>(outcome, ignoreCase: true, out var parsedOutcome) || !Enum.IsDefined(parsedOutcome))
        {
            throw DigitalSiteRequestException.BadRequest("outcome must be Authorised, Refused or Pending");
        }

        var payment = await _checkout.GetPaymentAsync(paymentId, cancellationToken);
        var payload = _gateway.CreateSignedNotification(parsedOutcome, payment.Key!, payment.AmountPlanned);
        var outcomes = await _notifications.HandleAsync(payload, cancellationToken);
        return outcomes.Single();
    }
}
