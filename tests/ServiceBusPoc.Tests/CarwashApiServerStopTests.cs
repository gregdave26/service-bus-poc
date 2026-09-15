using Microsoft.Extensions.Logging;
using ServiceBusPoc.Carwash.Api;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="CarwashApiServer.Stop"/>.
/// </summary>
public class CarwashApiServerStopTests
{
    [Fact]
    public void Stop_WhenListenerHasNotStarted_CompletesWithoutException()
    {
        var mockLogger = new Mock<ILogger<CarwashApiServer>>();
        var server = new CarwashApiServer(mockLogger.Object);

        server.Stop();
    }

    [Fact]
    public void Stop_WhenCalledMultipleTimes_CompletesWithoutException()
    {
        var mockLogger = new Mock<ILogger<CarwashApiServer>>();
        var server = new CarwashApiServer(mockLogger.Object);

        server.Stop();
        server.Stop();
        server.Stop();
    }
}
