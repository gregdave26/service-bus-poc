using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.ContactEvents.Verifier.Services;
using Azure.Messaging.ServiceBus;

namespace ServiceBusPoc.Tests;

public sealed class VerifierServiceTests
{
    [Fact]
    public async Task RunAsync_WhenTopologyIsValid_Completes()
    {
        var validator = new Mock<ITopologyValidator>();
        validator.Setup(x => x.ValidateAsync()).ReturnsAsync(true);
        var transport = new Mock<IRoutingScenarioTransport>();
        SetupRoutingBroker(transport);
        var service = CreateService(validator.Object, transport.Object);

        await service.RunAsync();

        validator.Verify(x => x.ValidateAsync(), Times.Once);
        transport.Verify(
            x => x.PublishAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(8));
        transport.Verify(
            x => x.ReceiveAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(32));
    }

    [Fact]
    public async Task RunAsync_ReceivesOnlyFromVerifierOwnedSubscriptions()
    {
        var validator = new Mock<ITopologyValidator>();
        validator.Setup(x => x.ValidateAsync()).ReturnsAsync(true);
        var transport = new Mock<IRoutingScenarioTransport>();
        SetupRoutingBroker(transport);
        var service = CreateService(validator.Object, transport.Object);

        await service.RunAsync();

        var receivedFrom = transport.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IRoutingScenarioTransport.ReceiveAsync))
            .Select(invocation => (string)invocation.Arguments[0])
            .ToHashSet();
        Assert.Equal(
            SubscriptionNames.All.Select(SubscriptionNames.ForVerifier).ToHashSet(),
            receivedFrom);
    }

    private static void SetupRoutingBroker(Mock<IRoutingScenarioTransport> transport)
    {
        ServiceBusMessage? publishedMessage = null;
        transport
            .Setup(x => x.PublishAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => publishedMessage = message)
            .Returns(Task.CompletedTask);
        transport
            .Setup(x => x.ReceiveAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string subscriptionName, TimeSpan _, CancellationToken _) =>
            {
                Assert.NotNull(publishedMessage);
                var expected = subscriptionName == SubscriptionNames.ForVerifier(SubscriptionNames.DigitalChannels)
                    || subscriptionName == SubscriptionNames.ForVerifier(SubscriptionNames.Insurance)
                        && IsEnabled("hasInsurance")
                    || subscriptionName == SubscriptionNames.ForVerifier(SubscriptionNames.ParksResorts)
                        && IsEnabled("hasParksResorts")
                    || subscriptionName == SubscriptionNames.ForVerifier(SubscriptionNames.Carwash)
                        && IsEnabled("hasCarwashProduct");

                return expected
                    ? ServiceBusModelFactory.ServiceBusReceivedMessage(
                        publishedMessage!.Body,
                        messageId: publishedMessage.MessageId)
                    : null;

                bool IsEnabled(string propertyName) =>
                    publishedMessage!.ApplicationProperties[propertyName] is true;
            });
    }

    [Fact]
    public async Task RunAsync_WhenTopologyIsInvalid_Throws()
    {
        var validator = new Mock<ITopologyValidator>();
        validator.Setup(x => x.ValidateAsync()).ReturnsAsync(false);
        var service = CreateService(validator.Object, Mock.Of<IRoutingScenarioTransport>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync());

        validator.Verify(x => x.ValidateAsync(), Times.Once);
    }

    private static VerifierService CreateService(
        ITopologyValidator validator,
        IRoutingScenarioTransport transport) =>
        new(
            Mock.Of<ILogger<VerifierService>>(),
            Options.Create(new ServiceBusSettings { Namespace = "test-namespace" }),
            validator,
            transport);
}
