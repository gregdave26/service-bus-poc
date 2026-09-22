using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;

namespace ServiceBusPoc.Tests;

public sealed class HttpDashboardReporterTests
{
    [Fact]
    public async Task ReportAsync_SuccessfulResponse_PostsHeartbeatToHeartbeatRoute()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var reporter = CreateReporter(handler);
        var heartbeat = ValidHeartbeat();

        await reporter.ReportAsync(heartbeat);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/heartbeat", handler.Request.RequestUri!.AbsolutePath);
        var postedHeartbeat = await handler.Request.Content!.ReadFromJsonAsync<ServiceHeartbeat>(
            ServiceBusPoc.Core.Utilities.JsonSerializerOptionsHelper.DefaultOptions);
        Assert.NotNull(postedHeartbeat);
        Assert.Equal(heartbeat.ServiceName, postedHeartbeat!.ServiceName);
        Assert.Equal(heartbeat.State, postedHeartbeat.State);
        Assert.Equal(heartbeat.SentAt, postedHeartbeat.SentAt);
        Assert.Equal(heartbeat.MessagesHandled, postedHeartbeat.MessagesHandled);
    }

    [Fact]
    public async Task ReportAsync_TransportFailure_IsAbsorbed()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            throw new HttpRequestException("dashboard unavailable"));
        using var reporter = CreateReporter(handler);

        await reporter.ReportAsync(ValidHeartbeat());

        Assert.NotNull(handler.Request);
    }

    [Fact]
    public async Task ReportAsync_NonSuccessResponse_IsAbsorbed()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway));
        using var reporter = CreateReporter(handler);

        await reporter.ReportAsync(ValidHeartbeat());

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ReportAsync_Timeout_IsAbsorbed()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            throw new TaskCanceledException("timed out"));
        using var reporter = CreateReporter(handler);

        await reporter.ReportAsync(ValidHeartbeat());

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ReportAsync_Cancellation_IsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new CancellationHttpMessageHandler();
        using var reporter = CreateReporter(handler);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => reporter.ReportAsync(ValidHeartbeat(), cancellation.Token));
    }

    [Fact]
    public async Task ReportAsync_RepeatedFailures_AreLoggedOnlyOnceUntilRecovery()
    {
        var logger = new Mock<ILogger<HttpDashboardReporter>>();
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var reporter = CreateReporter(handler, logger.Object);

        await reporter.ReportAsync(ValidHeartbeat());
        await reporter.ReportAsync(ValidHeartbeat());

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("not delivered")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReportAsync_NonSuccessThenSuccess_ReportsBothAttempts()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK)
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        using var reporter = CreateReporter(handler);

        await reporter.ReportAsync(ValidHeartbeat());
        await reporter.ReportAsync(ValidHeartbeat());

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task ReportAsync_FailureThenSuccess_ReportsRecovery()
    {
        var logger = new Mock<ILogger<HttpDashboardReporter>>();
        var responses = new Queue<HttpResponseMessage>(
        [
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK)
        ]);
        using var reporter = CreateReporter(
            new RecordingHttpMessageHandler(_ => responses.Dequeue()), logger.Object);

        await reporter.ReportAsync(ValidHeartbeat());
        await reporter.ReportAsync(ValidHeartbeat());

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("resumed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReportMessageAsync_SuccessfulResponse_PostsMessageToMessageRoute()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var reporter = CreateReporter(handler);
        var message = ValidMessage();

        await reporter.ReportMessageAsync(message);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/messages", handler.Request.RequestUri!.AbsolutePath);
        var postedMessage = await handler.Request.Content!.ReadFromJsonAsync<DashboardMessage>(
            ServiceBusPoc.Core.Utilities.JsonSerializerOptionsHelper.DefaultOptions);
        Assert.NotNull(postedMessage);
        Assert.Equal(message.MessageId, postedMessage!.MessageId);
        Assert.Equal(message.EventId, postedMessage.EventId);
        Assert.Equal(message.ServiceName, postedMessage.ServiceName);
        Assert.Equal(message.Direction, postedMessage.Direction);
        Assert.Equal(message.Payload, postedMessage.Payload);
    }

    [Fact]
    public async Task ReportMessageAsync_NonSuccessResponse_IsAbsorbed()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway));
        using var reporter = CreateReporter(handler);

        await reporter.ReportMessageAsync(ValidMessage());

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ReportMessageAsync_HttpFailure_IsAbsorbed()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            throw new HttpRequestException("dashboard unavailable"));
        using var reporter = CreateReporter(handler);

        await reporter.ReportMessageAsync(ValidMessage());

        Assert.NotNull(handler.Request);
    }

    [Fact]
    public async Task ReportMessageAsync_NullMessage_Throws()
    {
        using var reporter = CreateReporter(new RecordingHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => reporter.ReportMessageAsync(null!));
    }

    [Fact]
    public void Dispose_DisposesOwnedHttpClient()
    {
        var handler = new RecordingHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK));
        var reporter = CreateReporter(handler);

        reporter.Dispose();

        Assert.True(handler.IsDisposed);
    }

    private static HttpDashboardReporter CreateReporter(HttpMessageHandler handler) =>
        CreateReporter(handler, Mock.Of<ILogger<HttpDashboardReporter>>());

    private static HttpDashboardReporter CreateReporter(
        HttpMessageHandler handler,
        ILogger<HttpDashboardReporter> logger) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://dashboard.test") }, logger);

    private static ServiceHeartbeat ValidHeartbeat() =>
        new()
        {
            ServiceName = "producer",
            State = ServiceState.Running,
            SentAt = new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero),
            MessagesHandled = 3
        };

    private static DashboardMessage ValidMessage() =>
        new()
        {
            MessageId = "message-1",
            EventId = "event-1",
            ServiceName = "producer",
            Direction = "sent",
            Timestamp = new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero),
            Payload = "{}"
        };

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public int RequestCount { get; private set; }
        public bool IsDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class CancellationHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException("cancelled", null, cancellationToken);
    }
}
