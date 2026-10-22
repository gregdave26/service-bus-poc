using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;
using ServiceBusPoc.DigitalSite.Shared.Messaging;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class CommerceSubscriptionRunnerTests
{
    private const string ValidBody = """
        {"notificationType":"Message","projectKey":"rac-rsa-poc","id":"m-1","version":1,"sequenceNumber":3,
         "resource":{"typeId":"payment","id":"p-1"},"resourceVersion":4,"type":"PaymentTransactionAdded",
         "createdAt":"2026-01-01T00:00:00Z","transaction":{"id":"t-1"}}
        """;

    private readonly Mock<IServiceBusReceiver> _receiver = new();
    private readonly Mock<ICommerceMessageHandler> _handler = new();
    private readonly Mock<IDashboardReporter> _dashboard = new();
    private readonly CancellationTokenSource _cancellation = new();

    public CommerceSubscriptionRunnerTests()
    {
        _handler.SetupGet(handler => handler.ServiceName).Returns("cart-processor");
    }

    [Fact]
    public async Task RunAsync_ValidMessage_HandlesCompletesAndReports()
    {
        var message = Deliver(ValidBody);
        CommerceMessage? handled = null;
        _handler.Setup(handler => handler.HandleAsync(It.IsAny<CommerceMessage>(), It.IsAny<CancellationToken>()))
            .Callback<CommerceMessage, CancellationToken>((commerceMessage, _) => handled = commerceMessage)
            .Returns(Task.CompletedTask);

        await CreateRunner().RunAsync(_cancellation.Token);

        Assert.Equal("p-1", handled!.Resource.Id);
        Assert.Equal(3, handled.SequenceNumber);
        Assert.True(handled.Extensions!.ContainsKey("transaction"));
        _receiver.Verify(receiver => receiver.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        _dashboard.Verify(reporter => reporter.ReportMessageAsync(
            It.Is<DashboardMessage>(report => report.EventId == "m-1" && report.Direction == "received" && report.SubscriptionName == "cart-processor"),
            It.IsAny<CancellationToken>()), Times.Once);
        _dashboard.Verify(reporter => reporter.ReportAsync(
            It.Is<ServiceHeartbeat>(heartbeat => heartbeat.State == ServiceState.Stopped && heartbeat.MessagesHandled == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_HandlerFails_AbandonsForRedelivery()
    {
        var message = Deliver(ValidBody);
        _handler.Setup(handler => handler.HandleAsync(It.IsAny<CommerceMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commercetools down"));

        await CreateRunner().RunAsync(_cancellation.Token);

        _receiver.Verify(receiver => receiver.AbandonMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        _receiver.Verify(receiver => receiver.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"type":"PaymentTransactionAdded","resource":{"typeId":"payment"}}""")]
    public async Task RunAsync_InvalidMessage_DeadLetters(string body)
    {
        var message = Deliver(body);

        await CreateRunner().RunAsync(_cancellation.Token);

        _receiver.Verify(receiver => receiver.DeadLetterMessageAsync(message, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _handler.Verify(handler => handler.HandleAsync(It.IsAny<CommerceMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_DashboardFailure_StillCompletes()
    {
        var message = Deliver(ValidBody);
        _dashboard.Setup(reporter => reporter.ReportMessageAsync(It.IsAny<DashboardMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("dashboard down"));

        await CreateRunner().RunAsync(_cancellation.Token);

        _receiver.Verify(receiver => receiver.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ReceiveFailure_RetriesAfterDelay()
    {
        var calls = 0;
        _receiver.Setup(receiver => receiver.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new ServiceBusException("not ready", ServiceBusFailureReason.ServiceCommunicationProblem);
                }

                _cancellation.Cancel();
                return Task.FromResult<ServiceBusReceivedMessage?>(null);
            });

        await CreateRunner().RunAsync(_cancellation.Token);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RunAsync_MissingSubscription_Throws()
    {
        var runner = CreateRunner(subscriptionName: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(CancellationToken.None));
    }

    private ServiceBusReceivedMessage Deliver(string body)
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(BinaryData.FromString(body), messageId: "sb-1", deliveryCount: 1);
        var delivered = false;
        _receiver.Setup(receiver => receiver.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (delivered)
                {
                    _cancellation.Cancel();
                    return Task.FromResult<ServiceBusReceivedMessage?>(null);
                }

                delivered = true;
                return Task.FromResult<ServiceBusReceivedMessage?>(message);
            });
        return message;
    }

    private CommerceSubscriptionRunner CreateRunner(string? subscriptionName = "cart-processor") =>
        new(
            _receiver.Object,
            _handler.Object,
            _dashboard.Object,
            Options.Create(new ServiceBusSettings { TopicName = "commerce.events", SubscriptionName = subscriptionName }),
            Options.Create(new DashboardSettings()),
            NullLogger<CommerceSubscriptionRunner>.Instance,
            TimeProvider.System);
}
