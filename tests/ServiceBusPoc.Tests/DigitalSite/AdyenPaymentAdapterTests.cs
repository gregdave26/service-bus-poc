using System.Net;
using Adyen.Checkout.Models;
using Adyen.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class AdyenPaymentAdapterTests
{
    private const string HmacKey = "44782DEF547AAA06C910C43932B1EB0C71FC68D9D0C057550C48EC2ACF6BA056";

    private readonly Mock<IPaymentsService> _payments = new();

    [Fact]
    public async Task CreateCheckoutSession_SendsCartDetailsAndReturnsSession()
    {
        CreateCheckoutSessionRequest? sent = null;
        var response = new Mock<ISessionsApiResponse>();
        response.SetupGet(r => r.IsCreated).Returns(true);
        response.Setup(r => r.Created()).Returns(new CreateCheckoutSessionResponse { Id = "CS1", SessionData = "data" });
        _payments
            .Setup(p => p.SessionsAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<Adyen.Core.Client.RequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<CreateCheckoutSessionRequest, Adyen.Core.Client.RequestOptions, CancellationToken>((request, _, _) => sent = request)
            .ReturnsAsync(response.Object);

        var session = await CreateAdapter().CreateCheckoutSessionAsync(SessionRequest(), CancellationToken.None);

        Assert.Equal(new CheckoutSession("CS1", "data", "test_CLIENT", "test", PaymentGatewayMode.Adyen), session);
        Assert.Equal("RSA-1", sent!.Reference);
        Assert.Equal("RACWAAccountECOM", sent.MerchantAccount);
        Assert.Equal(31000, sent.Amount.Value);
        Assert.Equal("AU", sent.CountryCode);
        Assert.Equal("c1", sent.Metadata["cartId"]);
        Assert.Equal("rac-rsa-poc", sent.Metadata["ctProjectKey"]);
        Assert.Equal("CLAS", sent.Metadata["sku"]);
    }

    [Fact]
    public async Task CreateCheckoutSession_ThrowsWhenAdyenRejectsTheRequest()
    {
        var response = new Mock<ISessionsApiResponse>();
        response.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.Unauthorized);
        _payments
            .Setup(p => p.SessionsAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<Adyen.Core.Client.RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);

        await Assert.ThrowsAsync<PaymentGatewayException>(() => CreateAdapter().CreateCheckoutSessionAsync(SessionRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task HandlePaymentDetails_ReturnsResultCodeAndActionFlag()
    {
        PaymentDetailsRequest? sent = null;
        var response = new Mock<IPaymentsDetailsApiResponse>();
        response.SetupGet(r => r.IsOk).Returns(true);
        response.Setup(r => r.Ok()).Returns(new PaymentDetailsResponse { ResultCode = PaymentDetailsResponse.ResultCodeEnum.Authorised, PspReference = "PSP1" });
        _payments
            .Setup(p => p.PaymentsDetailsAsync(It.IsAny<PaymentDetailsRequest>(), It.IsAny<Adyen.Core.Client.RequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentDetailsRequest, Adyen.Core.Client.RequestOptions, CancellationToken>((request, _, _) => sent = request)
            .ReturnsAsync(response.Object);

        var result = await CreateAdapter().HandlePaymentDetailsAsync(new PaymentDetailsSubmission("p1", "corr", "redirect", null), CancellationToken.None);

        Assert.Equal(new PaymentActionResult("Authorised", "PSP1", false), result);
        Assert.Equal("redirect", sent!.Details.RedirectResult);
    }

    [Fact]
    public async Task HandlePaymentDetails_ThrowsWhenAdyenRejectsTheDetails()
    {
        var response = new Mock<IPaymentsDetailsApiResponse>();
        response.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.UnprocessableEntity);
        _payments
            .Setup(p => p.PaymentsDetailsAsync(It.IsAny<PaymentDetailsRequest>(), It.IsAny<Adyen.Core.Client.RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            CreateAdapter().HandlePaymentDetailsAsync(new PaymentDetailsSubmission("p1", "corr", "redirect", null), CancellationToken.None));
    }

    [Fact]
    public void Webhooks_AreVerifiedWithTheConfiguredHmacKey()
    {
        var adapter = CreateAdapter();
        var stub = new StubPaymentGateway(TimeProvider.System);
        var payload = stub.CreateSignedNotification(StubPaymentOutcome.Authorised, "RSA-1", Money.FromCents("AUD", 100));

        var verification = adapter.VerifyWebhook(payload);

        Assert.Empty(verification.Authentic);
        Assert.Equal(1, verification.RejectedCount);
    }

    [Fact]
    public async Task ApimHandler_ReplacesTheAdyenApiKeyWithTheSubscriptionKey()
    {
        HttpRequestMessage? forwarded = null;
        using var invoker = new HttpMessageInvoker(new ApimSubscriptionHandler("sub-key")
        {
            InnerHandler = new CapturingHandler(request => forwarded = request),
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://apim.example/adyen/checkout/v72/sessions");
        request.Headers.Add(ApimSubscriptionHandler.AdyenApiKeyHeader, "secret");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.False(forwarded!.Headers.Contains(ApimSubscriptionHandler.AdyenApiKeyHeader));
        Assert.Equal("sub-key", forwarded.Headers.GetValues(ApimSubscriptionHandler.SubscriptionKeyHeader).Single());
    }

    [Fact]
    public async Task AdyenMode_IsSelectedByConfiguration()
    {
        using var commerceTools = new CommerceToolsStubFactory();
        using var api = new CommerceApiFactory(
            commerceTools,
            new Dictionary<string, string?>
            {
                ["DigitalSite:PaymentGateway"] = "Adyen",
                ["Adyen:MerchantAccount"] = "RACWAAccountECOM",
                ["Adyen:ApiKey"] = "key",
                ["Adyen:ClientKey"] = "test_CLIENT",
                ["Adyen:HmacKey"] = HmacKey,
            },
            services => services.AddSingleton(_payments.Object));
        using var client = api.CreateClient();

        var config = await client.GetStringAsync("/api/digital-site/config");
        var stubRoute = await client.PostAsync("/api/digital-site/checkout/stub/p1/notify", new StringContent("{}"));

        Assert.Contains("Adyen", config, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, stubRoute.StatusCode);
    }

    [Fact]
    public void AdyenMode_RequiresCredentials()
    {
        using var commerceTools = new CommerceToolsStubFactory();
        using var api = new CommerceApiFactory(commerceTools, new Dictionary<string, string?> { ["DigitalSite:PaymentGateway"] = "Adyen" });

        Assert.Throws<OptionsValidationException>(() => api.CreateClient());
    }

    private AdyenPaymentAdapter CreateAdapter() =>
        new(
            _payments.Object,
            Options.Create(new AdyenSettings { MerchantAccount = "RACWAAccountECOM", ClientKey = "test_CLIENT", HmacKey = HmacKey }),
            Options.Create(new DigitalSiteSettings()),
            Options.Create(new CommerceToolsSettings { ProjectKey = "rac-rsa-poc" }),
            NullLogger<AdyenPaymentAdapter>.Instance);

    private static CheckoutSessionRequest SessionRequest() =>
        new("c1", "p1", "RSA-1", Money.FromCents("AUD", 31000), "http://localhost:5100/digital-site", "corr", "CRM-12345678", "CLAS");

    private sealed class CapturingHandler(Action<HttpRequestMessage> capture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            capture(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
