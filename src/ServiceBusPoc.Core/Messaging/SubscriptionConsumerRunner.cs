using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Receives and logs the <c>contact.events</c> messages that the broker delivers to one subscription.
/// The runner never re-evaluates capability flags: whatever arrives was already selected by the
/// subscription filter, so what a consumer logs is exactly what its filter let through.
/// One instance drives one subscription and is not re-entrant.
/// </summary>
public sealed class SubscriptionConsumerRunner
{
    private static readonly TimeSpan ReceiveWaitTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DelayAfterReceiveFailure = TimeSpan.FromSeconds(2);

    private readonly IServiceBusReceiver _receiver;
    private readonly IDashboardReporter _dashboardReporter;
    private readonly ServiceBusSettings _serviceBusSettings;
    private readonly DashboardSettings _dashboardSettings;
    private readonly ILogger<SubscriptionConsumerRunner> _logger;
    private readonly TimeProvider _timeProvider;

    private long _messagesReceived;
    private DateTimeOffset? _lastMessageAt;
    private string? _lastEventId;
    private DateTimeOffset _lastHeartbeatAt = DateTimeOffset.MinValue;

    public SubscriptionConsumerRunner(
        IServiceBusReceiver receiver,
        IDashboardReporter dashboardReporter,
        IOptions<ServiceBusSettings> serviceBusSettings,
        IOptions<DashboardSettings> dashboardSettings,
        ILogger<SubscriptionConsumerRunner> logger,
        TimeProvider timeProvider)
    {
        _receiver = receiver;
        _dashboardReporter = dashboardReporter;
        _serviceBusSettings = serviceBusSettings.Value;
        _dashboardSettings = dashboardSettings.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Waits for the Service Bus subscription to be ready by attempting to receive a message.
    /// Uses exponential backoff: 100ms, 200ms, 400ms, 800ms, 1.6s, 3.2s, 6.4s, 12.8s, 25.6s, 51.2s max.
    /// </summary>
    /// <param name="startTime">Time when the wait started.</param>
    /// <param name="cancellationToken">Token used to cancel the wait.</param>
    /// <returns>True if subscription is ready, false if timeout occurs.</returns>
    public async Task<bool> WaitForReadyAsync(DateTimeOffset startTime, CancellationToken cancellationToken)
    {
        var maxWaitDuration = TimeSpan.FromSeconds(120);
        var initialDelay = TimeSpan.FromMilliseconds(100);
        var currentDelay = initialDelay;
        var attempt = 0;

        while ((DateTimeOffset.UtcNow - startTime) < maxWaitDuration)
        {
            attempt++;
            var elapsedAtCheck = DateTimeOffset.UtcNow - startTime;

            try
            {
                // Log connection attempt start - every attempt for first 5, then every 10th
                if (attempt <= 5 || attempt % 10 == 0)
                {
                    _logger.LogInformation(
                        "Service Bus subscription connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)... [ABOUT TO TRY]",
                        attempt,
                        elapsedAtCheck.TotalSeconds);
                }
                else
                {
                    _logger.LogDebug(
                        "Service Bus subscription connection attempt {Attempt} (elapsed: {ElapsedSeconds:F1}s)...",
                        attempt,
                        elapsedAtCheck.TotalSeconds);
                }

                // Try to receive with a short timeout to verify the subscription is accessible
                await _receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(100), cancellationToken);

                var elapsedAtSuccess = DateTimeOffset.UtcNow - startTime;
                _logger.LogInformation(
                    "✓ Service Bus subscription is ready! Connected after {ElapsedSeconds:F1}s (attempt {Attempt})",
                    elapsedAtSuccess.TotalSeconds,
                    attempt);
                return true;
            }
            catch (OperationCanceledException ex)
            {
                // Log cancellation
                _logger.LogInformation(
                    ex,
                    "Service Bus subscription connection attempt {Attempt} cancelled (elapsed: {ElapsedSeconds:F1}s)",
                    attempt,
                    elapsedAtCheck.TotalSeconds);
                throw;
            }
            catch (Exception ex)
            {
                // Log exceptions immediately after catch for first 5 attempts and every 10th
                var elapsedAtError = DateTimeOffset.UtcNow - startTime;
                if (attempt <= 5 || attempt % 10 == 0)
                {
                    _logger.LogWarning(
                        ex,
                        "Connection attempt {Attempt} failed (elapsed: {ElapsedSeconds:F1}s): {ExceptionType} - {Message} [EXCEPTION CAUGHT]",
                        attempt,
                        elapsedAtError.TotalSeconds,
                        ex.GetType().Name,
                        ex.Message);
                }
                else
                {
                    _logger.LogDebug(
                        ex,
                        "Connection attempt {Attempt} failed (elapsed: {ElapsedSeconds:F1}s): {ExceptionType}",
                        attempt,
                        elapsedAtError.TotalSeconds,
                        ex.GetType().Name);
                }

                // Exponential backoff: cap at ~12.8s to avoid excessive waits
                var delayMs = (int)Math.Min(currentDelay.TotalMilliseconds, 12800);

                _logger.LogDebug(
                    "Sleeping for {DelayMs}ms before retry (exponential backoff) [ABOUT TO DELAY]",
                    delayMs);

                await Task.Delay(delayMs, cancellationToken);

                _logger.LogDebug(
                    "Sleep completed, resuming loop iteration (attempt {Attempt}) [DELAY COMPLETE]",
                    attempt + 1);

                currentDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * 2);
            }
        }

        _logger.LogError(
            "Service Bus subscription connection timeout after {TotalSeconds:F1}s and {Attempts} attempts",
            maxWaitDuration.TotalSeconds,
            attempt);

        return false;
    }

    /// <summary>
    /// Runs the receive loop until the token is cancelled.
    /// </summary>
    /// <param name="descriptor">Identity of the consuming application.</param>
    /// <param name="cancellationToken">Token used to stop the loop.</param>
    /// <exception cref="InvalidOperationException">The subscription name is not configured.</exception>
    public async Task RunAsync(ConsumerDescriptor descriptor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var subscriptionName = _serviceBusSettings.SubscriptionName;
        if (string.IsNullOrWhiteSpace(subscriptionName))
        {
            throw new InvalidOperationException(
                "ServiceBus:SubscriptionName must be configured before a consumer can receive messages.");
        }

        _logger.LogInformation(
            "Consumer {ServiceName} listening on topic {TopicName} subscription {SubscriptionName} with broker filter {FilterDescription}",
            descriptor.ServiceName,
            _serviceBusSettings.TopicName,
            subscriptionName,
            descriptor.FilterDescription);

        await ReportHeartbeatAsync(descriptor, subscriptionName, ServiceState.Starting, force: true, cancellationToken);

        try
        {
            await ReceiveLoopAsync(descriptor, subscriptionName, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Consumer {ServiceName} stopping on subscription {SubscriptionName}",
                descriptor.ServiceName,
                subscriptionName);
        }
        finally
        {
            await ReportHeartbeatAsync(descriptor, subscriptionName, ServiceState.Stopped, force: true, CancellationToken.None);

            _logger.LogInformation(
                "Consumer {ServiceName} stopped after receiving {MessageCount} messages from subscription {SubscriptionName}",
                descriptor.ServiceName,
                _messagesReceived,
                subscriptionName);
        }
    }

    private async Task ReceiveLoopAsync(
        ConsumerDescriptor descriptor,
        string subscriptionName,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ServiceBusReceivedMessage? message;

            try
            {
                message = await _receiver.ReceiveMessageAsync(ReceiveWaitTime, cancellationToken);
            }
            catch (ServiceBusException ex)
            {
                _logger.LogError(
                    ex,
                    "Consumer {ServiceName} failed to receive from subscription {SubscriptionName}: {FailureReason}",
                    descriptor.ServiceName,
                    subscriptionName,
                    ex.Reason);

                await Task.Delay(DelayAfterReceiveFailure, _timeProvider, cancellationToken);
                continue;
            }

            if (message is null)
            {
                await ReportHeartbeatAsync(descriptor, subscriptionName, ServiceState.Running, force: false, cancellationToken);
                continue;
            }

            await ProcessMessageAsync(descriptor, subscriptionName, message, cancellationToken);
            await ReportHeartbeatAsync(descriptor, subscriptionName, ServiceState.Running, force: true, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(
        ConsumerDescriptor descriptor,
        string subscriptionName,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        EventEnvelope<ContactData>? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope<ContactData>>(
                message.Body.ToString(),
                JsonSerializerOptionsHelper.DefaultOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Consumer {ServiceName} could not deserialize message {MessageId} from subscription {SubscriptionName}",
                descriptor.ServiceName,
                message.MessageId,
                subscriptionName);

            await DeadLetterAsync(descriptor, subscriptionName, message, "DeserializationFailed", ex.Message, cancellationToken);
            return;
        }

        if (envelope?.Data is null)
        {
            _logger.LogError(
                "Consumer {ServiceName} received message {MessageId} without a contact payload on subscription {SubscriptionName}",
                descriptor.ServiceName,
                message.MessageId,
                subscriptionName);

            await DeadLetterAsync(
                descriptor,
                subscriptionName,
                message,
                "InvalidEnvelope",
                "Envelope did not contain contact data.",
                cancellationToken);
            return;
        }

        RecordDelivery(envelope.Id);
        LogDelivery(descriptor, subscriptionName, envelope, envelope.Data);

        await CompleteAsync(descriptor, subscriptionName, message, cancellationToken);
    }

    private void LogDelivery(
        ConsumerDescriptor descriptor,
        string subscriptionName,
        EventEnvelope<ContactData> envelope,
        ContactData contact)
    {
        _logger.LogInformation(
            "Consumer {ServiceName} received event {EventId} of type {EventType} for contact {ContactId} " +
            "from {Source} with correlation {CorrelationId} on subscription {SubscriptionName} " +
            "(filter {FilterDescription}; hasInsurance={HasInsurance}, hasParksResorts={HasParksResorts}, hasCarwashProduct={HasCarwashProduct})",
            descriptor.ServiceName,
            envelope.Id,
            envelope.Type,
            contact.ContactId,
            envelope.Source,
            envelope.CorrelationId,
            subscriptionName,
            descriptor.FilterDescription,
            contact.Attributes?.HasInsurance ?? false,
            contact.Attributes?.HasParksResorts ?? false,
            contact.Attributes?.HasCarwashProduct ?? false);
    }

    private void RecordDelivery(string? eventId)
    {
        _messagesReceived++;
        _lastMessageAt = _timeProvider.GetUtcNow();
        _lastEventId = eventId;
    }

    private async Task CompleteAsync(
        ConsumerDescriptor descriptor,
        string subscriptionName,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _receiver.CompleteMessageAsync(message, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            _logger.LogWarning(
                ex,
                "Consumer {ServiceName} lost the lock on message {MessageId}; the broker will redeliver it on subscription {SubscriptionName}",
                descriptor.ServiceName,
                message.MessageId,
                subscriptionName);
        }
    }

    private async Task DeadLetterAsync(
        ConsumerDescriptor descriptor,
        string subscriptionName,
        ServiceBusReceivedMessage message,
        string reason,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            await _receiver.DeadLetterMessageAsync(message, reason, description, cancellationToken);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(
                ex,
                "Consumer {ServiceName} could not dead-letter message {MessageId} on subscription {SubscriptionName}: {FailureReason}",
                descriptor.ServiceName,
                message.MessageId,
                subscriptionName,
                ex.Reason);
        }
    }

    private async Task ReportHeartbeatAsync(
        ConsumerDescriptor descriptor,
        string subscriptionName,
        ServiceState state,
        bool force,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (!force && now - _lastHeartbeatAt < _dashboardSettings.HeartbeatInterval)
        {
            return;
        }

        _lastHeartbeatAt = now;

        await _dashboardReporter.ReportAsync(
            new ServiceHeartbeat
            {
                ServiceName = descriptor.ServiceName,
                SubscriptionName = subscriptionName,
                State = state,
                SentAt = now,
                MessagesHandled = _messagesReceived,
                LastMessageAt = _lastMessageAt,
                LastEventId = _lastEventId
            },
            cancellationToken);
    }
}
