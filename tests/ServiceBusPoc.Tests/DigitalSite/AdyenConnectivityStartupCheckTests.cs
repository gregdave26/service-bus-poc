using Adyen.Checkout.Models;
using Adyen.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task StartAsync_WhenConnectivityFails_SwitchesToStubMode()
    {
        var response = new Mock<ISessionsApiResponse>();
        response.SetupGet(r => r.IsCreated).Returns(false);
        _payments
            .Setup(p => p.SessionsAsync(
                It.IsAny<CreateCheckoutSessionRequest>(),
                It.IsAny<Adyen.Core.Client.RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);
        var state = new PaymentGatewayRuntimeState(PaymentGatewayMode.AdyenComponent);
        var check = CreateStartupCheck(state, CreateAdapter());

        await check.StartAsync(CancellationToken.None);

        Assert.Equal(PaymentGatewayMode.Stub, state.EffectiveMode);
    }

    private AdyenConnectivityStartupCheck CreateStartupCheck(
        PaymentGatewayRuntimeState state,
        AdyenPaymentAdapter adapter)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(AdyenPaymentAdapter)))
            .Returns(adapter);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(value => value.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(scope.Object);

        return new AdyenConnectivityStartupCheck(
            scopeFactory.Object,
            state,
            NullLogger<AdyenConnectivityStartupCheck>.Instance);
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
