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
/// Implements <see cref="IMessageReceiveOrchestrator"/> to coordinate with shared receive-loop logic.
/// </summary>
public sealed class CommerceSubscriptionRunner : IMessageReceiveOrchestrator
{
    private readonly IServiceBusReceiver _receiver;
    private readonly ICommerceMessageHandler _handler;
    private readonly IDashboardReporter _dashboardReporter;
    private readonly ServiceBusSettings _serviceBusSettings;
    private readonly DashboardSettings _dashboardSettings;
    private readonly ILogger<CommerceSubscriptionRunner> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IMessageReceiveLoopRunner _receiveLoopRunner;

    private long _messagesHandled;
    private DateTimeOffset? _lastMessageAt;
    private string? _lastEventId;
    private DateTimeOffset _lastHeartbeatAt = DateTimeOffset.MinValue;

    // Stored subscription name for orchestrator callbacks
    private string? _currentSubscriptionName;

    /// <summary>Initializes a new instance of the <see cref="CommerceSubscriptionRunner"/> class.</summary>
    public CommerceSubscriptionRunner(
        IServiceBusReceiver receiver,
        ICommerceMessageHandler handler,
        IDashboardReporter dashboardReporter,
        IOptions<ServiceBusSettings> serviceBusSettings,
        IOptions<DashboardSettings> dashboardSettings,
        ILogger<CommerceSubscriptionRunner> logger,
        TimeProvider timeProvider,
        IMessageReceiveLoopRunner? receiveLoopRunner = null)
    {
        _receiver = receiver;
        _handler = handler;
        _dashboardReporter = dashboardReporter;
        _serviceBusSettings = serviceBusSettings.Value;
        _dashboardSettings = dashboardSettings.Value;
        _logger = logger;
        _timeProvider = timeProvider;
        _receiveLoopRunner = receiveLoopRunner ?? new ServiceBusMessageReceiveLoopRunner(receiver, logger);
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

        // Store subscription name for use in orchestrator callbacks
        _currentSubscriptionName = subscriptionName;

        await ReportHeartbeatAsync(subscriptionName, ServiceState.Starting, force: true, cancellationToken);
        try
        {
            // Use the shared receive-loop runner to orchestrate message reception
            await _receiveLoopRunner.RunAsync(this, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Commerce consumer {ServiceName} stopping", _handler.ServiceName);
        }
        finally
        {
            await ReportHeartbeatAsync(subscriptionName, ServiceState.Stopped, force: true, CancellationToken.None);
            _currentSubscriptionName = null;
        }
    }

    /// <summary>
    /// Implements <see cref="IMessageReceiveOrchestrator.ProcessMessageAsync"/> to handle received messages.
    /// </summary>
    async Task IMessageReceiveOrchestrator.ProcessMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        if (_currentSubscriptionName is null)
        {
            _logger.LogError("ProcessMessageAsync called without subscription context");
            return;
        }

        await ProcessAsync(_currentSubscriptionName, message, cancellationToken);
        await ReportHeartbeatAsync(_currentSubscriptionName, ServiceState.Running, force: true, cancellationToken);
    }

    /// <summary>
    /// Implements <see cref="IMessageReceiveOrchestrator.OnEmptyReceiveAsync"/> to handle timeouts.
    /// </summary>
    async Task IMessageReceiveOrchestrator.OnEmptyReceiveAsync(CancellationToken cancellationToken)
    {
        if (_currentSubscriptionName is null)
        {
            return;
        }

        await ReportHeartbeatAsync(_currentSubscriptionName, ServiceState.Running, force: false, cancellationToken);
    }

    /// <summary>
    /// Implements <see cref="IMessageReceiveOrchestrator.OnReceiveFailureAsync"/> to handle receive errors.
    /// </summary>
    async Task IMessageReceiveOrchestrator.OnReceiveFailureAsync(ServiceBusException exception, CancellationToken cancellationToken)
    {
        if (_currentSubscriptionName is null)
        {
            return;
        }

        _logger.LogError(
            exception,
            "Commerce consumer {ServiceName} failed to receive from subscription {SubscriptionName}: {FailureReason}",
            _handler.ServiceName,
            _currentSubscriptionName,
            exception.Reason);
        
        // Delay before retry (delay is handled by receive-loop runner)
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
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
