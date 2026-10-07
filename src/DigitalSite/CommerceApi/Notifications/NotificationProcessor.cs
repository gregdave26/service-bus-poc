using System.Globalization;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Notifications;

/// <summary>What happened when a notification was applied.</summary>
public enum NotificationOutcome
{
    /// <summary>The transaction changed and an interaction was recorded.</summary>
    TransactionUpdated,

    /// <summary>Only an interaction was recorded.</summary>
    InteractionRecorded,

    /// <summary>The notification had already been applied.</summary>
    Duplicate,

    /// <summary>No payment has the notification's merchant reference.</summary>
    UnknownPayment,
}

/// <summary>
/// Applies verified payment notifications to commercetools payments, following the official
/// Adyen connector: transactions are matched on <c>interactionId</c> (the PSP reference), states
/// only move forward, and every notification is kept as an interface interaction.
/// </summary>
public sealed class NotificationProcessor
{
    /// <summary>The interface interaction type for notifications, as used by the connector.</summary>
    public const string InteractionTypeKey = "ctp-adyen-integration-interaction-notification";

    private const int MaxAttempts = 3;

    private static readonly Dictionary<string, int> StateRanks = new()
    {
        [TransactionStates.Initial] = 0,
        [TransactionStates.Pending] = 1,
        [TransactionStates.Success] = 2,
        [TransactionStates.Failure] = 3,
    };

    private readonly ICommerceToolsClient _commerceTools;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationProcessor> _logger;

    /// <summary>Initializes a new instance of the <see cref="NotificationProcessor"/> class.</summary>
    /// <param name="commerceTools">The commercetools client.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger.</param>
    public NotificationProcessor(ICommerceToolsClient commerceTools, TimeProvider timeProvider, ILogger<NotificationProcessor> logger)
    {
        _commerceTools = commerceTools;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Applies an update idempotently, retrying when the payment changes concurrently.</summary>
    /// <param name="update">The update.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<NotificationOutcome> ApplyAsync(PaymentUpdate update, CancellationToken cancellationToken)
    {
        var notification = update.Notification;
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MerchantReference"] = notification.MerchantReference,
            ["PspReference"] = notification.PspReference,
            ["EventCode"] = notification.EventCode,
        });

        for (var attempt = 1; ; attempt++)
        {
            var payment = await _commerceTools.GetPaymentByKeyAsync(notification.MerchantReference, cancellationToken);
            if (payment is null)
            {
                _logger.LogWarning("Notification ignored: no payment has merchant reference {MerchantReference}", notification.MerchantReference);
                return NotificationOutcome.UnknownPayment;
            }

            if (HasInteractionFor(payment, notification))
            {
                _logger.LogInformation("Duplicate notification for payment {PaymentId} ignored", payment.Id);
                return NotificationOutcome.Duplicate;
            }

            var transactionAction = TransactionActionFor(payment, update);
            var actions = transactionAction is null
                ? new AbstractUpdateAction[] { InteractionFor(notification) }
                : [transactionAction, InteractionFor(notification)];

            try
            {
                await _commerceTools.UpdatePaymentAsync(payment.Id, payment.Version, actions, cancellationToken);
            }
            catch (ConcurrentModificationException) when (attempt < MaxAttempts)
            {
                _logger.LogInformation("Payment {PaymentId} changed concurrently; retrying notification (attempt {Attempt})", payment.Id, attempt);
                continue;
            }

            var outcome = transactionAction is null ? NotificationOutcome.InteractionRecorded : NotificationOutcome.TransactionUpdated;
            _logger.LogInformation(
                "Notification {EventCode} success={Success} applied to payment {PaymentId}: {Outcome} ({TransactionType} {TransactionState})",
                notification.EventCode,
                notification.Success,
                payment.Id,
                outcome,
                update.TransactionType,
                update.TransactionState);
            return outcome;
        }
    }

    /// <summary>Gets a value indicating whether a transaction may move between two states.</summary>
    /// <param name="transactionType">The transaction type.</param>
    /// <param name="currentState">The current state.</param>
    /// <param name="newState">The proposed state.</param>
    /// <returns><see langword="true"/> when the change moves forward or corrects a failed authorisation.</returns>
    public static bool CanChangeState(string transactionType, string currentState, string newState)
    {
        if (transactionType == TransactionTypes.Authorization
            && currentState == TransactionStates.Failure
            && newState == TransactionStates.Success)
        {
            return true;
        }

        return StateRanks.GetValueOrDefault(newState) > StateRanks.GetValueOrDefault(currentState);
    }

    private static bool HasInteractionFor(Payment payment, PaymentNotification notification)
    {
        var success = FormatSuccess(notification.Success);
        return payment.InterfaceInteractions.Any(interaction =>
            interaction.Type?.Key == InteractionTypeKey
            && interaction.GetString("eventCode") == notification.EventCode
            && interaction.GetString("pspReference") == notification.PspReference
            && interaction.GetString("success") == success);
    }

    private static AbstractUpdateAction? TransactionActionFor(Payment payment, PaymentUpdate update)
    {
        if (!update.ChangesTransaction)
        {
            return null;
        }

        var notification = update.Notification;
        var existing = payment.Transactions.FirstOrDefault(transaction =>
            transaction.InteractionId == notification.PspReference && transaction.Type == update.TransactionType);
        if (existing is null)
        {
            return new AddTransactionAction(new TransactionDraft
            {
                Type = update.TransactionType!,
                State = update.TransactionState!,
                Amount = notification.Amount,
                InteractionId = notification.PspReference,
                Timestamp = notification.EventDate,
            });
        }

        return CanChangeState(existing.Type, existing.State, update.TransactionState!)
            ? new ChangeTransactionStateAction(existing.Id, update.TransactionState!)
            : null;
    }

    private AddInterfaceInteractionAction InteractionFor(PaymentNotification notification) =>
        new(
            ResourceReference.ByKey(ResourceTypes.Type, InteractionTypeKey),
            new Dictionary<string, object?>
            {
                ["createdAt"] = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                ["eventCode"] = notification.EventCode,
                ["success"] = FormatSuccess(notification.Success),
                ["pspReference"] = notification.PspReference,
                ["originalReference"] = notification.OriginalReference,
                ["merchantReference"] = notification.MerchantReference,
                ["amount"] = notification.Amount.CentAmount,
                ["currency"] = notification.Amount.CurrencyCode,
                ["paymentMethod"] = notification.PaymentMethod,
                ["reason"] = notification.Reason,
            });

    private static string FormatSuccess(bool success) => success ? "true" : "false";
}
