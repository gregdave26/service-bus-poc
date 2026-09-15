using Microsoft.Extensions.Logging;
using ServiceBusPoc.Carwash.Api;

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
        var server = new CarwashApiServer(mockLogger.Object);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await server.StartAsync(cancellationTokenSource.Token);

        Assert.True(cancellationTokenSource.IsCancellationRequested);
    }

    [Fact]
    public async Task StartAsync_WithCancelledToken_LogsStartAndStop()
    {
        var mockLogger = new Mock<ILogger<CarwashApiServer>>();
        var server = new CarwashApiServer(mockLogger.Object);
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
}
