using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.ContactEvents.Verifier.Services;
using ServiceBusPoc.Core.Contracts;

namespace ServiceBusPoc.Tests;

public sealed class DigitalChannelsRoutingVerifierTests
{
    [Fact]
    public async Task VerifyAsync_PublishesAndCompletesAllEightScenarios()
    {
        var transport = new Mock<IDigitalChannelsRoutingTransport>();
        var publishedCorrelationIds = new List<string>();
        var messages = new Queue<ServiceBusReceivedMessage>();

        transport
            .Setup(x => x.PublishAsync(
                It.IsAny<ContactData>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<ContactData, string, CancellationToken>((_, correlationId, _) =>
            {
                publishedCorrelationIds.Add(correlationId);
                messages.Enqueue(ServiceBusModelFactory.ServiceBusReceivedMessage(
                    body: BinaryData.FromString("{}"),
                    messageId: correlationId,
                    correlationId: correlationId));
            })
            .Returns(Task.CompletedTask);
        transport
            .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => messages.Dequeue());
        transport
            .Setup(x => x.CompleteAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var verifier = new DigitalChannelsRoutingVerifier(
            transport.Object,
            Mock.Of<ILogger<DigitalChannelsRoutingVerifier>>());

        await verifier.VerifyAsync();

        Assert.Equal(8, publishedCorrelationIds.Count);
        Assert.Equal(8, publishedCorrelationIds.Distinct(StringComparer.Ordinal).Count());
        transport.Verify(
            x => x.CompleteAsync(
                It.IsAny<ServiceBusReceivedMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(8));
    }
}
