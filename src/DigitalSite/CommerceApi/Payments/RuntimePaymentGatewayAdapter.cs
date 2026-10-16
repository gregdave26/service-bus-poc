using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>Delegates payment operations to the configured gateway or the startup fallback.</summary>
public sealed class RuntimePaymentGatewayAdapter : IPaymentGatewayAdapter
{
    private readonly PaymentGatewayRuntimeState _state;
    private readonly AdyenPaymentAdapter _adyen;
    private readonly StubPaymentGateway _stub;

    /// <summary>Initializes the runtime gateway adapter.</summary>
    public RuntimePaymentGatewayAdapter(
        PaymentGatewayRuntimeState state,
        AdyenPaymentAdapter adyen,
        StubPaymentGateway stub)
    {
        _state = state;
        _adyen = adyen;
        _stub = stub;
    }

    private IPaymentGatewayAdapter Current => _state.EffectiveMode is PaymentGatewayMode.Adyen or PaymentGatewayMode.AdyenComponent
        ? _adyen
        : _stub;

    /// <inheritdoc />
    public PaymentGatewayMode Mode => Current.Mode;

    /// <inheritdoc />
    public Task<CheckoutSession> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken) =>
        Current.CreateCheckoutSessionAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<PaymentActionResult> HandlePaymentDetailsAsync(PaymentDetailsSubmission submission, CancellationToken cancellationToken) =>
        Current.HandlePaymentDetailsAsync(submission, cancellationToken);

    /// <inheritdoc />
    public WebhookVerification VerifyWebhook(string payload) => Current.VerifyWebhook(payload);

    /// <inheritdoc />
    public PaymentUpdate MapWebhookToPaymentUpdate(PaymentNotification notification) => Current.MapWebhookToPaymentUpdate(notification);
}
