using Microsoft.Extensions.Logging;
using ServiceBusPoc.Carwash.Api;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for the Carwash API consumer module lifecycle.
/// </summary>
public sealed class CarwashApiModuleTests
{
    [Fact]
    public async Task StartAndStopAsync_WithCancelledServerToken_Completes()
    {
        var server = new CarwashApiServer(Mock.Of<ILogger<CarwashApiServer>>(), GetUnusedPort());
        var module = new CarwashApiModule(server, Mock.Of<ILogger<CarwashApiModule>>());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await module.StartAsync(cancellationTokenSource.Token);
        await module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwice_Throws()
    {
        var server = new CarwashApiServer(Mock.Of<ILogger<CarwashApiServer>>(), GetUnusedPort());
        var module = new CarwashApiModule(server, Mock.Of<ILogger<CarwashApiModule>>());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await module.StartAsync(cancellationTokenSource.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            module.StartAsync(cancellationTokenSource.Token));

        await module.StopAsync(CancellationToken.None);
    }

    private static int GetUnusedPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback,
            port: 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
