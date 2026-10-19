using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.ContactEvents.Verifier.Services;

namespace ServiceBusPoc.Tests;

public sealed class VerifierServiceTests
{
    [Fact]
    public async Task RunAsync_WhenTopologyIsValid_Completes()
    {
        var validator = new Mock<ITopologyValidator>();
        validator.Setup(x => x.ValidateAsync()).ReturnsAsync(true);
        var routingVerifier = new Mock<IDigitalChannelsRoutingVerifier>();
        var filteredVerifier = new Mock<IFilteredChannelsRoutingVerifier>();
        var service = CreateService(validator.Object, routingVerifier.Object, filteredVerifier.Object);

        await service.RunAsync();

        validator.Verify(x => x.ValidateAsync(), Times.Once);
        routingVerifier.Verify(x => x.VerifyAsync(), Times.Once);
        filteredVerifier.Verify(x => x.VerifyAsync(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenTopologyIsInvalid_Throws()
    {
        var validator = new Mock<ITopologyValidator>();
        validator.Setup(x => x.ValidateAsync()).ReturnsAsync(false);
        var routingVerifier = new Mock<IDigitalChannelsRoutingVerifier>();
        var filteredVerifier = new Mock<IFilteredChannelsRoutingVerifier>();
        var service = CreateService(validator.Object, routingVerifier.Object, filteredVerifier.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync());

        validator.Verify(x => x.ValidateAsync(), Times.Once);
    }

    private static VerifierService CreateService(
        ITopologyValidator validator,
        IDigitalChannelsRoutingVerifier routingVerifier,
        IFilteredChannelsRoutingVerifier filteredVerifier) =>
        new(
            Mock.Of<ILogger<VerifierService>>(),
            Options.Create(new ServiceBusSettings { Namespace = "test-namespace" }),
            validator,
            routingVerifier,
            filteredVerifier);
}
