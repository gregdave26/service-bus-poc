using System.ComponentModel.DataAnnotations;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Tests;

public sealed class ContactEventPublisherTests
{
    [Fact]
    public async Task PublishContactUpdatedAsync_ValidContact_SendsCanonicalMessage()
    {
        var sender = new Mock<IServiceBusSender>();
        ServiceBusMessage? sent = null;
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => sent = message)
            .Returns(Task.CompletedTask);
        var timestamp = new DateTimeOffset(2026, 9, 21, 2, 0, 0, TimeSpan.Zero);
        var publisher = new ContactEventPublisher(
            sender.Object,
            Mock.Of<ILogger<ContactEventPublisher>>(),
            new FixedTimeProvider(timestamp));
        var contact = ValidContact(new ContactAttributes
        {
            HasInsurance = true,
            HasParksResorts = false,
            HasCarwashProduct = true
        });

        var eventId = await publisher.PublishContactUpdatedAsync(
            contact, "crm", "correlation-1", CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Equal(eventId, sent!.MessageId);
        Assert.Equal("correlation-1", sent.CorrelationId);
        Assert.Equal(ContactEventMessage.ContactUpdatedType, sent.Subject);
        Assert.Equal(ContactEventMessage.ContentType, sent.ContentType);
        Assert.Equal(true, sent.ApplicationProperties[ContactEventMessage.HasInsuranceProperty]);
        Assert.Equal(false, sent.ApplicationProperties[ContactEventMessage.HasParksResortsProperty]);
        Assert.Equal(true, sent.ApplicationProperties[ContactEventMessage.HasCarwashProductProperty]);

        var envelope = JsonSerializer.Deserialize<EventEnvelope<ContactData>>(
            sent.Body.ToString(), ServiceBusPoc.Core.Utilities.JsonSerializerOptionsHelper.DefaultOptions);
        Assert.Equal(eventId, envelope!.Id);
        Assert.Equal("crm", envelope.Source);
        Assert.Equal(timestamp.UtcDateTime, envelope.Timestamp);
        Assert.Equal(contact.ContactId, envelope.Data!.ContactId);
        sender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PublishContactUpdatedAsync_MissingCorrelation_GeneratesCorrelationId()
    {
        var sender = new Mock<IServiceBusSender>();
        ServiceBusMessage? sent = null;
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => sent = message)
            .Returns(Task.CompletedTask);
        var publisher = new ContactEventPublisher(
            sender.Object, Mock.Of<ILogger<ContactEventPublisher>>(), TimeProvider.System);

        await publisher.PublishContactUpdatedAsync(ValidContact(), "dashboard");

        Assert.False(string.IsNullOrWhiteSpace(sent!.CorrelationId));
    }

    [Fact]
    public async Task PublishContactUpdatedAsync_NullContact_Throws()
    {
        var publisher = CreatePublisher();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => publisher.PublishContactUpdatedAsync(null!, "crm"));
    }

    [Fact]
    public async Task PublishContactUpdatedAsync_MissingSource_Throws()
    {
        var publisher = CreatePublisher();

        await Assert.ThrowsAsync<ArgumentException>(
            () => publisher.PublishContactUpdatedAsync(ValidContact(), " "));
    }

    [Fact]
    public async Task PublishContactUpdatedAsync_InvalidContact_ThrowsValidationExceptionWithoutSending()
    {
        var sender = new Mock<IServiceBusSender>();
        var publisher = new ContactEventPublisher(
            sender.Object, Mock.Of<ILogger<ContactEventPublisher>>(), TimeProvider.System);

        await Assert.ThrowsAsync<ValidationException>(
            () => publisher.PublishContactUpdatedAsync(new ContactData { ContactId = "only-id" }, "crm"));

        sender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishContactUpdatedAsync_ServiceBusFailure_RethrowsException()
    {
        var sender = new Mock<IServiceBusSender>();
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceBusException("send failed", ServiceBusFailureReason.GeneralError));
        var publisher = new ContactEventPublisher(
            sender.Object, Mock.Of<ILogger<ContactEventPublisher>>(), TimeProvider.System);

        await Assert.ThrowsAsync<ServiceBusException>(
            () => publisher.PublishContactUpdatedAsync(ValidContact(), "crm"));
    }

    private static ContactEventPublisher CreatePublisher() =>
        new(
            Mock.Of<IServiceBusSender>(),
            Mock.Of<ILogger<ContactEventPublisher>>(),
            TimeProvider.System);

    private static ContactData ValidContact(ContactAttributes? attributes = null) =>
        new()
        {
            ContactId = "C001",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Attributes = attributes
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
