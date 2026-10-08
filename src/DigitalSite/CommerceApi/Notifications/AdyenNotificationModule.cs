using ServiceBusPoc.DigitalSite.CommerceApi.Payments;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Notifications;

/// <summary>Receives payment webhooks: verify, map, then apply each authentic item.</summary>
public sealed class AdyenNotificationModule
{
    /// <summary>The body Adyen expects back to stop retrying.</summary>
    public const string AcceptedResponse = "[accepted]";

    private readonly IPaymentGatewayAdapter _gateway;
    private readonly NotificationProcessor _processor;
    private readonly ILogger<AdyenNotificationModule> _logger;

    /// <summary>Initializes a new instance of the <see cref="AdyenNotificationModule"/> class.</summary>
    /// <param name="gateway">The payment gateway adapter.</param>
    /// <param name="processor">The commercetools notification processor.</param>
    /// <param name="logger">The logger.</param>
    public AdyenNotificationModule(IPaymentGatewayAdapter gateway, NotificationProcessor processor, ILogger<AdyenNotificationModule> logger)
    {
        _gateway = gateway;
        _processor = processor;
        _logger = logger;
    }

    /// <summary>Handles a webhook body.</summary>
    /// <param name="payload">The raw body.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome for each authentic item.</returns>
    /// <exception cref="FormatException">The body is not a notification.</exception>
    /// <remarks>
    /// Failures to update commercetools propagate so the endpoint returns an error and Adyen
    /// redelivers; already-applied items are recognised as duplicates on the retry.
    /// </remarks>
    public async Task<IReadOnlyList<NotificationOutcome>> HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var verification = _gateway.VerifyWebhook(payload);
        if (verification.RejectedCount > 0)
        {
            _logger.LogWarning("Rejected {RejectedCount} notification item(s) with a missing or invalid HMAC signature", verification.RejectedCount);
        }

        var outcomes = new List<NotificationOutcome>(verification.Authentic.Count);
        foreach (var notification in verification.Authentic)
        {
            var update = _gateway.MapWebhookToPaymentUpdate(notification);
            outcomes.Add(await _processor.ApplyAsync(update, cancellationToken));
        }

        return outcomes;
    }
}
