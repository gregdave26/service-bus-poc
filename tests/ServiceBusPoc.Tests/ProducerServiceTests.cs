using System.ComponentModel.DataAnnotations;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.Producer.Services;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="ProducerService"/>.
/// </summary>
public sealed class ProducerServiceTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 9, 16, 5, 30, 0, TimeSpan.Zero);

    [Fact]
    public void CreateMessage_ValidContact_ConstructsCanonicalEnvelope()
    {
        var service = CreateService();
        var contactEvent = CreateContactEvent();

        var message = service.CreateMessage(contactEvent, "crm", "correlation-123");
        var envelope = JsonSerializer.Deserialize<EventEnvelope<ContactUpdatedEvent>>(
            message.Body,
            JsonSerializerOptionsHelper.DefaultOptions);

        Assert.NotNull(envelope);
        Assert.True(Guid.TryParse(envelope.Id, out _));
        Assert.Equal("contact.updated", envelope.Type);
        Assert.Equal("crm", envelope.Source);
        Assert.Equal(FixedTime.UtcDateTime, envelope.Timestamp);
        Assert.Equal("1", envelope.DataVersion);
        Assert.Equal("correlation-123", envelope.CorrelationId);
        Assert.NotNull(envelope.Data);
        Assert.Equal("C001", envelope.Data.ContactId);
        Assert.DoesNotContain("\"contact\":", message.Body.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateMessage_ValidContact_SetsRoutingAndBrokerProperties()
    {
        var service = CreateService();
        var contactEvent = CreateContactEvent();

        var message = service.CreateMessage(contactEvent, "crm", "correlation-123");
        var envelope = JsonSerializer.Deserialize<EventEnvelope<ContactUpdatedEvent>>(
            message.Body,
            JsonSerializerOptionsHelper.DefaultOptions);

        Assert.Equal("application/json", message.ContentType);
        Assert.Equal("contact.updated", message.Subject);
        Assert.Equal("correlation-123", message.CorrelationId);
        Assert.Equal(message.MessageId, envelope!.Id);
        Assert.Equal(true, message.ApplicationProperties["hasInsurance"]);
        Assert.Equal(false, message.ApplicationProperties["hasParksResorts"]);
        Assert.Equal(true, message.ApplicationProperties["hasCarwashProduct"]);
    }

    [Fact]
    public async Task PublishAsync_ValidContact_PublishesOnce()
    {
        var publisher = new Mock<IServiceBusMessagePublisher>();
        var service = CreateService(publisher);

        await service.PublishAsync(CreateContactEvent(), "crm", "correlation-123");

        publisher.Verify(
            candidate => candidate.PublishAsync(
                It.Is<ServiceBusMessage>(message =>
                    message.Subject == "contact.updated"
                    && message.ApplicationProperties["hasInsurance"].Equals(true)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ServiceBusFailure_LogsAndRethrows()
    {
        var publisher = new Mock<IServiceBusMessagePublisher>();
        var logger = new Mock<ILogger<ProducerService>>();
        var exception = new ServiceBusException(
            "Service Bus unavailable",
            ServiceBusFailureReason.ServiceCommunicationProblem);
        publisher
            .Setup(candidate => candidate.PublishAsync(
                It.IsAny<ServiceBusMessage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        var service = CreateService(publisher, logger);

        var actual = await Assert.ThrowsAsync<ServiceBusException>(
            () => service.PublishAsync(CreateContactEvent(), "crm"));

        Assert.Same(exception, actual);
        logger.Verify(
            candidate => candidate.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Failed to publish event", StringComparison.Ordinal)),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_InvalidContact_ThrowsWithoutPublishing()
    {
        var publisher = new Mock<IServiceBusMessagePublisher>();
        var service = CreateService(publisher);
        var contactEvent = CreateContactEvent();
        contactEvent.ContactId = " ";

        await Assert.ThrowsAsync<ValidationException>(
            () => service.PublishAsync(contactEvent, "crm"));

        publisher.Verify(
            candidate => candidate.PublishAsync(
                It.IsAny<ServiceBusMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ConfiguredInput_PublishesConfiguredContact()
    {
        var publisher = new Mock<IServiceBusMessagePublisher>();
        var settings = CreateSettings();
        var service = CreateService(publisher, settings: settings);

        await service.RunAsync();

        publisher.Verify(
            candidate => candidate.PublishAsync(
                It.Is<ServiceBusMessage>(message =>
                    message.ApplicationProperties["hasInsurance"].Equals(true)
                    && message.ApplicationProperties["hasParksResorts"].Equals(false)
                    && message.ApplicationProperties["hasCarwashProduct"].Equals(true)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Comprehensive test suite for all 8 boolean combinations of routing properties.
    /// Ensures proper subscription filtering for all attribute combinations.
    /// </summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void CreateMessage_AllBooleanCombinations_SetsCorrectRoutingProperties(
        bool hasInsurance,
        bool hasParksResorts,
        bool hasCarwashProduct)
    {
        var service = CreateService();
        var contactEvent = new ContactUpdatedEvent
        {
            ContactId = "C-combo-test",
            FirstName = "Test",
            LastName = "Contact",
            Attributes = new ContactAttributes
            {
                HasInsurance = hasInsurance,
                HasParksResorts = hasParksResorts,
                HasCarwashProduct = hasCarwashProduct
            }
        };

        var message = service.CreateMessage(contactEvent, "test-source", "test-correlation");

        Assert.Equal(hasInsurance, message.ApplicationProperties["hasInsurance"]);
        Assert.Equal(hasParksResorts, message.ApplicationProperties["hasParksResorts"]);
        Assert.Equal(hasCarwashProduct, message.ApplicationProperties["hasCarwashProduct"]);
    }

    [Fact]
    public void CreateMessage_NullAttributes_DefaultsAllPropertiesToFalse()
    {
        var service = CreateService();
        var contactEvent = new ContactUpdatedEvent
        {
            ContactId = "C-null-attrs",
            FirstName = "Test",
            LastName = "Contact",
            Attributes = null
        };

        var message = service.CreateMessage(contactEvent, "test-source", "test-id");

        Assert.Equal(false, message.ApplicationProperties["hasInsurance"]);
        Assert.Equal(false, message.ApplicationProperties["hasParksResorts"]);
        Assert.Equal(false, message.ApplicationProperties["hasCarwashProduct"]);
    }

    [Fact]
    public void CreateMessage_NullSource_ThrowsValidationException()
    {
        var service = CreateService();
        var contactEvent = CreateContactEvent();

        var exception = Assert.Throws<ValidationException>(
            () => service.CreateMessage(contactEvent, null!, "correlation"));

        Assert.Contains("source is required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateMessage_WhitespaceSource_ThrowsValidationException()
    {
        var service = CreateService();
        var contactEvent = CreateContactEvent();

        var exception = Assert.Throws<ValidationException>(
            () => service.CreateMessage(contactEvent, "  ", "correlation"));

        Assert.Contains("source is required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateMessage_NullContactEvent_ThrowsArgumentNullException()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(
            () => service.CreateMessage(null!, "crm", "correlation"));
    }

    [Fact]
    public void CreateMessage_MissingContactId_ThrowsValidationException()
    {
        var service = CreateService();
        var contactEvent = new ContactUpdatedEvent
        {
            FirstName = "John",
            LastName = "Smith",
            ContactId = null
        };

        Assert.Throws<ValidationException>(
            () => service.CreateMessage(contactEvent, "crm", "correlation"));
    }

    [Fact]
    public void CreateMessage_WithoutCorrelationId_GeneratesNewEventIdAsCorrelationId()
    {
        var service = CreateService();
        var contactEvent = CreateContactEvent();

        var message = service.CreateMessage(contactEvent, "crm", null);
        var envelope = JsonSerializer.Deserialize<EventEnvelope<ContactUpdatedEvent>>(
            message.Body,
            JsonSerializerOptionsHelper.DefaultOptions);

        Assert.NotNull(envelope);
        Assert.Equal(message.MessageId, envelope.Id);
        Assert.Equal(envelope.Id, message.CorrelationId);
        Assert.Equal(envelope.CorrelationId, envelope.Id);
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_PassesThroughToPublisher()
    {
        var publisher = new Mock<IServiceBusMessagePublisher>();
        var service = CreateService(publisher);
        var cts = new CancellationTokenSource();

        await service.PublishAsync(CreateContactEvent(), "crm", "correlation-123", cts.Token);

        publisher.Verify(
            candidate => candidate.PublishAsync(
                It.IsAny<ServiceBusMessage>(),
                cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithCancellationToken_PassesThroughToPublish()
    {
        var publisher = new Mock<IServiceBusMessagePublisher>();
        var settings = CreateSettings();
        var service = CreateService(publisher, settings: settings);
        var cts = new CancellationTokenSource();

        await service.RunAsync(cts.Token);

        publisher.Verify(
            candidate => candidate.PublishAsync(
                It.IsAny<ServiceBusMessage>(),
                cts.Token),
            Times.Once);
    }

    [Fact]
    public void CreateMessage_MultipleCallsGenerate_UniqueMessageIds()
    {
        var service = CreateService();
        var contactEvent = CreateContactEvent();

        var message1 = service.CreateMessage(contactEvent, "crm", null);
        var message2 = service.CreateMessage(contactEvent, "crm", null);

        Assert.NotEqual(message1.MessageId, message2.MessageId);
    }

    [Fact]
    public void CreateMessage_CorrelationIdWithWhitespace_TreatsAsEmpty()
    {
        var service = CreateService();
        var contactEvent = CreateContactEvent();

        var message = service.CreateMessage(contactEvent, "crm", "   ");
        var envelope = JsonSerializer.Deserialize<EventEnvelope<ContactUpdatedEvent>>(
            message.Body,
            JsonSerializerOptionsHelper.DefaultOptions);

        Assert.NotNull(envelope);
        // Whitespace-only correlation ID should be treated as null and replaced with message ID
        Assert.Equal(message.MessageId, message.CorrelationId);
        Assert.Equal(envelope.Id, envelope.CorrelationId);
    }

    [Fact]
    public void CreateMessage_ValidatesEnvelopeMetadata()
    {
        var service = CreateService();
        var contactEvent = CreateContactEvent();

        var message = service.CreateMessage(contactEvent, "crm", "correlation-123");
        var envelope = JsonSerializer.Deserialize<EventEnvelope<ContactUpdatedEvent>>(
            message.Body,
            JsonSerializerOptionsHelper.DefaultOptions);

        Assert.NotNull(envelope);
        // Verify envelope metadata is complete and valid
        Assert.NotNull(envelope.Id);
        Assert.Equal(EventType, envelope.Type);
        Assert.NotNull(envelope.Source);
        Assert.NotEqual(default, envelope.Timestamp);
        Assert.Equal("1", envelope.DataVersion);
        Assert.NotNull(envelope.CorrelationId);
        Assert.NotNull(envelope.Data);
    }

    [Fact]
    public async Task PublishAsync_LogsSuccessMessage()
    {
        var publisher = new Mock<IServiceBusMessagePublisher>();
        var logger = new Mock<ILogger<ProducerService>>();
        var service = CreateService(publisher, logger);

        await service.PublishAsync(CreateContactEvent(), "crm", "correlation-123");

        logger.Verify(
            candidate => candidate.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Published event", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private const string EventType = "contact.updated";

    private static ProducerService CreateService(
        Mock<IServiceBusMessagePublisher>? publisher = null,
        Mock<ILogger<ProducerService>>? logger = null,
        ProducerSettings? settings = null)
    {
        return new ProducerService(
            (logger ?? new Mock<ILogger<ProducerService>>()).Object,
            Options.Create(settings ?? CreateSettings()),
            (publisher ?? new Mock<IServiceBusMessagePublisher>()).Object,
            new FixedTimeProvider(FixedTime));
    }

    private static ProducerSettings CreateSettings() => new()
    {
        ContactId = "C001",
        FirstName = "John",
        LastName = "Smith",
        Source = "crm",
        CorrelationId = "correlation-123",
        HasInsurance = true,
        HasParksResorts = false,
        HasCarwashProduct = true
    };

    private static ContactUpdatedEvent CreateContactEvent() => new()
    {
        ContactId = "C001",
        FirstName = "John",
        LastName = "Smith",
        Email = "john.smith@example.test",
        Attributes = new ContactAttributes
        {
            HasInsurance = true,
            HasParksResorts = false,
            HasCarwashProduct = true
        }
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
