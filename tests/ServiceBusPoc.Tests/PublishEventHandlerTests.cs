using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.Dashboard.Api;

namespace ServiceBusPoc.Tests;

public sealed class PublishEventHandlerTests
{
    [Fact]
    public async Task PublishEventAsync_ValidRequest_PublishesMappedContactAndReturnsEventId()
    {
        var sender = new Mock<IServiceBusSender>();
        ServiceBusMessage? sentMessage = null;
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => sentMessage = message)
            .Returns(Task.CompletedTask);
        var handler = CreateHandler(sender);

        var result = await handler.PublishEventAsync(new PublishEventRequest
        {
            ContactId = "C001",
            FirstName = "Ada",
            LastName = "Lovelace",
            Phone = "+1 555 0100",
            Email = "ada@example.com",
            RacId = "RAC-1",
            HasInsurance = true,
            HasParksResorts = true,
            HasCarwashProduct = false
        });

        Assert.True(result.Success);
        Assert.Equal(sentMessage!.MessageId, result.EventId);
        var envelope = JsonSerializer.Deserialize<EventEnvelope<ContactData>>(
            sentMessage.Body.ToString(),
            ServiceBusPoc.Core.Utilities.JsonSerializerOptionsHelper.DefaultOptions);
        Assert.NotNull(envelope);
        Assert.Equal("dashboard", envelope!.Source);
        Assert.Equal("C001", envelope.Data!.ContactId);
        Assert.Equal("Ada", envelope.Data.FirstName);
        Assert.Equal("+1 555 0100", envelope.Data.Phone);
        Assert.True(envelope.Data.Attributes!.HasInsurance);
        Assert.True(envelope.Data.Attributes.HasParksResorts);
        Assert.False(envelope.Data.Attributes.HasCarwashProduct);
        Assert.Equal("RAC-1", envelope.Data.Attributes.RacId);
        sender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PublishEventAsync_PublisherFails_ReturnsFailureResponse()
    {
        var sender = new Mock<IServiceBusSender>();
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        var handler = CreateHandler(sender);

        var result = await handler.PublishEventAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Null(result.EventId);
        Assert.Equal("broker unavailable", result.Error);
    }

    [Fact]
    public async Task PublishEventAsync_NullRequest_Throws()
    {
        var handler = CreateHandler(new Mock<IServiceBusSender>());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.PublishEventAsync(null!));
    }

    private static PublishEventHandler CreateHandler(Mock<IServiceBusSender> sender) =>
        new(
            new ContactEventPublisher(
                sender.Object,
                Mock.Of<ILogger<ContactEventPublisher>>(),
                TimeProvider.System),
            Mock.Of<ILogger<PublishEventHandler>>());

    private static PublishEventRequest ValidRequest() =>
        new()
        {
            ContactId = "C001",
            FirstName = "Ada",
            LastName = "Lovelace",
            Phone = "+1 555 0100",
            Email = "ada@example.com"
        };
}
