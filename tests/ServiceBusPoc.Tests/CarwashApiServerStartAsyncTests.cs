using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using ServiceBusPoc.ContactEvents.Consumers.Carwash.Api;
using ServiceBusPoc.ContactEvents.Consumers.Carwash.Services;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for the cancelled startup path of <see cref="CarwashApiServer.StartAsync"/>.
/// </summary>
public class CarwashApiServerStartAsyncTests
{
    [Fact]
    public async Task StartAsync_WithCancelledToken_CompletesWithoutException()
    {
        var mockLogger = new Mock<ILogger<CarwashApiServer>>();
        var server = new CarwashApiServer(mockLogger.Object, Mock.Of<IMembershipVerifier>(), GetUnusedPort());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await server.StartAsync(cancellationTokenSource.Token);

        Assert.True(cancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public async Task StartAsync_WithCancelledToken_LogsStartAndStop()
    {
        var mockLogger = new Mock<ILogger<CarwashApiServer>>();
        var server = new CarwashApiServer(mockLogger.Object, Mock.Of<IMembershipVerifier>(), GetUnusedPort());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await server.StartAsync(cancellationTokenSource.Token);

        mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("started", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("stopped", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static int GetUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
