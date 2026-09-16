using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.Producer.Services;

/// <summary>
/// Constructs and publishes canonical contact.updated events.
/// </summary>
public sealed class ProducerService(
    ILogger<ProducerService> logger,
    IOptions<ProducerSettings> producerOptions,
    IServiceBusMessagePublisher publisher,
    TimeProvider timeProvider)
{
    private const string EventType = "contact.updated";
    private const string DataVersion = "1";
    private const string HasInsuranceProperty = "hasInsurance";
    private const string HasParksResortsProperty = "hasParksResorts";
    private const string HasCarwashProductProperty = "hasCarwashProduct";
    private readonly ILogger<ProducerService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ProducerSettings _settings =
        producerOptions?.Value ?? throw new ArgumentNullException(nameof(producerOptions));
    private readonly IServiceBusMessagePublisher _publisher =
        publisher ?? throw new ArgumentNullException(nameof(publisher));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Creates and publishes a contact event from configured producer input.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel publishing.</param>
    /// <returns>A task representing the publish operation.</returns>
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        var contactEvent = new ContactUpdatedEvent
        {
            ContactId = _settings.ContactId,
            FirstName = _settings.FirstName,
            LastName = _settings.LastName,
            Email = _settings.Email,
            Phone = _settings.Phone,
            Attributes = new ContactAttributes
            {
                HasInsurance = _settings.HasInsurance,
                HasParksResorts = _settings.HasParksResorts,
                HasCarwashProduct = _settings.HasCarwashProduct
            }
        };

        return PublishAsync(
            contactEvent,
            _settings.Source,
            _settings.CorrelationId,
            cancellationToken);
    }

    /// <summary>
    /// Creates and publishes a canonical contact.updated message.
    /// </summary>
    /// <param name="contactEvent">The contact event data.</param>
    /// <param name="source">The originating system.</param>
    /// <param name="correlationId">An optional correlation identifier.</param>
    /// <param name="cancellationToken">A token that can cancel publishing.</param>
    /// <returns>A task representing the publish operation.</returns>
    public async Task PublishAsync(
        ContactUpdatedEvent contactEvent,
        string source,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var message = CreateMessage(contactEvent, source, correlationId);

        try
        {
            await _publisher.PublishAsync(message, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Published event {EventId} of type {EventType} for contact {ContactId} with correlation {CorrelationId}",
                message.MessageId,
                message.Subject,
                contactEvent.ContactId,
                message.CorrelationId);
        }
        catch (ServiceBusException exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish event {EventId} of type {EventType} for contact {ContactId} with correlation {CorrelationId}",
                message.MessageId,
                message.Subject,
                contactEvent.ContactId,
                message.CorrelationId);
            throw;
        }
    }

    /// <summary>
    /// Constructs a canonical contact.updated Service Bus message.
    /// </summary>
    /// <param name="contactEvent">The contact event data.</param>
    /// <param name="source">The originating system.</param>
    /// <param name="correlationId">An optional correlation identifier.</param>
    /// <returns>The constructed Service Bus message.</returns>
    public ServiceBusMessage CreateMessage(
        ContactUpdatedEvent contactEvent,
        string source,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(contactEvent);
        Validator.ValidateObject(contactEvent, new ValidationContext(contactEvent), true);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ValidationException("Event source is required.");
        }

        var eventId = Guid.NewGuid().ToString();
        var effectiveCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? eventId
            : correlationId;
        var envelope = new EventEnvelope<ContactUpdatedEvent>
        {
            Id = eventId,
            Type = EventType,
            Source = source,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            DataVersion = DataVersion,
            CorrelationId = effectiveCorrelationId,
            Data = contactEvent
        };
        Validator.ValidateObject(envelope, new ValidationContext(envelope), true);

        var body = JsonSerializer.Serialize(envelope, JsonSerializerOptionsHelper.DefaultOptions);
        var message = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            CorrelationId = effectiveCorrelationId,
            MessageId = eventId,
            Subject = EventType
        };

        var attributes = contactEvent.Attributes ?? new ContactAttributes();
        message.ApplicationProperties[HasInsuranceProperty] = attributes.HasInsurance;
        message.ApplicationProperties[HasParksResortsProperty] = attributes.HasParksResorts;
        message.ApplicationProperties[HasCarwashProductProperty] = attributes.HasCarwashProduct;

        return message;
    }
}
