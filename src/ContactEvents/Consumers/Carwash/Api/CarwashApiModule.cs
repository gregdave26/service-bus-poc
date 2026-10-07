using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.ConsumerModules;

namespace ServiceBusPoc.ContactEvents.Consumers.Carwash.Api;

/// <summary>
/// Hosts the optional Carwash verification API alongside the Carwash consumer.
/// </summary>
public sealed class CarwashApiModule : IConsumerModule
{
    private readonly CarwashApiServer _server;
    private readonly ILogger<CarwashApiModule> _logger;
    private Task? _serverTask;

    /// <summary>
    /// Initializes the Carwash verification API module.
    /// </summary>
    public CarwashApiModule(
        CarwashApiServer server,
        ILogger<CarwashApiModule> logger)
    {
        _server = server;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_serverTask is not null)
        {
            throw new InvalidOperationException("The Carwash API module has already been started.");
        }

        _serverTask = _server.StartAsync(cancellationToken);
        _logger.LogInformation("Carwash verification API module started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_serverTask is null)
        {
            return;
        }

        _server.Stop();
        await _serverTask.WaitAsync(cancellationToken);
        _logger.LogInformation("Carwash verification API module stopped");
    }
}
