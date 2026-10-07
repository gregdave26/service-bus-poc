using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>What the gateway needs to start a checkout session for a cart.</summary>
/// <param name="CartId">The commercetools cart id.</param>
/// <param name="PaymentId">The commercetools payment id.</param>
/// <param name="MerchantReference">The payment key, sent to the gateway as its reference.</param>
/// <param name="Amount">The amount to authorise.</param>
/// <param name="ReturnUrl">Where the shopper returns after a redirect payment method.</param>
/// <param name="CorrelationId">The POC correlation id.</param>
/// <param name="CrmId">The member's mock CRM id.</param>
/// <param name="Sku">The representative product SKU.</param>
public sealed record CheckoutSessionRequest(
    string CartId,
    string PaymentId,
    string MerchantReference,
    Money Amount,
    string ReturnUrl,
    string CorrelationId,
    string? CrmId,
    string? Sku);

/// <summary>A gateway checkout session the browser uses to mount the payment form.</summary>
/// <param name="SessionId">The session id.</param>
/// <param name="SessionData">The opaque session data for the client component.</param>
/// <param name="ClientKey">The public client key; absent for the stub gateway.</param>
/// <param name="Environment">The gateway environment, for example <c>test</c>.</param>
/// <param name="Gateway">The gateway that created the session.</param>
public sealed record CheckoutSession(
    string SessionId,
    string SessionData,
    string? ClientKey,
    string Environment,
    PaymentGatewayMode Gateway);

/// <summary>Details returned by the client component after a payment action, such as a redirect.</summary>
/// <param name="PaymentId">The commercetools payment id.</param>
/// <param name="CorrelationId">The POC correlation id.</param>
/// <param name="RedirectResult">The redirect result from the return URL.</param>
/// <param name="ThreeDSResult">The 3D Secure result.</param>
public sealed record PaymentDetailsSubmission(
    string PaymentId,
    string CorrelationId,
    string? RedirectResult,
    string? ThreeDSResult);

/// <summary>The gateway's interim answer to a payment-details submission.</summary>
/// <param name="ResultCode">The gateway result code, for example <c>Authorised</c>.</param>
/// <param name="PspReference">The gateway reference, when known.</param>
/// <param name="RequiresAction">Whether the shopper must complete another action.</param>
public sealed record PaymentActionResult(string ResultCode, string? PspReference, bool RequiresAction);

/// <summary>A verified, gateway-neutral payment notification.</summary>
/// <param name="EventCode">The event code, for example <c>AUTHORISATION</c>.</param>
/// <param name="Success">Whether the event succeeded.</param>
/// <param name="PspReference">The gateway reference of this event.</param>
/// <param name="OriginalReference">The gateway reference of the original payment, for modifications.</param>
/// <param name="MerchantReference">Our payment key.</param>
/// <param name="MerchantAccountCode">The merchant account.</param>
/// <param name="Amount">The event amount.</param>
/// <param name="EventDate">When the event happened.</param>
/// <param name="Reason">The gateway reason, for example a refusal reason.</param>
/// <param name="PaymentMethod">The payment method, for example <c>visa</c>.</param>
/// <param name="ModificationAction">The action for <c>CANCEL_OR_REFUND</c>: <c>cancel</c> or <c>refund</c>.</param>
public sealed record PaymentNotification(
    string EventCode,
    bool Success,
    string PspReference,
    string? OriginalReference,
    string MerchantReference,
    string? MerchantAccountCode,
    Money Amount,
    DateTimeOffset? EventDate,
    string? Reason,
    string? PaymentMethod,
    string? ModificationAction);

/// <summary>The result of verifying a notification payload.</summary>
/// <param name="Authentic">Notification items whose signature is valid.</param>
/// <param name="RejectedCount">The number of items with a missing or invalid signature.</param>
public sealed record WebhookVerification(IReadOnlyList<PaymentNotification> Authentic, int RejectedCount);

/// <summary>The commercetools change a notification implies.</summary>
/// <param name="Notification">The source notification.</param>
/// <param name="TransactionType">The transaction type to record, or <see langword="null"/> when only an interaction is recorded.</param>
/// <param name="TransactionState">The transaction state to record, or <see langword="null"/> when only an interaction is recorded.</param>
public sealed record PaymentUpdate(PaymentNotification Notification, string? TransactionType, string? TransactionState)
{
    /// <summary>Gets a value indicating whether the notification changes a transaction.</summary>
    public bool ChangesTransaction => TransactionType is not null && TransactionState is not null;
}
