using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Notifications;

/// <summary>
/// Maps Adyen event codes to commercetools transactions. Ported from the official
/// commercetools-adyen-integration connector (<c>adyen-events.json</c>) so a later move to the
/// connector does not change payment history semantics.
/// </summary>
public static class AdyenEventMapping
{
    private static readonly Dictionary<(string EventCode, bool Success), (string? Type, string State)> Mappings = new()
    {
        [("AUTHORISATION", true)] = (TransactionTypes.Authorization, TransactionStates.Success),
        [("AUTHORISATION", false)] = (TransactionTypes.Authorization, TransactionStates.Failure),
        [("AUTHORISATION_ADJUSTMENT", true)] = (TransactionTypes.Authorization, TransactionStates.Pending),
        [("AUTHORISATION_ADJUSTMENT", false)] = (TransactionTypes.Authorization, TransactionStates.Pending),
        [("CANCELLATION", true)] = (TransactionTypes.CancelAuthorization, TransactionStates.Success),
        [("CANCELLATION", false)] = (TransactionTypes.CancelAuthorization, TransactionStates.Failure),
        [("CANCEL_OR_REFUND", true)] = (null, TransactionStates.Success),
        [("CANCEL_OR_REFUND", false)] = (null, TransactionStates.Failure),
        [("CAPTURE", true)] = (TransactionTypes.Charge, TransactionStates.Success),
        [("CAPTURE", false)] = (TransactionTypes.Charge, TransactionStates.Failure),
        [("CAPTURE_FAILED", true)] = (TransactionTypes.Charge, TransactionStates.Failure),
        [("REFUND", true)] = (TransactionTypes.Refund, TransactionStates.Success),
        [("REFUND", false)] = (TransactionTypes.Refund, TransactionStates.Failure),
        [("REFUND_FAILED", true)] = (TransactionTypes.Refund, TransactionStates.Failure),
        [("REFUNDED_REVERSED", true)] = (TransactionTypes.Refund, TransactionStates.Failure),
        [("CHARGEBACK", true)] = (TransactionTypes.Chargeback, TransactionStates.Success),
        [("SECOND_CHARGEBACK", true)] = (TransactionTypes.Chargeback, TransactionStates.Success),
        [("SECOND_CHARGEBACK", false)] = (TransactionTypes.Chargeback, TransactionStates.Failure),
        [("NOTIFICATION_OF_CHARGEBACK", true)] = (TransactionTypes.Chargeback, TransactionStates.Pending),
        [("PREARBITRATION_WON", true)] = (TransactionTypes.Chargeback, TransactionStates.Pending),
        [("PREARBITRATION_LOST", true)] = (TransactionTypes.Chargeback, TransactionStates.Pending),
        [("REQUEST_FOR_INFORMATION", true)] = (TransactionTypes.Chargeback, TransactionStates.Pending),
    };

    /// <summary>Maps a notification to a payment update.</summary>
    /// <param name="notification">The verified notification.</param>
    /// <returns>The update; unmapped events record an interaction only.</returns>
    public static PaymentUpdate ToPaymentUpdate(PaymentNotification notification)
    {
        if (!Mappings.TryGetValue((notification.EventCode.ToUpperInvariant(), notification.Success), out var mapping))
        {
            return new PaymentUpdate(notification, null, null);
        }

        var type = mapping.Type ?? CancelOrRefundType(notification.ModificationAction);
        return type is null
            ? new PaymentUpdate(notification, null, null)
            : new PaymentUpdate(notification, type, mapping.State);
    }

    private static string? CancelOrRefundType(string? modificationAction) =>
        modificationAction?.ToLowerInvariant() switch
        {
            "cancel" => TransactionTypes.CancelAuthorization,
            "refund" => TransactionTypes.Refund,
            _ => null,
        };
}
