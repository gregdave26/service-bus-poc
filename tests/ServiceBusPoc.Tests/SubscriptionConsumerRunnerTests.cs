using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Tests;

public sealed class SubscriptionConsumerRunnerTests
{
    [Fact]
    public async Task WaitForReadyAsync_WhenReceiveSucceeds_ReturnsTrue()
    {
        var receiver = new Mock<IServiceBusReceiver>();
        receiver.Setup(x => x.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceBusReceivedMessage?)null);
        var runner = CreateRunner(receiver);

        var result = await runner.WaitForReadyAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.True(result);
        receiver.Verify(x => x.ReceiveMessageAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task WaitForReadyAsync_WhenCancelled_RethrowsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var receiver = new Mock<IServiceBusReceiver>();
        receiver.Setup(x => x.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var runner = CreateRunner(receiver);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.WaitForReadyAsync(DateTimeOffset.UtcNow, cancellation.Token));
    }

    [Fact]
    public async Task WaitForReadyAsync_WhenReceiveFails_RetriesAndHonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var receiver = new Mock<IServiceBusReceiver>();
        receiver.Setup(x => x.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, _) => cancellation.Cancel())
            .ThrowsAsync(new ServiceBusException("not ready", ServiceBusFailureReason.GeneralError));
        var runner = CreateRunner(receiver);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.WaitForReadyAsync(DateTimeOffset.UtcNow, cancellation.Token));

        receiver.Verify(x => x.ReceiveMessageAsync(TimeSpan.FromMilliseconds(100), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task RunAsync_NullDescriptor_ThrowsArgumentNullException()
    {
        var runner = CreateRunner();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => runner.RunAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_MissingSubscription_ThrowsInvalidOperationException()
    {
        var runner = CreateRunner(subscriptionName: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(Descriptor(), CancellationToken.None));

        Assert.Contains("SubscriptionName", exception.Message);
    }

    [Fact]
    public async Task RunAsync_Cancellation_ReportsStartingAndStoppedHeartbeats()
    {
        using var cancellation = new CancellationTokenSource();
        var receiver = new Mock<IServiceBusReceiver>();
        receiver.Setup(x => x.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, _) => cancellation.Cancel())
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var dashboard = new Mock<IDashboardReporter>();
        var runner = CreateRunner(receiver, dashboard);

        await runner.RunAsync(Descriptor(), cancellation.Token);

        dashboard.Verify(x => x.ReportAsync(
            It.Is<ServiceHeartbeat>(h => h.State == ServiceState.Starting && h.SubscriptionName == "insurance"),
            cancellation.Token), Times.Once);
        dashboard.Verify(x => x.ReportAsync(
            It.Is<ServiceHeartbeat>(h => h.State == ServiceState.Stopped && h.MessagesHandled == 0),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ValidMessage_ReportsMessageAndCompletes()
    {
        using var cancellation = new CancellationTokenSource();
        var message = Message(ValidEnvelope("event-1"));
        var receiver = new Mock<IServiceBusReceiver>();
        receiver.SetupSequence(x => x.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message)
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromException<ServiceBusReceivedMessage?>(
                    new OperationCanceledException(cancellation.Token));
            });
        var dashboard = new Mock<IDashboardReporter>();
        var runner = CreateRunner(receiver, dashboard);

        await runner.RunAsync(Descriptor(), cancellation.Token);

        receiver.Verify(x => x.CompleteMessageAsync(message, cancellation.Token), Times.Once);
        dashboard.Verify(x => x.ReportMessageAsync(
            It.Is<DashboardMessage>(m =>
                m.MessageId == "message-1" &&
                m.EventId == "event-1" &&
                m.ServiceName == "insurance" &&
                m.Direction == "received" &&
                m.SubscriptionName == "insurance"),
            cancellation.Token), Times.Once);
        dashboard.Verify(x => x.ReportAsync(
            It.Is<ServiceHeartbeat>(h =>
                h.State == ServiceState.Running &&
                h.MessagesHandled == 1 &&
                h.LastEventId == "event-1"),
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task RunAsync_NoMessage_ReportsRunningHeartbeat()
    {
        using var cancellation = new CancellationTokenSource();
        var receiver = new Mock<IServiceBusReceiver>();
        receiver.Setup(x => x.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, _) => cancellation.Cancel())
            .ReturnsAsync((ServiceBusReceivedMessage?)null);
        var dashboard = new Mock<IDashboardReporter>();
        var runner = CreateRunner(receiver, dashboard, heartbeatIntervalSeconds: 0);

        await runner.RunAsync(Descriptor(), cancellation.Token);

        dashboard.Verify(x => x.ReportAsync(
            It.Is<ServiceHeartbeat>(h => h.State == ServiceState.Running),
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task RunAsync_InvalidJson_DeadLettersWithoutCompleting()
    {
        using var cancellation = new CancellationTokenSource();
        var message = Message("{ not-json");
        var receiver = ReceiverThatCancelsAfter(message, cancellation);
        var runner = CreateRunner(receiver);

        await runner.RunAsync(Descriptor(), cancellation.Token);

        receiver.Verify(x => x.DeadLetterMessageAsync(
            message,
            "DeserializationFailed",
            It.IsAny<string>(),
            cancellation.Token), Times.Once);
        receiver.Verify(x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_MissingPayload_DeadLettersAsInvalidEnvelope()
    {
        using var cancellation = new CancellationTokenSource();
        var message = Message(JsonSerializer.Serialize(new EventEnvelope<ContactData>
        {
            Id = "event-2",
            Type = "contact.updated",
            Source = "crm",
            Timestamp = DateTime.UtcNow,
            DataVersion = "1.0"
        }));
        var receiver = ReceiverThatCancelsAfter(message, cancellation);
        var runner = CreateRunner(receiver);

        await runner.RunAsync(Descriptor(), cancellation.Token);

        receiver.Verify(x => x.DeadLetterMessageAsync(
            message,
            "InvalidEnvelope",
            "Envelope did not contain contact data.",
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DashboardMessageFailure_StillCompletesMessage()
    {
        using var cancellation = new CancellationTokenSource();
        var message = Message(ValidEnvelope("event-3"));
        var receiver = ReceiverThatCancelsAfter(message, cancellation);
        var dashboard = new Mock<IDashboardReporter>();
        dashboard.Setup(x => x.ReportMessageAsync(It.IsAny<DashboardMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("dashboard unavailable"));
        var runner = CreateRunner(receiver, dashboard);

        await runner.RunAsync(Descriptor(), cancellation.Token);

        receiver.Verify(x => x.CompleteMessageAsync(message, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LockLostWhileCompleting_IsHandled()
    {
        using var cancellation = new CancellationTokenSource();
        var message = Message(ValidEnvelope("event-4"));
        var receiver = ReceiverThatCancelsAfter(message, cancellation);
        receiver.Setup(x => x.CompleteMessageAsync(message, cancellation.Token))
            .ThrowsAsync(new ServiceBusException("lock lost", ServiceBusFailureReason.MessageLockLost));
        var runner = CreateRunner(receiver);

        await runner.RunAsync(Descriptor(), cancellation.Token);
    }

    [Fact]
    public async Task RunAsync_DeadLetterFailure_IsHandled()
    {
        using var cancellation = new CancellationTokenSource();
        var message = Message("{ invalid");
        var receiver = ReceiverThatCancelsAfter(message, cancellation);
        receiver.Setup(x => x.DeadLetterMessageAsync(
                message, It.IsAny<string>(), It.IsAny<string>(), cancellation.Token))
            .ThrowsAsync(new ServiceBusException("dead-letter failed", ServiceBusFailureReason.GeneralError));
        var runner = CreateRunner(receiver);

        await runner.RunAsync(Descriptor(), cancellation.Token);
    }

    [Fact]
    public async Task RunAsync_ReceiveFailure_RetriesUntilCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var receiver = new Mock<IServiceBusReceiver>();
        receiver.Setup(x => x.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<TimeSpan, CancellationToken>((_, _) => cancellation.Cancel())
            .ThrowsAsync(new ServiceBusException("receive failed", ServiceBusFailureReason.GeneralError));
        var runner = CreateRunner(receiver);

        await runner.RunAsync(Descriptor(), cancellation.Token);

        receiver.Verify(x => x.ReceiveMessageAsync(TimeSpan.FromSeconds(5), cancellation.Token), Times.Once);
    }

    private static SubscriptionConsumerRunner CreateRunner(
        Mock<IServiceBusReceiver>? receiver = null,
        Mock<IDashboardReporter>? dashboard = null,
        string? subscriptionName = "insurance",
        int heartbeatIntervalSeconds = 300)
    {
        return new(
            (receiver ?? new Mock<IServiceBusReceiver>()).Object,
            (dashboard ?? new Mock<IDashboardReporter>()).Object,
            Options.Create(new ServiceBusSettings
            {
                TopicName = "contact.events",
                SubscriptionName = subscriptionName
            }),
            Options.Create(new DashboardSettings { HeartbeatIntervalSeconds = heartbeatIntervalSeconds }),
            Mock.Of<ILogger<SubscriptionConsumerRunner>>(),
            TimeProvider.System);
    }

    private static Mock<IServiceBusReceiver> ReceiverThatCancelsAfter(
        ServiceBusReceivedMessage message,
        CancellationTokenSource cancellation)
    {
        var receiver = new Mock<IServiceBusReceiver>();
        receiver.SetupSequence(x => x.ReceiveMessageAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(message)
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromException<ServiceBusReceivedMessage?>(
                    new OperationCanceledException(cancellation.Token));
            });
        return receiver;
    }

    private static ConsumerDescriptor Descriptor() => new("insurance", "hasInsurance = true");

    private static ServiceBusReceivedMessage Message(string body) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: "message-1");

    private static string ValidEnvelope(string eventId) =>
        JsonSerializer.Serialize(new EventEnvelope<ContactData>
        {
            Id = eventId,
            Type = "contact.updated",
            Source = "crm",
            Timestamp = DateTime.UtcNow,
            DataVersion = "1.0",
            CorrelationId = "correlation-1",
            Data = new ContactData
            {
                ContactId = "C001",
                FirstName = "Ada",
                LastName = "Lovelace"
            }
        });
}
