using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.Notifications;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>The outcome a tester chooses for a stub payment.</summary>
public enum StubPaymentOutcome
{
    /// <summary>The payment is authorised.</summary>
    Authorised,

    /// <summary>The payment is refused.</summary>
    Refused,

    /// <summary>The payment is still being processed; no transaction changes.</summary>
    Pending,
}

/// <summary>
/// A gateway that needs no Adyen account. It creates fake sessions and produces Adyen-format
/// notifications signed with a per-process key, so simulated payments travel through the same
/// verification and commercetools update path as real ones.
/// </summary>
public sealed class StubPaymentGateway : IPaymentGatewayAdapter
{
    /// <summary>The merchant account the stub reports.</summary>
    public const string MerchantAccount = "StubMerchantAccount";

    private const string Environment = "stub";
    private const int HmacKeyBytes = 32;
    private const int PspReferenceLength = 16;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AdyenNotificationReader _notificationReader = new(Convert.ToHexString(RandomNumberGenerator.GetBytes(HmacKeyBytes)));
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="StubPaymentGateway"/> class.</summary>
    /// <param name="timeProvider">The clock.</param>
    public StubPaymentGateway(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public PaymentGatewayMode Mode => PaymentGatewayMode.Stub;

    /// <inheritdoc />
    public Task<CheckoutSession> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new CheckoutSession($"stub-session-{Guid.NewGuid():N}", Environment, null, Environment, Mode));

    /// <inheritdoc />
    public Task<PaymentActionResult> HandlePaymentDetailsAsync(PaymentDetailsSubmission submission, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentActionResult("Received", null, false));

    /// <inheritdoc />
    public WebhookVerification VerifyWebhook(string payload) => _notificationReader.Verify(payload);

    /// <inheritdoc />
    public PaymentUpdate MapWebhookToPaymentUpdate(PaymentNotification notification) =>
        AdyenEventMapping.ToPaymentUpdate(notification);

    /// <summary>Builds a signed Adyen-format notification for a simulated outcome.</summary>
    /// <param name="outcome">The outcome to simulate.</param>
    /// <param name="merchantReference">The payment key.</param>
    /// <param name="amount">The payment amount.</param>
    /// <returns>The notification body.</returns>
    public string CreateSignedNotification(StubPaymentOutcome outcome, string merchantReference, Money amount)
    {
        var item = new AdyenNotificationItemPayload
        {
            Amount = new AdyenAmountPayload { Currency = amount.CurrencyCode, Value = amount.CentAmount },
            EventCode = outcome == StubPaymentOutcome.Pending ? "PENDING" : "AUTHORISATION",
            EventDate = _timeProvider.GetUtcNow().ToString("O"),
            MerchantAccountCode = MerchantAccount,
            MerchantReference = merchantReference,
            PspReference = PspReferenceFor(merchantReference),
            Reason = outcome == StubPaymentOutcome.Refused ? "Refused" : null,
            Success = outcome == StubPaymentOutcome.Refused ? "false" : "true",
            PaymentMethod = "visa",
        };
        _notificationReader.Sign(item);

        var request = new AdyenNotificationRequestPayload
        {
            Live = "false",
            NotificationItems = [new AdyenNotificationContainerPayload { NotificationRequestItem = item }],
        };
        return JsonSerializer.Serialize(request, SerializerOptions);
    }

    // Stable per payment, so repeated simulations of one payment behave like Adyen retries.
    private static string PspReferenceFor(string merchantReference) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(merchantReference)))[..PspReferenceLength];
}
