using System.Globalization;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.Carts;
using ServiceBusPoc.DigitalSite.CommerceApi.Notifications;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Checkout;

/// <summary>
/// Starts and tracks payments. The browser only ever sees interim results here; the order is
/// created by the cart processor once a verified notification has authorised the payment.
/// </summary>
public sealed class CheckoutService
{
    /// <summary>The payment interface recorded on commercetools payments.</summary>
    public const string PaymentInterface = "adyen";

    private readonly ICommerceToolsClient _commerceTools;
    private readonly IPaymentGatewayAdapter _gateway;
    private readonly DigitalSiteSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CheckoutService> _logger;

    /// <summary>Initializes a new instance of the <see cref="CheckoutService"/> class.</summary>
    /// <param name="commerceTools">The commercetools client.</param>
    /// <param name="gateway">The payment gateway.</param>
    /// <param name="settings">The Digital Site settings.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CheckoutService(
        ICommerceToolsClient commerceTools,
        IPaymentGatewayAdapter gateway,
        IOptions<DigitalSiteSettings> settings,
        TimeProvider timeProvider,
        ILogger<CheckoutService> logger)
    {
        _commerceTools = commerceTools;
        _gateway = gateway;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Creates a commercetools payment for the cart and a gateway session for it.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The session for the browser.</returns>
    public async Task<CheckoutSessionResponse> StartCheckoutAsync(StartCheckoutRequest request, CancellationToken cancellationToken)
    {
        MemberCartService.EnsureValidCrmId(request.CrmId);
        if (!request.AcceptedPaymentAuthTerms)
        {
            throw DigitalSiteRequestException.BadRequest("Payment authorisation terms must be accepted");
        }

        if (!request.AcceptedRoadsideAssistTerms)
        {
            throw DigitalSiteRequestException.BadRequest("Roadside Assistance terms must be accepted");
        }

        var returnUrl = ValidateReturnUrl(request.ReturnUrl);
        var cart = await GetPayableCartAsync(request.CartId, request.CrmId!, cancellationToken);
        var correlationId = cart.Custom?.GetString(DigitalSiteCustomFields.CorrelationId) ?? Guid.NewGuid().ToString();
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["CartId"] = cart.Id,
        });

        var merchantReference = $"RSA-{Guid.NewGuid():N}";
        var payment = await _commerceTools.CreatePaymentAsync(
            new PaymentDraft
            {
                Key = merchantReference,
                AmountPlanned = cart.TotalPrice,
                PaymentMethodInfo = new PaymentMethodInfo { PaymentInterface = PaymentInterface },
                Custom = new CustomFieldsDraft
                {
                    Type = ResourceReference.ByKey(ResourceTypes.Type, DigitalSiteCustomFields.PaymentTypeKey),
                    Fields = new Dictionary<string, object?>
                    {
                        [DigitalSiteCustomFields.CorrelationId] = correlationId,
                        [DigitalSiteCustomFields.CartId] = cart.Id,
                        [DigitalSiteCustomFields.TermsAcceptedAt] = Now(),
                    },
                },
            },
            cancellationToken);
        await _commerceTools.UpdateCartAsync(
            cart.Id,
            cart.Version,
            [new AddPaymentAction(ResourceReference.ById(ResourceTypes.Payment, payment.Id))],
            cancellationToken);

        var session = await _gateway.CreateCheckoutSessionAsync(
            new CheckoutSessionRequest(
                cart.Id,
                payment.Id,
                merchantReference,
                cart.TotalPrice,
                returnUrl,
                correlationId,
                cart.AnonymousId,
                cart.LineItems.FirstOrDefault()?.Variant.Sku),
            cancellationToken);
        await RecordInteractionAsync(
            payment,
            new Dictionary<string, object?>
            {
                ["createdAt"] = Now(),
                ["gateway"] = session.Gateway.ToString(),
                ["sessionId"] = session.SessionId,
            },
            cancellationToken);

        _logger.LogInformation(
            "Checkout started: payment {PaymentId} ({MerchantReference}) session {SessionId} via {Gateway}",
            payment.Id,
            merchantReference,
            session.SessionId,
            session.Gateway);
        return new CheckoutSessionResponse(
            payment.Id,
            merchantReference,
            session.SessionId,
            session.SessionData,
            session.ClientKey,
            session.Environment,
            session.Gateway.ToString(),
            cart.TotalPrice.ToDecimal(),
            cart.TotalPrice,
            correlationId);
    }

    /// <summary>Passes redirect or 3D Secure details to the gateway.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The interim result.</returns>
    public async Task<PaymentActionResult> SubmitDetailsAsync(SubmitPaymentDetailsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RedirectResult) && string.IsNullOrWhiteSpace(request.ThreeDSResult))
        {
            throw DigitalSiteRequestException.BadRequest("redirectResult or threeDSResult is required");
        }

        var payment = await GetPaymentAsync(request.PaymentId, cancellationToken);
        var correlationId = payment.Custom?.GetString(DigitalSiteCustomFields.CorrelationId) ?? string.Empty;
        var result = await _gateway.HandlePaymentDetailsAsync(
            new PaymentDetailsSubmission(payment.Id, correlationId, request.RedirectResult, request.ThreeDSResult),
            cancellationToken);
        await RecordInteractionAsync(
            payment,
            new Dictionary<string, object?>
            {
                ["createdAt"] = Now(),
                ["gateway"] = _gateway.Mode.ToString(),
                ["resultCode"] = result.ResultCode,
                ["pspReference"] = result.PspReference,
            },
            cancellationToken);
        return result;
    }

    /// <summary>Reads the payment, order and provisioning state from commercetools.</summary>
    /// <param name="paymentId">The payment id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The status.</returns>
    public async Task<CheckoutStatus> GetStatusAsync(string paymentId, CancellationToken cancellationToken)
    {
        var payment = await GetPaymentAsync(paymentId, cancellationToken);
        var cartId = payment.Custom?.GetString(DigitalSiteCustomFields.CartId);
        var order = cartId is null ? null : await FindOrderAsync(cartId, cancellationToken);
        var authorisations = payment.Transactions.Where(transaction => transaction.Type == TransactionTypes.Authorization).ToArray();
        var authorised = authorisations.FirstOrDefault(transaction => transaction.State == TransactionStates.Success);
        var refused = authorisations.FirstOrDefault(transaction => transaction.State == TransactionStates.Failure);

        return new CheckoutStatus(
            payment.Id,
            authorised is not null ? PaymentStates.Authorised : refused is not null ? PaymentStates.Refused : PaymentStates.Pending,
            (authorised ?? refused)?.InteractionId,
            authorised is null && refused is not null ? RefusalReasonOf(payment) : null,
            payment.Custom?.GetString(DigitalSiteCustomFields.ProcessingResult),
            cartId,
            order?.Id,
            order?.OrderNumber,
            order?.OrderState,
            order is null ? ProvisioningStatuses.NotStarted
                : order.OrderState == OrderStates.Complete ? ProvisioningStatuses.Provisioned
                : ProvisioningStatuses.Pending,
            order?.Custom?.GetString(DigitalSiteCustomFields.HoldingEventId) is not null,
            payment.Custom?.GetString(DigitalSiteCustomFields.CorrelationId),
            payment.AmountPlanned.ToDecimal(),
            payment.AmountPlanned.CurrencyCode);
    }

    /// <summary>Gets a payment or fails with 404.</summary>
    /// <param name="paymentId">The payment id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The payment.</returns>
    public async Task<Payment> GetPaymentAsync(string? paymentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            throw DigitalSiteRequestException.BadRequest("paymentId is required");
        }

        return await _commerceTools.GetPaymentAsync(paymentId, cancellationToken)
            ?? throw DigitalSiteRequestException.NotFound("Payment not found");
    }

    private async Task<Cart> GetPayableCartAsync(string? cartId, string crmId, CancellationToken cancellationToken)
    {
        var cart = string.IsNullOrWhiteSpace(cartId) ? null : await _commerceTools.GetCartAsync(cartId, cancellationToken);
        if (cart is null || cart.AnonymousId != crmId)
        {
            throw DigitalSiteRequestException.NotFound("Cart not found");
        }

        if (cart.CartState != CartStates.Active)
        {
            throw DigitalSiteRequestException.Conflict("This cart has already been ordered");
        }

        return cart.LineItems.Count > 0 && cart.TotalPrice.CentAmount > 0
            ? cart
            : throw DigitalSiteRequestException.BadRequest("The cart is empty");
    }

    private async Task<Order?> FindOrderAsync(string cartId, CancellationToken cancellationToken)
    {
        var orders = await _commerceTools.QueryOrdersAsync(CommercePredicate.ReferenceIdEqualTo("cart", cartId), cancellationToken);
        return orders.FirstOrDefault();
    }

    private static string? RefusalReasonOf(Payment payment) =>
        payment.InterfaceInteractions
            .LastOrDefault(interaction =>
                interaction.Type?.Key == NotificationProcessor.InteractionTypeKey
                && interaction.GetString("success") == "false")
            ?.GetString("reason");

    // Session ids and result codes are kept for support; session data and card details never are.
    private async Task RecordInteractionAsync(Payment payment, Dictionary<string, object?> fields, CancellationToken cancellationToken)
    {
        try
        {
            var current = await _commerceTools.GetPaymentAsync(payment.Id, cancellationToken) ?? payment;
            await _commerceTools.UpdatePaymentAsync(
                current.Id,
                current.Version,
                [new AddInterfaceInteractionAction(ResourceReference.ByKey(ResourceTypes.Type, DigitalSiteCustomFields.SessionInteractionTypeKey), fields)],
                cancellationToken);
        }
        catch (ConcurrentModificationException)
        {
            _logger.LogInformation("Skipped recording a checkout interaction on payment {PaymentId}: it changed concurrently", payment.Id);
        }
    }

    private string ValidateReturnUrl(string? returnUrl)
    {
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw DigitalSiteRequestException.BadRequest("returnUrl must be an absolute http(s) URL");
        }

        var origin = uri.GetLeftPart(UriPartial.Authority);
        return _settings.AllowedOriginList.Contains(origin, StringComparer.OrdinalIgnoreCase)
            ? uri.ToString()
            : throw DigitalSiteRequestException.BadRequest("returnUrl must be on an allowed origin");
    }

    private string Now() => _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
}
