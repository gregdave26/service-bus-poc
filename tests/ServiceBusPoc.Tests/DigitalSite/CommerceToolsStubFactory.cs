using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Messaging;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;

namespace ServiceBusPoc.Tests.DigitalSite;

/// <summary>Hosts the commercetools stub in memory with a temporary database and a recording publisher.</summary>
public sealed class CommerceToolsStubFactory : WebApplicationFactory<CatalogService>
{
    public const string ProjectKey = "rac-rsa-poc";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"ct-stub-{Guid.NewGuid():N}.db");
    private readonly string? _clientId;
    private readonly string? _clientSecret;

    public CommerceToolsStubFactory(string? clientId = null, string? clientSecret = null)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public RecordingPublisher Publisher { get; } = new();

    public CommerceToolsClient CreateCommerceClient()
    {
        var httpClient = CreateClient();
        httpClient.BaseAddress = new Uri($"http://localhost/{ProjectKey}/");
        return new CommerceToolsClient(httpClient);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("CommerceToolsStub:DatabasePath", _databasePath);
        builder.UseSetting("CommerceToolsStub:ProjectKey", ProjectKey);
        builder.UseSetting("CommerceToolsStub:ClientId", _clientId);
        builder.UseSetting("CommerceToolsStub:ClientSecret", _clientSecret);
        builder.UseSetting("ServiceBus:ConnectionString", "Endpoint=sb://localhost;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;");
        builder.UseSetting("ServiceBus:Namespace", "test");
        builder.UseSetting("ServiceBus:TopicName", "commerce.events");
        builder.UseSetting("Dashboard:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ICommerceMessagePublisher>(Publisher);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath))
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }
    }

    public sealed class RecordingPublisher : ICommerceMessagePublisher
    {
        private readonly ConcurrentQueue<OutboxMessage> _messages = new();

        public int FailuresRemaining { get; set; }

        public IReadOnlyList<OutboxMessage> Messages => _messages.ToArray();

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new Azure.Messaging.ServiceBus.ServiceBusException("unavailable", Azure.Messaging.ServiceBus.ServiceBusFailureReason.ServiceCommunicationProblem);
            }

            _messages.Enqueue(message);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<OutboxMessage>> WaitForAsync(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (_messages.Count < count && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            return Messages;
        }
    }
}
