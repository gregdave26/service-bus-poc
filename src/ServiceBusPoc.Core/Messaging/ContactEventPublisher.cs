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

        ValidateContact(contact);

        var envelope = CreateEnvelope(contact, source, correlationId);
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

    private static void ValidateContact(ContactData contact)
    {
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            contact,
            new ValidationContext(contact),
            validationResults,
            validateAllProperties: true);

        if (isValid)
        {
            return;
        }

        var messages = string.Join("; ", validationResults.Select(result => result.ErrorMessage));
        throw new ValidationException($"Contact payload is invalid: {messages}");
    }

    private EventEnvelope<ContactData> CreateEnvelope(ContactData contact, string source, string? correlationId) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            Type = ContactEventMessage.ContactUpdatedType,
            Source = source,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            DataVersion = ContactEventMessage.ContactUpdatedDataVersion,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Data = contact
        };

    private static ServiceBusMessage CreateMessage(EventEnvelope<ContactData> envelope, ContactAttributes? attributes)
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
