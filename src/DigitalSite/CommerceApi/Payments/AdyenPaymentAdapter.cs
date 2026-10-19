using Adyen.Checkout.Models;
using Adyen.Checkout.Services;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.Notifications;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>Adyen Checkout (Sessions flow) behind the gateway-neutral adapter.</summary>
public sealed class AdyenPaymentAdapter : IPaymentGatewayAdapter
{
    private readonly IPaymentsService _payments;
    private readonly AdyenSettings _adyenSettings;
    private readonly DigitalSiteSettings _siteSettings;
    private readonly CommerceToolsSettings _commerceToolsSettings;
    private readonly AdyenNotificationReader? _notificationReader;
    private readonly ILogger<AdyenPaymentAdapter> _logger;

    /// <summary>Initializes a new instance of the <see cref="AdyenPaymentAdapter"/> class.</summary>
    /// <param name="payments">The Adyen Checkout payments API.</param>
    /// <param name="adyenSettings">The Adyen settings.</param>
    /// <param name="siteSettings">The Digital Site settings.</param>
    /// <param name="commerceToolsSettings">The commercetools settings.</param>
    /// <param name="logger">The logger.</param>
    public AdyenPaymentAdapter(
        IPaymentsService payments,
        IOptions<AdyenSettings> adyenSettings,
        IOptions<DigitalSiteSettings> siteSettings,
        IOptions<CommerceToolsSettings> commerceToolsSettings,
        ILogger<AdyenPaymentAdapter> logger)
    {
        _payments = payments;
        _adyenSettings = adyenSettings.Value;
        _siteSettings = siteSettings.Value;
        _commerceToolsSettings = commerceToolsSettings.Value;
        _notificationReader = string.IsNullOrWhiteSpace(_adyenSettings.HmacKey)
            ? null
            : new AdyenNotificationReader(_adyenSettings.HmacKey);
        _logger = logger;
    }

    /// <inheritdoc />
    public PaymentGatewayMode Mode =>
        _siteSettings.PaymentGateway == PaymentGatewayMode.AdyenComponent
            ? PaymentGatewayMode.AdyenComponent
            : PaymentGatewayMode.Adyen;

    /// <summary>Verifies that the configured Adyen credentials and endpoint can create a Checkout session.</summary>
    public async Task CheckConnectivityAsync(CancellationToken cancellationToken)
    {
        await CreateCheckoutSessionAsync(
            new CheckoutSessionRequest(
                "startup-connectivity-check",
                "startup-connectivity-check",
                $"RSA-startup-{Guid.NewGuid():N}",
                Money.FromCents(_siteSettings.Currency, 1),
                _siteSettings.AllowedOriginList.FirstOrDefault() ?? "http://localhost",
                "startup-connectivity-check",
                null,
                null),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CheckoutSession> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        var sessionRequest = new CreateCheckoutSessionRequest
        {
            Amount = new Amount { Currency = request.Amount.CurrencyCode, Value = request.Amount.CentAmount },
            MerchantAccount = _adyenSettings.MerchantAccount,
            Reference = request.MerchantReference,
            ReturnUrl = request.ReturnUrl,
            CountryCode = _siteSettings.Country,
            ShopperLocale = _siteSettings.ShopperLocale,
            Channel = CreateCheckoutSessionRequest.ChannelEnum.Web,
            Metadata = CreateMetadata(request),
        };

        var response = await _payments.SessionsAsync(sessionRequest, null, cancellationToken);
        if (!response.IsCreated)
        {
            _logger.LogWarning(
                "Adyen session creation failed with {StatusCode} for {MerchantReference}",
                (int)response.StatusCode,
                request.MerchantReference);
            throw new PaymentGatewayException($"Adyen did not create a session (HTTP {(int)response.StatusCode})");
        }

        var session = response.Created();
        if (session?.Id is null || session.SessionData is null)
        {
            throw new PaymentGatewayException("Adyen returned an incomplete session");
        }

        _logger.LogInformation("Adyen session {SessionId} created for {MerchantReference}", session.Id, request.MerchantReference);
        return new CheckoutSession(session.Id, session.SessionData, _adyenSettings.ClientKey, _adyenSettings.Environment, Mode);
    }

    /// <inheritdoc />
    public async Task<PaymentActionResult> HandlePaymentDetailsAsync(PaymentDetailsSubmission submission, CancellationToken cancellationToken)
    {
        var detailsRequest = new PaymentDetailsRequest
        {
            Details = new PaymentCompletionDetails
            {
                RedirectResult = submission.RedirectResult ?? ReadDetail(submission.Details, "redirectResult"),
                ThreeDSResult = submission.ThreeDSResult ?? ReadDetail(submission.Details, "threeDSResult"),
                Threeds2ChallengeResult = ReadDetail(submission.Details, "threeds2.challengeResult", "challengeResult"),
            },
            PaymentData = submission.PaymentData,
        };

        var response = await _payments.PaymentsDetailsAsync(detailsRequest, null, cancellationToken);
        if (!response.IsOk)
        {
            _logger.LogWarning(
                "Adyen payment details failed with {StatusCode} for payment {PaymentId}",
                (int)response.StatusCode,
                submission.PaymentId);
            throw new PaymentGatewayException($"Adyen rejected the payment details (HTTP {(int)response.StatusCode})");
        }

        var details = response.Ok() ?? throw new PaymentGatewayException("Adyen returned empty payment details");
        var resultCode = details.ResultCode?.ToString() ?? "Unknown";
        _logger.LogInformation(
            "Adyen payment details returned {ResultCode} with {PspReference} for payment {PaymentId}",
            resultCode,
            details.PspReference,
            submission.PaymentId);
        return new PaymentActionResult(resultCode, details.PspReference, details.Action is not null, details.Action);
    }

    /// <inheritdoc />
    public WebhookVerification VerifyWebhook(string payload) =>
        _notificationReader?.Verify(payload)
        ?? throw new PaymentGatewayException("Adyen webhooks are not enabled in component-only mode");

    /// <inheritdoc />
    public PaymentUpdate MapWebhookToPaymentUpdate(PaymentNotification notification) =>
        AdyenEventMapping.ToPaymentUpdate(notification);

    // Adyen returns metadata in the AUTHORISATION webhook, which lets support staff trace a payment
    // back to commercetools without our database.
    private Dictionary<string, string> CreateMetadata(CheckoutSessionRequest request)
    {
        var metadata = new Dictionary<string, string>
        {
            ["ctProjectKey"] = _commerceToolsSettings.ProjectKey ?? string.Empty,
            ["cartId"] = request.CartId,
            ["paymentId"] = request.PaymentId,
            ["correlationId"] = request.CorrelationId,
        };
        if (request.CrmId is not null)
        {
            metadata["crmId"] = request.CrmId;
        }

        if (request.Sku is not null)
        {
            metadata["sku"] = request.Sku;
        }

        return metadata;
    }

    private static string? ReadDetail(JsonElement? details, params string[] names)
    {
        if (details is not JsonElement value || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }
}
