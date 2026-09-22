using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.Dashboard.Api;
using ServiceBusPoc.Dashboard.Server;
using ServiceBusPoc.Dashboard.Status;

namespace ServiceBusPoc.Tests;

[CollectionDefinition("Dashboard HTTP", DisableParallelization = true)]
public sealed class DashboardHttpCollection;

[Collection("Dashboard HTTP")]
public sealed class DashboardHttpServerTests
{
    [Fact]
    public async Task RootAndUnknownRoutes_ReturnExpectedResponses()
    {
        await using var fixture = await ServerFixture.StartAsync();

        using var root = await fixture.Client.GetAsync(fixture.BaseAddress + "/");
        var html = await root.Content.ReadAsStringAsync();
        using var unknown = await fixture.Client.GetAsync(fixture.BaseAddress + "/missing");

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Equal("text/html", root.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Service Bus Dashboard", html);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Not found", await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HeartbeatAndStatus_ReturnRecordedService()
    {
        await using var fixture = await ServerFixture.StartAsync();
        var heartbeat = new ServiceHeartbeat
        {
            ServiceName = "insurance",
            SubscriptionName = "insurance",
            State = ServiceState.Running,
            SentAt = DateTimeOffset.UtcNow,
            MessagesHandled = 3,
            LastEventId = "event-1"
        };

        using var heartbeatResponse = await fixture.Client.PostAsJsonAsync(
            fixture.BaseAddress + "/api/heartbeat", heartbeat);
        var statusJson = await fixture.Client.GetStringAsync(fixture.BaseAddress + "/api/status");
        using var document = JsonDocument.Parse(statusJson);
        var status = document.RootElement.EnumerateArray().Single();

        Assert.Equal(HttpStatusCode.OK, heartbeatResponse.StatusCode);
        Assert.Equal("OK", await heartbeatResponse.Content.ReadAsStringAsync());
        Assert.Equal("insurance", status.GetProperty("serviceName").GetString());
        Assert.Equal("running", status.GetProperty("state").GetString());
        Assert.Equal(3, status.GetProperty("messagesHandled").GetInt64());
        Assert.Equal("event-1", status.GetProperty("lastEventId").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{invalid}")]
    public async Task InvalidHeartbeat_ReturnsBadRequest(string body)
    {
        await using var fixture = await ServerFixture.StartAsync();

        using var response = await fixture.Client.PostAsync(
            fixture.BaseAddress + "/api/heartbeat",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid heartbeat", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Publish_ReturnsPublisherResultAndMapsRequest()
    {
        var sender = new Mock<IServiceBusSender>();
        sender.Setup(x => x.SendMessageAsync(It.IsAny<Azure.Messaging.ServiceBus.ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await using var fixture = await ServerFixture.StartAsync(CreateHandler(sender));

        using var response = await fixture.Client.PostAsJsonAsync(
            fixture.BaseAddress + "/api/publish",
            new
            {
                ContactId = "C1", FirstName = "Ada", LastName = "Lovelace",
                Phone = "555", Email = "ada@example.com", RacId = "R1",
                HasInsurance = true, HasParksResorts = false, HasCarwashProduct = true
            });
        var result = await response.Content.ReadFromJsonAsync<PublishEventResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.EventId));
        sender.Verify(x => x.SendMessageAsync(It.IsAny<Azure.Messaging.ServiceBus.ServiceBusMessage>(), CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{invalid}")]
    public async Task InvalidPublish_ReturnsBadRequest(string body)
    {
        await using var fixture = await ServerFixture.StartAsync();

        using var response = await fixture.Client.PostAsync(
            fixture.BaseAddress + "/api/publish",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Publish_WhenPublisherFails_ReturnsBadRequest()
    {
        var sender = new Mock<IServiceBusSender>();
        sender.Setup(x => x.SendMessageAsync(
                It.IsAny<Azure.Messaging.ServiceBus.ServiceBusMessage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        await using var fixture = await ServerFixture.StartAsync(CreateHandler(sender));

        using var response = await fixture.Client.PostAsJsonAsync(
            fixture.BaseAddress + "/api/publish",
            new
            {
                ContactId = "C1", FirstName = "Ada", LastName = "Lovelace",
                Phone = "555", Email = "ada@example.com", RacId = "R1"
            });

        var result = await response.Content.ReadFromJsonAsync<PublishEventResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("broker unavailable", result.Error);
    }

    [Fact]
    public async Task Status_WhenNoHeartbeats_ReturnsEmptyArray()
    {
        await using var fixture = await ServerFixture.StartAsync();

        using var response = await fixture.Client.GetAsync(fixture.BaseAddress + "/api/status");
        var statuses = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(statuses);
        Assert.Empty(statuses!);
    }

    private static PublishEventHandler CreateHandler(Mock<IServiceBusSender> sender) =>
        new(
            new ContactEventPublisher(sender.Object, Mock.Of<ILogger<ContactEventPublisher>>(), TimeProvider.System),
            Mock.Of<ILogger<PublishEventHandler>>());

    private sealed class ServerFixture : IAsyncDisposable
    {
        private readonly DashboardHttpServer _server;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serverTask;

        private ServerFixture(DashboardHttpServer server, int port)
        {
            _server = server;
            BaseAddress = $"http://localhost:{port}";
            Client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _serverTask = _server.StartAsync(_stop.Token);
        }

        public HttpClient Client { get; }
        public string BaseAddress { get; }

        public static async Task<ServerFixture> StartAsync(PublishEventHandler? handler = null)
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var server = new DashboardHttpServer(
                Options.Create(new DashboardHostSettings { Host = "localhost", Port = port }),
                new StatusRegistry(Mock.Of<ILogger<StatusRegistry>>()),
                handler ?? CreateHandler(new Mock<IServiceBusSender>()),
                Mock.Of<ILogger<DashboardHttpServer>>());
            var fixture = new ServerFixture(server, port);
            await fixture.WaitForStartedAsync();
            return fixture;
        }

        private async Task WaitForStartedAsync()
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    using var response = await Client.GetAsync(BaseAddress + "/");
                    return;
                }
                catch (HttpRequestException) when (attempt < 19)
                {
                    await Task.Delay(25);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            try
            {
                using var wake = await Client.GetAsync(BaseAddress + "/");
            }
            catch (HttpRequestException)
            {
            }

            _server.Dispose();
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
            _stop.Dispose();
            Client.Dispose();
        }
    }
}
