using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Carwash.Services;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalChannels.Services;
using ServiceBusPoc.Insurance.Services;
using ServiceBusPoc.ParksResorts.Services;

namespace ServiceBusPoc.Tests;

public class ConsumerServiceTests
{
    [Fact]
    public async Task Carwash_RunAsync_DelegatesConfiguredDescriptorAndCancellation()
    {
        var (runner, runnerMock) = CreateRunner();
        var logger = new Mock<ILogger<CarwashConsumerService>>();
        var settings = Options.Create(new CarwashSettings { ApiUrl = "http://localhost:5000", MockMode = true });
        using var cancellation = new CancellationTokenSource();
        var run = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), cancellation.Token)).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(It.IsAny<ConsumerDescriptor>(), cancellation.Token))
            .Callback(() => run.SetResult())
            .Returns(Task.CompletedTask);

        await new CarwashConsumerService(runner, logger.Object, settings).RunAsync(cancellation.Token);

        await run.Task.WaitAsync(TimeSpan.FromSeconds(1));
        runnerMock.Verify(r => r.RunAsync(
            It.Is<ConsumerDescriptor>(d => d.ServiceName == "carwash" && d.FilterDescription == "hasCarwashProduct = true"),
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task Carwash_Constructor_UsesSettingsAndWaitsForReady()
    {
        var (runner, runnerMock) = CreateRunner();
        var logger = new Mock<ILogger<CarwashConsumerService>>();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CarwashConsumerService(runner, logger.Object, Options.Create(new CarwashSettings
            {
                ApiUrl = "http://localhost:5000",
                MockMode = false
            })).RunAsync());

        runnerMock.Verify(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DigitalChannels_RunAsync_UsesNoFilterDescriptor()
    {
        var (runner, runnerMock) = CreateRunner();
        var logger = new Mock<ILogger<DigitalChannelsConsumerService>>();
        using var cancellation = new CancellationTokenSource();
        ConsumerDescriptor? descriptor = null;
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), cancellation.Token)).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(It.IsAny<ConsumerDescriptor>(), cancellation.Token))
            .Callback<ConsumerDescriptor, CancellationToken>((d, _) => descriptor = d)
            .Returns(Task.CompletedTask);

        await new DigitalChannelsConsumerService(runner, logger.Object).RunAsync(cancellation.Token);

        Assert.NotNull(descriptor);
        Assert.Equal("digital-channels", descriptor.ServiceName);
        Assert.Equal(ConsumerDescriptor.NoFilter, descriptor.FilterDescription);
    }

    [Fact]
    public async Task Insurance_RunAsync_UsesInsuranceDescriptor()
    {
        var (runner, runnerMock) = CreateRunner();
        var logger = new Mock<ILogger<InsuranceConsumerService>>();
        using var cancellation = new CancellationTokenSource();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), cancellation.Token)).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(
            It.Is<ConsumerDescriptor>(d => d.ServiceName == "insurance" && d.FilterDescription == "hasInsurance = true"),
            cancellation.Token)).Returns(Task.CompletedTask);

        await new InsuranceConsumerService(runner, logger.Object).RunAsync(cancellation.Token);

        runnerMock.Verify(r => r.RunAsync(
            It.Is<ConsumerDescriptor>(d => d.ServiceName == "insurance" && d.FilterDescription == "hasInsurance = true"),
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task DigitalChannels_WhenNotReady_Throws()
    {
        var (runner, runnerMock) = CreateRunner();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DigitalChannelsConsumerService(runner, Mock.Of<ILogger<DigitalChannelsConsumerService>>()).RunAsync());
    }

    [Fact]
    public async Task Insurance_WhenRunnerCancels_IsHandled()
    {
        var (runner, runnerMock) = CreateRunner();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(It.IsAny<ConsumerDescriptor>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await new InsuranceConsumerService(runner, Mock.Of<ILogger<InsuranceConsumerService>>()).RunAsync();
    }

    [Fact]
    public async Task Insurance_WhenRunnerFails_RethrowsOriginalException()
    {
        var (runner, runnerMock) = CreateRunner();
        var expected = new InvalidOperationException("receiver failed");
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(It.IsAny<ConsumerDescriptor>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new InsuranceConsumerService(runner, Mock.Of<ILogger<InsuranceConsumerService>>()).RunAsync());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ParksResorts_WhenNotReady_Throws()
    {
        var (runner, runnerMock) = CreateRunner();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ParksResortsConsumerService(runner, Mock.Of<ILogger<ParksResortsConsumerService>>()).RunAsync());
    }

    [Fact]
    public async Task ParksResorts_RunAsync_UsesParksResortsDescriptor()
    {
        var (runner, runnerMock) = CreateRunner();
        var logger = new Mock<ILogger<ParksResortsConsumerService>>();
        using var cancellation = new CancellationTokenSource();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), cancellation.Token)).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(
            It.Is<ConsumerDescriptor>(d => d.ServiceName == "parks-resorts" && d.FilterDescription == "hasParksResorts = true"),
            cancellation.Token)).Returns(Task.CompletedTask);

        await new ParksResortsConsumerService(runner, logger.Object).RunAsync(cancellation.Token);

        runnerMock.Verify(r => r.RunAsync(
            It.Is<ConsumerDescriptor>(d => d.ServiceName == "parks-resorts" && d.FilterDescription == "hasParksResorts = true"),
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task ConsumerServices_CancelledRunner_IsHandled()
    {
        var (runner, runnerMock) = CreateRunner();
        var logger = new Mock<ILogger<InsuranceConsumerService>>();
        using var cancellation = new CancellationTokenSource();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), cancellation.Token)).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(It.IsAny<ConsumerDescriptor>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await new InsuranceConsumerService(runner, logger.Object).RunAsync(cancellation.Token);
    }

    [Fact]
    public async Task Carwash_CancelledRunner_IsHandled()
    {
        var (runner, runnerMock) = CreateRunner();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(It.IsAny<ConsumerDescriptor>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await new CarwashConsumerService(
            runner,
            Mock.Of<ILogger<CarwashConsumerService>>(),
            Options.Create(new CarwashSettings { ApiUrl = "http://localhost:5000", MockMode = true }))
            .RunAsync();
    }

    [Fact]
    public async Task DigitalChannels_CancelledRunner_IsHandled()
    {
        var (runner, runnerMock) = CreateRunner();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(It.IsAny<ConsumerDescriptor>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await new DigitalChannelsConsumerService(
            runner,
            Mock.Of<ILogger<DigitalChannelsConsumerService>>())
            .RunAsync();
    }

    [Fact]
    public async Task ParksResorts_CancelledRunner_IsHandled()
    {
        var (runner, runnerMock) = CreateRunner();
        runnerMock.Setup(r => r.WaitForReadyAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runnerMock.Setup(r => r.RunAsync(It.IsAny<ConsumerDescriptor>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await new ParksResortsConsumerService(
            runner,
            Mock.Of<ILogger<ParksResortsConsumerService>>())
            .RunAsync();
    }

    private static (SubscriptionConsumerRunner Runner, Mock<SubscriptionConsumerRunner> Mock) CreateRunner()
    {
        var mock = new Mock<SubscriptionConsumerRunner>(
            MockBehavior.Strict,
            Mock.Of<IServiceBusReceiver>(),
            Mock.Of<IDashboardReporter>(),
            Options.Create(new ServiceBusSettings()),
            Options.Create(new DashboardSettings()),
            Mock.Of<ILogger<SubscriptionConsumerRunner>>(),
            TimeProvider.System);
        return (mock.Object, mock);
    }
}
