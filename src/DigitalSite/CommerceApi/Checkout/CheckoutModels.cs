using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Checkout;

/// <summary>Request to start paying for a cart.</summary>
/// <param name="CartId">The cart id.</param>
/// <param name="CrmId">The member's mock CRM id.</param>
/// <param name="ReturnUrl">Where a redirect payment method returns the shopper.</param>
/// <param name="AcceptedPaymentAuthTerms">Whether the payment authority terms were accepted.</param>
/// <param name="AcceptedRoadsideAssistTerms">Whether the Roadside Assistance terms were accepted.</param>
public sealed record StartCheckoutRequest(
    string? CartId,
    string? CrmId,
    string? ReturnUrl,
    bool AcceptedPaymentAuthTerms,
    bool AcceptedRoadsideAssistTerms);

/// <summary>Everything the browser needs to mount the payment form.</summary>
/// <param name="PaymentId">The commercetools payment id.</param>
/// <param name="MerchantReference">The payment key, sent to the gateway as its reference.</param>
/// <param name="SessionId">The gateway session id.</param>
/// <param name="SessionData">The opaque session data.</param>
/// <param name="ClientKey">The gateway client key; absent for the stub.</param>
/// <param name="Environment">The gateway environment.</param>
/// <param name="Gateway">The gateway: <c>Stub</c> or <c>Adyen</c>.</param>
/// <param name="Amount">The amount in dollars.</param>
/// <param name="Price">The amount as commercetools money.</param>
/// <param name="CorrelationId">The POC correlation id.</param>
public sealed record CheckoutSessionResponse(
    string PaymentId,
    string MerchantReference,
    string SessionId,
    string SessionData,
    string? ClientKey,
    string Environment,
    string Gateway,
    decimal Amount,
    Money Price,
    string CorrelationId);

/// <summary>Details from the payment form after a shopper action.</summary>
/// <param name="PaymentId">The commercetools payment id.</param>
/// <param name="RedirectResult">The redirect result.</param>
/// <param name="ThreeDSResult">The 3D Secure result.</param>
public sealed record SubmitPaymentDetailsRequest(string? PaymentId, string? RedirectResult, string? ThreeDSResult);

/// <summary>Request to simulate a payment outcome with the stub gateway.</summary>
/// <param name="Outcome"><c>Authorised</c>, <c>Refused</c> or <c>Pending</c>.</param>
public sealed record SimulatePaymentRequest(string? Outcome);

/// <summary>Where a payment, its order and provisioning have got to.</summary>
/// <param name="PaymentId">The payment id.</param>
/// <param name="PaymentState"><c>Pending</c>, <c>Authorised</c> or <c>Refused</c>.</param>
/// <param name="PspReference">The gateway reference of the authorisation.</param>
/// <param name="RefusalReason">Why the payment was refused.</param>
/// <param name="ProcessingResult">Why no order was created despite a payment, if applicable.</param>
/// <param name="CartId">The cart id.</param>
/// <param name="OrderId">The order id, once created.</param>
/// <param name="OrderNumber">The order number, once created.</param>
/// <param name="OrderState">The order state, once created.</param>
/// <param name="ProvisioningStatus"><c>NotStarted</c>, <c>Pending</c> or <c>Provisioned</c>.</param>
/// <param name="HoldingEventPublished">Whether the ProductHoldingChange event was published.</param>
/// <param name="CorrelationId">The POC correlation id.</param>
/// <param name="Amount">The amount in dollars.</param>
/// <param name="Currency">The currency.</param>
public sealed record CheckoutStatus(
    string PaymentId,
    string PaymentState,
    string? PspReference,
    string? RefusalReason,
    string? ProcessingResult,
    string? CartId,
    string? OrderId,
    string? OrderNumber,
    string? OrderState,
    string ProvisioningStatus,
    bool HoldingEventPublished,
    string? CorrelationId,
    decimal Amount,
    string Currency);

/// <summary>Payment states shown to the shopper.</summary>
public static class PaymentStates
{
    /// <summary>No final authorisation outcome yet.</summary>
    public const string Pending = "Pending";

    /// <summary>The authorisation succeeded.</summary>
    public const string Authorised = "Authorised";

    /// <summary>The authorisation failed.</summary>
    public const string Refused = "Refused";
}

/// <summary>Provisioning states shown to the shopper.</summary>
public static class ProvisioningStatuses
{
    /// <summary>No order yet.</summary>
    public const string NotStarted = "NotStarted";

    /// <summary>The order is waiting for fulfilment.</summary>
    public const string Pending = "Pending";

    /// <summary>Fulfilment completed the order.</summary>
    public const string Provisioned = "Provisioned";
}
