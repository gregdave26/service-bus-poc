using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.Verifier.Services;

namespace ServiceBusPoc.Tests;

public sealed class VerifierServiceTests
{
    [Fact]
    public async Task RunAsync_WhenTopologyIsValid_Completes()
    {
        var validator = new Mock<ITopologyValidator>();
        validator.Setup(x => x.ValidateAsync()).ReturnsAsync(true);
        var service = CreateService(validator.Object);

        await service.RunAsync();

        validator.Verify(x => x.ValidateAsync(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenTopologyIsInvalid_ReturnsWithoutReadiness()
    {
        var validator = new Mock<ITopologyValidator>();
        validator.Setup(x => x.ValidateAsync()).ReturnsAsync(false);
        var service = CreateService(validator.Object);

        await service.RunAsync();

        validator.Verify(x => x.ValidateAsync(), Times.Once);
    }

    private static VerifierService CreateService(ITopologyValidator validator) =>
        new(
            Mock.Of<ILogger<VerifierService>>(),
            Options.Create(new ServiceBusSettings { Namespace = "test-namespace" }),
            validator);
}
