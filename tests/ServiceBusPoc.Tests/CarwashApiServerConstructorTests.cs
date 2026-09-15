using Microsoft.Extensions.Logging;
using ServiceBusPoc.Carwash.Api;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="CarwashApiServer"/> construction.
/// </summary>
public class CarwashApiServerConstructorTests
{
    [Fact]
    public void Constructor_WithDefaultPort_InitializesSuccessfully()
    {
        var mockLogger = new Mock<ILogger<CarwashApiServer>>();

        var server = new CarwashApiServer(mockLogger.Object);

        Assert.NotNull(server);
    }

    [Theory]
    [InlineData(6000)]
    [InlineData(8080)]
    [InlineData(9000)]
    public void Constructor_WithCustomPort_InitializesSuccessfully(int port)
    {
        var mockLogger = new Mock<ILogger<CarwashApiServer>>();

        var server = new CarwashApiServer(mockLogger.Object, port);

        Assert.NotNull(server);
    }
}
