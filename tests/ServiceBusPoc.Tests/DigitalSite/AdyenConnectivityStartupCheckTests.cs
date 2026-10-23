using Adyen.Checkout.Models;
using Adyen.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class AdyenConnectivityStartupCheckTests
{
    private readonly Mock<IPaymentsService> _payments = new();

    [Fact]
    public async Task StartAsync_WhenConnectivitySucceeds_RetainsAdyenComponentMode()
    {
        SetupConnectivitySuccess();
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.AdyenComponent);
        var check = CreateStartupCheck(state, CreateAdapter());

        await check.StartAsync(CancellationToken.None);

        Assert.Equal(PaymentGatewayMode.AdyenComponent, state.EffectiveMode);
    }

    [Theory]
    [InlineData(PaymentGatewayMode.Adyen)]
    [InlineData(PaymentGatewayMode.AdyenComponent)]
    public async Task StartAsync_WhenDevelopmentFallbackEnabled_ReportsSimulatedPayments(PaymentGatewayMode mode)
    {
        SetupConnectivityFailure(new HttpRequestException("sensitive upstream response"));
        var state = new PaymentGatewayRuntimeState(mode);
        var logger = new Mock<ILogger<AdyenConnectivityStartupCheck>>();
        var check = CreateStartupCheck(state, CreateAdapter(), allowFallback: true, logger: logger.Object);

        await check.StartAsync(CancellationToken.None);

        Assert.Equal(PaymentGatewayMode.Stub, state.EffectiveMode);
        Assert.Equal(mode, state.ConfiguredMode);
        Assert.True(state.FallbackActive);
        Assert.Equal("AdyenStartupConnectivityFailed", state.FallbackReason);
        logger.Verify(log => log.Log(
            LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("payments are simulated")),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.Verify(log => log.Log(
            LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("Restart")),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task StartAsync_WithoutOptIn_PropagatesConnectivityFailure(string environment)
    {
        var failure = new HttpRequestException("unreachable");
        SetupConnectivityFailure(failure);
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Adyen);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateStartupCheck(state, CreateAdapter(), environment).StartAsync(CancellationToken.None));

        Assert.Same(failure, exception);
        Assert.Equal(PaymentGatewayMode.Adyen, state.EffectiveMode);
        Assert.False(state.FallbackActive);
    }

    [Theory]
    [InlineData("Test", PaymentGatewayMode.Adyen)]
    [InlineData("Staging", PaymentGatewayMode.AdyenComponent)]
    [InlineData("Production", PaymentGatewayMode.Stub)]
    public async Task StartAsync_WithOptInOutsideDevelopment_RejectsConfiguration(string environment, PaymentGatewayMode mode)
    {
        var state = new PaymentGatewayRuntimeState(mode);
        await Assert.ThrowsAsync<OptionsValidationException>(() =>
            CreateStartupCheck(state, CreateAdapter(), environment, true).StartAsync(CancellationToken.None));
        _payments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartAsync_WhenStubSelected_DoesNotCheckConnectivity()
    {
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Stub);
        var check = CreateStartupCheck(state, CreateAdapter());
        await check.StartAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);
        Assert.False(state.FallbackActive);
        Assert.Null(state.FallbackReason);
        _payments.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task StartAsync_WhenGatewayStatusIsTransient_AllowsDevelopmentFallback(int httpStatusCode)
    {
        SetupConnectivityFailure(new PaymentGatewayException("unavailable", httpStatusCode));
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Adyen);
        await CreateStartupCheck(state, CreateAdapter(), allowFallback: true).StartAsync(CancellationToken.None);
        Assert.True(state.FallbackActive);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    public async Task StartAsync_WhenGatewayRejectsConfigurationOrCredentials_PropagatesEvenWithOptIn(int httpStatusCode)
    {
        SetupConnectivityFailure(new PaymentGatewayException("rejected", httpStatusCode));
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Adyen);

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            CreateStartupCheck(state, CreateAdapter(), allowFallback: true).StartAsync(CancellationToken.None));

        Assert.Equal(PaymentGatewayMode.Adyen, state.EffectiveMode);
        Assert.False(state.FallbackActive);
    }

    [Fact]
    public async Task StartAsync_WhenGatewayFailureHasNoStatus_PropagatesEvenWithOptIn()
    {
        SetupConnectivityFailure(new PaymentGatewayException("incomplete session"));
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Adyen);

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            CreateStartupCheck(state, CreateAdapter(), allowFallback: true).StartAsync(CancellationToken.None));

        Assert.False(state.FallbackActive);
    }

    [Fact]
    public async Task StartAsync_WhenUnexpectedFailure_DoesNotFallback()
    {
        SetupConnectivityFailure(new InvalidOperationException("bug"));
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Adyen);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateStartupCheck(state, CreateAdapter(), allowFallback: true).StartAsync(CancellationToken.None));
        Assert.False(state.FallbackActive);
    }

    [Fact]
    public async Task StartAsync_WhenCancelled_DoesNotFallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        SetupConnectivityFailure(new OperationCanceledException(cancellation.Token));
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Adyen);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateStartupCheck(state, CreateAdapter(), allowFallback: true).StartAsync(cancellation.Token));
        Assert.False(state.FallbackActive);
    }

    [Fact]
    public async Task StartAsync_WhenHttpTimeout_AllowsDevelopmentFallback()
    {
        SetupConnectivityFailure(new TaskCanceledException("timeout"));
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Adyen);
        await CreateStartupCheck(state, CreateAdapter(), allowFallback: true).StartAsync(CancellationToken.None);
        Assert.True(state.FallbackActive);
    }

    [Fact]
    public async Task RuntimeGateway_WhenPaymentRequestFails_DoesNotFallback()
    {
        SetupConnectivityFailure(new HttpRequestException("payment failure"));
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.Adyen);
        var gateway = new RuntimePaymentGatewayAdapter(state, CreateAdapter(), new StubPaymentGateway(TimeProvider.System));
        var request = new CheckoutSessionRequest(
            "cart", "payment", "reference", Money.FromCents("AUD", 100),
            "http://localhost:5100", "correlation", null, null);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            gateway.CreateCheckoutSessionAsync(request, CancellationToken.None));

        Assert.Equal(PaymentGatewayMode.Adyen, gateway.Mode);
        Assert.False(state.FallbackActive);
    }

    private void SetupConnectivityFailure(Exception exception) =>
        _payments.Setup(payment => payment.SessionsAsync(
            It.IsAny<CreateCheckoutSessionRequest>(),
            It.IsAny<Adyen.Core.Client.RequestOptions>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(exception);

    private AdyenConnectivityStartupCheck CreateStartupCheck(
        PaymentGatewayRuntimeState state,
        AdyenPaymentAdapter adapter,
        string environment = "Development",
        bool allowFallback = false,
        ILogger<AdyenConnectivityStartupCheck>? logger = null)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(AdyenPaymentAdapter)))
            .Returns(adapter);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(value => value.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(scope.Object);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(host => host.EnvironmentName).Returns(environment);
        return new AdyenConnectivityStartupCheck(
            scopeFactory.Object,
            state,
            Options.Create(new DigitalSiteSettings { AllowDevelopmentAdyenFallback = allowFallback }),
            hostEnvironment.Object,
            logger ?? NullLogger<AdyenConnectivityStartupCheck>.Instance);
    }

    private AdyenPaymentAdapter CreateAdapter() =>
        new(
            _payments.Object,
            Options.Create(new AdyenSettings { MerchantAccount = "merchant", ClientKey = "client", HmacKey = "hmac" }),
            Options.Create(new DigitalSiteSettings()),
            Options.Create(new CommerceToolsSettings { ProjectKey = "project" }),
            NullLogger<AdyenPaymentAdapter>.Instance);

    private void SetupConnectivitySuccess()
    {
        var response = new Mock<ISessionsApiResponse>();
        response.SetupGet(value => value.IsCreated).Returns(true);
        response
            .Setup(value => value.Created())
            .Returns(new CreateCheckoutSessionResponse { Id = "startup", SessionData = "data" });
        _payments
            .Setup(payment => payment.SessionsAsync(
                It.IsAny<CreateCheckoutSessionRequest>(),
                It.IsAny<Adyen.Core.Client.RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);
    }
}
