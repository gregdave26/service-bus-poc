using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Carwash.Api;
using ServiceBusPoc.Carwash.Api.Contracts;

namespace ServiceBusPoc.Tests;

[CollectionDefinition("Carwash API", DisableParallelization = true)]
public sealed class CarwashApiCollection
{
}

[Collection("Carwash API")]
public class CarwashApiServerHttpTests
{
    [Theory]
    [InlineData("GET", "/carwash/v1/verify")]
    [InlineData("POST", "/not-found")]
    public async Task UnsupportedMethodOrPath_ReturnsNotFound(string method, string path)
    {
        await using var fixture = await ServerFixture.StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), fixture.BaseAddress + path);

        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EmptyBody_ReturnsBadRequest()
    {
        await using var fixture = await ServerFixture.StartAsync();

        using var response = await fixture.Client.PostAsync(fixture.VerifyUri, new StringContent(""));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("must not be empty", body);
    }

    [Fact]
    public async Task InvalidJson_ReturnsBadRequest()
    {
        await using var fixture = await ServerFixture.StartAsync();

        using var response = await fixture.Client.PostAsync(fixture.VerifyUri, new StringContent("{invalid}"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid JSON format", body);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"RacId":""}""")]
    [InlineData("""{"RacId":"   "}""")]
    public async Task MissingOrBlankRacId_ReturnsBadRequest(string payload)
    {
        await using var fixture = await ServerFixture.StartAsync();

        using var response = await fixture.Client.PostAsync(
            fixture.VerifyUri, new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("VALID-123", true)]
    [InlineData("123-INVALID", false)]
    public async Task MemberResponse_ReflectsMockValidation(string racId, bool expectedValid)
    {
        await using var fixture = await ServerFixture.StartAsync();

        using var response = await fixture.Client.PostAsJsonAsync(
            fixture.VerifyUri, new { RacId = racId });
        var payload = await response.Content.ReadFromJsonAsync<VerifyMemberResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(expectedValid, payload.ValidMember);
    }

    private sealed class ServerFixture : IAsyncDisposable
    {
        private readonly CarwashApiServer _server;
        private readonly Task _serverTask;

        private ServerFixture(CarwashApiServer server, Task serverTask, int port)
        {
            _server = server;
            _serverTask = serverTask;
            BaseAddress = $"http://localhost:{port}";
            VerifyUri = $"{BaseAddress}/carwash/v1/verify";
            Client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        }

        public HttpClient Client { get; }
        public string BaseAddress { get; }
        public string VerifyUri { get; }

        public static async Task<ServerFixture> StartAsync()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var server = new CarwashApiServer(new Mock<ILogger<CarwashApiServer>>().Object, port);
            var task = server.StartAsync();
            var fixture = new ServerFixture(server, task, port);
            await fixture.WaitForStartedAsync();
            return fixture;
        }

        private async Task WaitForStartedAsync()
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, BaseAddress);
                    using var response = await Client.SendAsync(request);
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
            _server.Stop();
            Client.Dispose();
            await _serverTask;
        }
    }
}
