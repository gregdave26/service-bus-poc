using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;
using ServiceBusPoc.DigitalSite.Shared.Dashboard;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Messaging;

/// <summary>
/// Sends messages to the <c>commerce.events</c> topic. The message type is promoted to the
/// <c>messageType</c> application property so subscriptions can filter on it.
/// </summary>
public sealed class ServiceBusCommerceMessagePublisher : ICommerceMessagePublisher
{
    private readonly IServiceBusSender _sender;
    private readonly IDashboardReporter _dashboardReporter;
    private readonly IServiceActivity _activity;
    private readonly ILogger<ServiceBusCommerceMessagePublisher> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ServiceBusCommerceMessagePublisher"/> class.</summary>
    public ServiceBusCommerceMessagePublisher(
        IServiceBusSender sender,
        IDashboardReporter dashboardReporter,
        IServiceActivity activity,
        ILogger<ServiceBusCommerceMessagePublisher> logger,
        TimeProvider timeProvider)
    {
        _sender = sender;
        _dashboardReporter = dashboardReporter;
        _activity = activity;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var commerceMessage = message.Message;
        var body = JsonSerializer.Serialize(commerceMessage, StubJson.Options);
        var serviceBusMessage = new ServiceBusMessage(body)
        {
            MessageId = commerceMessage.Id,
            CorrelationId = message.CorrelationId,
            Subject = commerceMessage.Type,
            ContentType = ContactEventMessage.ContentType
        };
        serviceBusMessage.ApplicationProperties[CommerceMessageTypes.MessageTypeProperty] = commerceMessage.Type;

        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
        _activity.Record(commerceMessage.Id);
        _logger.LogInformation(
            "Emitted {MessageType} #{SequenceNumber} for {ResourceType} {ResourceId} (message {MessageId}, correlation {CorrelationId})",
            commerceMessage.Type,
            commerceMessage.SequenceNumber,
            commerceMessage.Resource.TypeId,
            commerceMessage.Resource.Id,
            commerceMessage.Id,
            message.CorrelationId);

        try
        {
            await _dashboardReporter.ReportMessageAsync(
                new DashboardMessage
                {
                    MessageId = commerceMessage.Id,
                    EventId = commerceMessage.Id,
                    ServiceName = _activity.ServiceName,
                    Direction = "sent",
                    Timestamp = _timeProvider.GetUtcNow(),
                    Payload = body
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Dashboard message report failed for {MessageId}; message flow continues", commerceMessage.Id);
        }
    }
}
