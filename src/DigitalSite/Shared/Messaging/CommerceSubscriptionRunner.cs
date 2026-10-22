using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.Shared.Messaging;

/// <summary>
/// Receives commercetools messages from one <c>commerce.events</c> subscription and passes them to a handler.
/// Malformed messages are dead-lettered; handler failures are abandoned so the broker retries them
/// until its maximum delivery count dead-letters them.
/// </summary>
public sealed class CommerceSubscriptionRunner
{
    private static readonly TimeSpan ReceiveWaitTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DelayAfterReceiveFailure = TimeSpan.FromSeconds(2);

    private readonly IServiceBusReceiver _receiver;
    private readonly ICommerceMessageHandler _handler;
    private readonly IDashboardReporter _dashboardReporter;
    private readonly ServiceBusSettings _serviceBusSettings;
    private readonly DashboardSettings _dashboardSettings;
    private readonly ILogger<CommerceSubscriptionRunner> _logger;
    private readonly TimeProvider _timeProvider;

    private long _messagesHandled;
    private DateTimeOffset? _lastMessageAt;
    private string? _lastEventId;
    private DateTimeOffset _lastHeartbeatAt = DateTimeOffset.MinValue;

    /// <summary>Initializes a new instance of the <see cref="CommerceSubscriptionRunner"/> class.</summary>
    public CommerceSubscriptionRunner(
        IServiceBusReceiver receiver,
        ICommerceMessageHandler handler,
        IDashboardReporter dashboardReporter,
        IOptions<ServiceBusSettings> serviceBusSettings,
        IOptions<DashboardSettings> dashboardSettings,
        ILogger<CommerceSubscriptionRunner> logger,
        TimeProvider timeProvider)
    {
        _receiver = receiver;
        _handler = handler;
        _dashboardReporter = dashboardReporter;
        _serviceBusSettings = serviceBusSettings.Value;
        _dashboardSettings = dashboardSettings.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>Runs the receive loop until the token is cancelled.</summary>
    /// <param name="cancellationToken">Token used to stop the loop.</param>
    /// <returns>A task that completes when the loop stops.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var subscriptionName = _serviceBusSettings.SubscriptionName
            ?? throw new InvalidOperationException("ServiceBus:SubscriptionName must be configured.");

        _logger.LogInformation(
            "Commerce consumer {ServiceName} listening on topic {TopicName} subscription {SubscriptionName}",
            _handler.ServiceName,
            _serviceBusSettings.TopicName,
            subscriptionName);

        await ReportHeartbeatAsync(subscriptionName, ServiceState.Starting, force: true, cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await TryReceiveAsync(subscriptionName, cancellationToken);
                if (message is not null)
                {
                    await ProcessAsync(subscriptionName, message, cancellationToken);
                }

                await ReportHeartbeatAsync(subscriptionName, ServiceState.Running, force: message is not null, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Commerce consumer {ServiceName} stopping", _handler.ServiceName);
        }
        finally
        {
            await ReportHeartbeatAsync(subscriptionName, ServiceState.Stopped, force: true, CancellationToken.None);
        }
    }

    private async Task<ServiceBusReceivedMessage?> TryReceiveAsync(string subscriptionName, CancellationToken cancellationToken)
    {
        try
        {
            return await _receiver.ReceiveMessageAsync(ReceiveWaitTime, cancellationToken);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(
                ex,
                "Commerce consumer {ServiceName} failed to receive from subscription {SubscriptionName}: {FailureReason}",
                _handler.ServiceName,
                subscriptionName,
                ex.Reason);
            await Task.Delay(DelayAfterReceiveFailure, _timeProvider, cancellationToken);
            return null;
        }
    }

    private async Task ProcessAsync(string subscriptionName, ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        var body = message.Body.ToString();
        CommerceMessage? commerceMessage;
        try
        {
            commerceMessage = JsonSerializer.Deserialize<CommerceMessage>(body, JsonSerializerOptionsHelper.DefaultOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Commerce consumer {ServiceName} could not deserialize message {MessageId}", _handler.ServiceName, message.MessageId);
            await _receiver.DeadLetterMessageAsync(message, "DeserializationFailed", ex.Message, cancellationToken);
            return;
        }

        if (commerceMessage is null || string.IsNullOrWhiteSpace(commerceMessage.Resource.Id) || string.IsNullOrWhiteSpace(commerceMessage.Type))
        {
            _logger.LogError("Commerce consumer {ServiceName} received message {MessageId} without a resource or type", _handler.ServiceName, message.MessageId);
            await _receiver.DeadLetterMessageAsync(message, "InvalidMessage", "Message had no resource id or type.", cancellationToken);
            return;
        }

        _logger.LogInformation(
            "Commerce consumer {ServiceName} received {MessageType} #{SequenceNumber} for {ResourceType} {ResourceId} (delivery {DeliveryCount}, correlation {CorrelationId})",
            _handler.ServiceName,
            commerceMessage.Type,
            commerceMessage.SequenceNumber,
            commerceMessage.Resource.TypeId,
            commerceMessage.Resource.Id,
            message.DeliveryCount,
            message.CorrelationId);

        try
        {
            await _handler.HandleAsync(commerceMessage, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Commerce consumer {ServiceName} failed to handle {MessageType} {MessageId}; abandoning for redelivery",
                _handler.ServiceName,
                commerceMessage.Type,
                commerceMessage.Id);
            await _receiver.AbandonMessageAsync(message, cancellationToken);
            return;
        }

        _messagesHandled++;
        _lastMessageAt = _timeProvider.GetUtcNow();
        _lastEventId = commerceMessage.Id;
        await ReportMessageAsync(subscriptionName, message, commerceMessage, body, cancellationToken);
        await _receiver.CompleteMessageAsync(message, cancellationToken);
    }

    private async Task ReportMessageAsync(
        string subscriptionName,
        ServiceBusReceivedMessage message,
        CommerceMessage commerceMessage,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dashboardReporter.ReportMessageAsync(
                new DashboardMessage
                {
                    MessageId = message.MessageId,
                    EventId = commerceMessage.Id,
                    ServiceName = _handler.ServiceName,
                    Direction = "received",
                    Timestamp = _timeProvider.GetUtcNow(),
                    SubscriptionName = subscriptionName,
                    Payload = body
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Dashboard message report failed for {MessageId}; message flow continues", commerceMessage.Id);
        }
    }

    private async Task ReportHeartbeatAsync(string subscriptionName, ServiceState state, bool force, CancellationToken cancellationToken)
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
                ServiceName = _handler.ServiceName,
                SubscriptionName = subscriptionName,
                State = state,
                SentAt = now,
                MessagesHandled = _messagesHandled,
                LastMessageAt = _lastMessageAt,
                LastEventId = _lastEventId
            },
            cancellationToken);
    }
}
