using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Publishes canonical <c>contact.events</c> envelopes to the topic.
/// Capability flags are promoted to application properties so the broker, not the consumer,
/// decides which subscriptions receive each message.
/// </summary>
public sealed class ContactEventPublisher
{
    private readonly IServiceBusSender _sender;
    private readonly ILogger<ContactEventPublisher> _logger;
    private readonly TimeProvider _timeProvider;

    public ContactEventPublisher(
        IServiceBusSender sender,
        ILogger<ContactEventPublisher> logger,
        TimeProvider timeProvider)
    {
        _sender = sender;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Validates and publishes a <c>contact.updated</c> event.
    /// </summary>
    /// <param name="contact">The contact payload; validated against its data annotations.</param>
    /// <param name="source">The publishing system, for example <c>crm</c> or <c>dashboard</c>.</param>
    /// <param name="correlationId">Optional correlation identifier; generated when omitted.</param>
    /// <param name="cancellationToken">Token used to cancel the publish.</param>
    /// <returns>The identifier of the published event.</returns>
    /// <exception cref="ArgumentException">The source is missing.</exception>
    /// <exception cref="ValidationException">The contact payload is invalid.</exception>
    public async Task<string> PublishContactUpdatedAsync(
        ContactData contact,
        string source,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Validate(contact, "Contact");

        var envelope = CreateEnvelope(
            contact,
            ContactEventMessage.ContactUpdatedType,
            ContactEventMessage.ContactUpdatedDataVersion,
            source,
            correlationId);
        var message = CreateMessage(envelope, contact.Attributes);

        try
        {
            await _sender.SendMessageAsync(message, cancellationToken);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish event {EventId} for contact {ContactId} from {Source}: {FailureReason}",
                envelope.Id,
                contact.ContactId,
                source,
                ex.Reason);
            throw;
        }

        _logger.LogInformation(
            "Published event {EventId} of type {EventType} for contact {ContactId} from {Source} with correlation {CorrelationId} " +
            "(hasInsurance={HasInsurance}, hasParksResorts={HasParksResorts}, hasCarwashProduct={HasCarwashProduct})",
            envelope.Id,
            envelope.Type,
            contact.ContactId,
            source,
            envelope.CorrelationId,
            contact.Attributes?.HasInsurance ?? false,
            contact.Attributes?.HasParksResorts ?? false,
            contact.Attributes?.HasCarwashProduct ?? false);

        return envelope.Id!;
    }

    /// <summary>
    /// Validates and publishes a <c>ProductHoldingChange</c> event. Holding changes carry no capability
    /// flags, so only unfiltered subscriptions receive them.
    /// </summary>
    /// <param name="holdingChange">The holding change payload; validated against its data annotations.</param>
    /// <param name="source">The publishing system, for example <c>digital-site</c>.</param>
    /// <param name="correlationId">Optional correlation identifier; generated when omitted.</param>
    /// <param name="cancellationToken">Token used to cancel the publish.</param>
    /// <returns>The identifier of the published event.</returns>
    /// <exception cref="ArgumentException">The source is missing.</exception>
    /// <exception cref="ValidationException">The holding change payload is invalid.</exception>
    public async Task<string> PublishProductHoldingChangeAsync(
        ProductHoldingChangeData holdingChange,
        string source,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(holdingChange);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Validate(holdingChange, "Product holding change");

        var envelope = CreateEnvelope(
            holdingChange,
            ContactEventMessage.ProductHoldingChangeType,
            ContactEventMessage.ProductHoldingChangeDataVersion,
            source,
            correlationId);
        var message = CreateMessage(envelope, attributes: null);

        try
        {
            await _sender.SendMessageAsync(message, cancellationToken);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish event {EventId} for holding {HoldingId} of contact {ContactId} from {Source}: {FailureReason}",
                envelope.Id,
                holdingChange.HoldingId,
                holdingChange.ContactId,
                source,
                ex.Reason);
            throw;
        }

        _logger.LogInformation(
            "Published event {EventId} of type {EventType} for holding {HoldingId} ({ProductType} {HoldingAction}) of contact {ContactId} from {Source} with correlation {CorrelationId}",
            envelope.Id,
            envelope.Type,
            holdingChange.HoldingId,
            holdingChange.ProductType,
            holdingChange.Action,
            holdingChange.ContactId,
            source,
            envelope.CorrelationId);

        return envelope.Id!;
    }

    private static void Validate(object payload, string payloadName)
    {
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            payload,
            new ValidationContext(payload),
            validationResults,
            validateAllProperties: true);

        if (isValid)
        {
            return;
        }

        var messages = string.Join("; ", validationResults.Select(result => result.ErrorMessage));
        throw new ValidationException($"{payloadName} payload is invalid: {messages}");
    }

    private EventEnvelope<TData> CreateEnvelope<TData>(
        TData data,
        string type,
        string dataVersion,
        string source,
        string? correlationId)
        where TData : class =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Source = source,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            DataVersion = dataVersion,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Data = data
        };

    private static ServiceBusMessage CreateMessage<TData>(EventEnvelope<TData> envelope, ContactAttributes? attributes)
        where TData : class
    {
        var payload = JsonSerializer.Serialize(envelope, JsonSerializerOptionsHelper.DefaultOptions);

        var message = new ServiceBusMessage(payload)
        {
            MessageId = envelope.Id,
            CorrelationId = envelope.CorrelationId,
            Subject = envelope.Type,
            ContentType = ContactEventMessage.ContentType
        };

        message.ApplicationProperties[ContactEventMessage.HasInsuranceProperty] = attributes?.HasInsurance ?? false;
        message.ApplicationProperties[ContactEventMessage.HasParksResortsProperty] = attributes?.HasParksResorts ?? false;
        message.ApplicationProperties[ContactEventMessage.HasCarwashProductProperty] = attributes?.HasCarwashProduct ?? false;

        return message;
    }
}
