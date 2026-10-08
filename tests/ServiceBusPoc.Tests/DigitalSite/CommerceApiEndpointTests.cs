using System.Net;
using System.Net.Http.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalSite.CartProcessor;
using ServiceBusPoc.DigitalSite.CommerceApi.Carts;
using ServiceBusPoc.DigitalSite.CommerceApi.Catalog;
using ServiceBusPoc.DigitalSite.CommerceApi.Checkout;
using ServiceBusPoc.DigitalSite.FulfilmentStub;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.Tests.DigitalSite;

/// <summary>Drives the commerce API, Cart Processor and fulfilment stub against the commercetools stub.</summary>
public sealed class CommerceApiEndpointTests : IDisposable
{
    private const string CrmId = "CRM-12345678";
    private const string Api = "/api/digital-site";

    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    private readonly CommerceToolsStubFactory _commerceTools = new();
    private readonly CommerceApiFactory _api;
    private readonly HttpClient _client;
    private readonly List<ServiceBusMessage> _publishedHoldings = [];

    public CommerceApiEndpointTests()
    {
        _api = new CommerceApiFactory(_commerceTools);
        _client = _api.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
        _commerceTools.Dispose();
    }

    [Fact]
    public async Task Config_ReportsStubGateway()
    {
        var config = await _client.GetFromJsonAsync<JsonElement>($"{Api}/config");

        Assert.Equal("Stub", config.GetProperty("gateway").GetString());
    }

    [Fact]
    public async Task Catalog_UsesCommercetoolsPricesAndSiteContent()
    {
        var catalog = await _client.GetFromJsonAsync<CatalogResponse>($"{Api}/catalog", Json);

        Assert.Equal(4, catalog!.Covers.Count);
        var classic = Assert.Single(catalog.Covers, cover => cover.Id == "CLAS");
        Assert.Equal("Classic", classic.Name);
        Assert.Equal(310m, classic.AnnualPrice);
        Assert.NotEmpty(classic.Perks);
        Assert.Equal("Annual", Assert.Single(catalog.PaymentPlans).Id);
    }

    [Theory]
    [InlineData("1abc123", HttpStatusCode.OK)]
    [InlineData("ZZZ999", HttpStatusCode.NotFound)]
    [InlineData("not-a-rego!", HttpStatusCode.BadRequest)]
    public async Task Vehicles_LooksUpDemoRegister(string rego, HttpStatusCode expected)
    {
        var response = await _client.GetAsync($"{Api}/vehicles/{Uri.EscapeDataString(rego)}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Cart_CreateResumeUpdateAndDelete()
    {
        var cart = await CreateCartAsync("CLAS");
        Assert.Equal(310m, cart.TotalPrice);
        Assert.Equal("CLAS", cart.CoverId);
        Assert.Equal("1ABC123", cart.Vehicle!.Rego);

        var active = await _client.GetFromJsonAsync<List<CartView>>($"{Api}/members/{CrmId}/carts", Json);
        Assert.Equal(cart.Id, Assert.Single(active!).Id);

        var update = await _client.PutAsJsonAsync(
            $"{Api}/carts/{cart.Id}",
            new UpdateCartRequest(CrmId, cart.Version, new VehicleSelection("Skip", null), "ULTI"));
        var updated = await ReadAsync<CartView>(update, HttpStatusCode.OK);
        Assert.Equal("ULTI", updated.CoverId);
        Assert.Equal(410m, updated.TotalPrice);
        Assert.Null(updated.Vehicle);

        var stale = await _client.PutAsJsonAsync(
            $"{Api}/carts/{cart.Id}",
            new UpdateCartRequest(CrmId, cart.Version, new VehicleSelection("Skip", null), "STD"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var deleted = await _client.DeleteAsync($"{Api}/carts/{cart.Id}?crmId={CrmId}&version={updated.Version}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<List<CartView>>($"{Api}/members/{CrmId}/carts", Json))!);
    }

    [Fact]
    public async Task Cart_RejectsInvalidRequestsAndOtherMembers()
    {
        var invalidCrm = await _client.PostAsJsonAsync($"{Api}/carts", new CreateCartRequest("123", "No", null, "CLAS"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCrm.StatusCode);

        var unknownCover = await _client.PostAsJsonAsync($"{Api}/carts", new CreateCartRequest(CrmId, "No", null, "GOLD"));
        Assert.Equal(HttpStatusCode.BadRequest, unknownCover.StatusCode);

        var cart = await CreateCartAsync("CLAS");
        var otherMember = await _client.PutAsJsonAsync(
            $"{Api}/carts/{cart.Id}",
            new UpdateCartRequest("CRM-99999999", cart.Version, null, "STD"));
        Assert.Equal(HttpStatusCode.NotFound, otherMember.StatusCode);

        var missingHistory = await _client.GetAsync($"{Api}/members/bad/history");
        Assert.Equal(HttpStatusCode.BadRequest, missingHistory.StatusCode);
    }

    [Fact]
    public async Task Checkout_RejectsUnacceptedTermsAndForeignReturnUrls()
    {
        var cart = await CreateCartAsync("STD");

        var noTerms = await _client.PostAsJsonAsync(
            $"{Api}/checkout/session",
            new StartCheckoutRequest(cart.Id, CrmId, $"{CommerceApiFactory.AllowedOrigin}/digital-site", true, false));
        Assert.Equal(HttpStatusCode.BadRequest, noTerms.StatusCode);

        var foreignReturn = await _client.PostAsJsonAsync(
            $"{Api}/checkout/session",
            new StartCheckoutRequest(cart.Id, CrmId, "https://attacker.example/return", true, true));
        Assert.Equal(HttpStatusCode.BadRequest, foreignReturn.StatusCode);

        var unknownPayment = await _client.GetAsync($"{Api}/checkout/status/missing");
        Assert.Equal(HttpStatusCode.NotFound, unknownPayment.StatusCode);
    }

    [Fact]
    public async Task AuthorisedPayment_CreatesOneOrderPublishesHoldingAndProvisions()
    {
        var cart = await CreateCartAsync("CLAS");
        var session = await StartCheckoutAsync(cart);
        Assert.Equal("Stub", session.Gateway);
        Assert.Equal(310m, session.Amount);

        await SimulateAsync(session.PaymentId, "Authorised");
        await SimulateAsync(session.PaymentId, "Authorised");

        var authorised = await GetStatusAsync(session.PaymentId);
        Assert.Equal(PaymentStates.Authorised, authorised.PaymentState);
        Assert.NotNull(authorised.PspReference);
        Assert.Null(authorised.OrderNumber);
        Assert.Equal(cart.CorrelationId, authorised.CorrelationId);

        var paymentMessage = await WaitForMessageAsync(CommerceMessageTypes.PaymentTransactionAdded, session.PaymentId);
        var cartProcessor = CreateCartProcessor();
        await cartProcessor.HandleAsync(paymentMessage, CancellationToken.None);
        await cartProcessor.HandleAsync(paymentMessage, CancellationToken.None);

        var ordered = await GetStatusAsync(session.PaymentId);
        Assert.Equal(PaymentMessageHandler.OrderNumberFor(cart.Id), ordered.OrderNumber);
        Assert.Equal(OrderStates.Open, ordered.OrderState);
        Assert.Equal(ProvisioningStatuses.Pending, ordered.ProvisioningStatus);
        Assert.True(ordered.HoldingEventPublished);
        Assert.Equal(ProcessingResults.OrderCreated, ordered.ProcessingResult);

        var holding = Assert.Single(_publishedHoldings);
        Assert.Equal(cart.CorrelationId, holding.CorrelationId);
        var envelope = JsonDocument.Parse(holding.Body.ToString()).RootElement;
        Assert.Equal("ProductHoldingChange", envelope.GetProperty("type").GetString());
        Assert.Equal(CrmId, envelope.GetProperty("data").GetProperty("contactId").GetString());
        Assert.Equal("roadside-assistance", envelope.GetProperty("data").GetProperty("productType").GetString());

        var orderMessage = await WaitForMessageAsync(CommerceMessageTypes.OrderCreated, ordered.OrderId!);
        var fulfilment = new OrderCreatedHandler(_commerceTools.CreateCommerceClient(), NullLogger<OrderCreatedHandler>.Instance);
        await fulfilment.HandleAsync(orderMessage, CancellationToken.None);
        await fulfilment.HandleAsync(orderMessage, CancellationToken.None);

        var provisioned = await GetStatusAsync(session.PaymentId);
        Assert.Equal(OrderStates.Complete, provisioned.OrderState);
        Assert.Equal(ProvisioningStatuses.Provisioned, provisioned.ProvisioningStatus);

        var history = await _client.GetFromJsonAsync<MemberHistory>($"{Api}/members/{CrmId}/history", Json);
        Assert.Empty(history!.ActiveCarts);
        var order = Assert.Single(history.Orders);
        Assert.Equal(OrderStates.Complete, order.OrderState);
        Assert.Equal("Classic", order.CoverName);
    }

    [Fact]
    public async Task RefusedPayment_CreatesNoOrder()
    {
        var cart = await CreateCartAsync("STD");
        var session = await StartCheckoutAsync(cart);

        await SimulateAsync(session.PaymentId, "Refused");
        var paymentMessage = await WaitForMessageAsync(CommerceMessageTypes.PaymentTransactionAdded, session.PaymentId);
        await CreateCartProcessor().HandleAsync(paymentMessage, CancellationToken.None);

        var status = await GetStatusAsync(session.PaymentId);
        Assert.Equal(PaymentStates.Refused, status.PaymentState);
        Assert.Equal(ProcessingResults.PaymentRefused, status.ProcessingResult);
        Assert.Null(status.OrderId);
        Assert.Empty(_publishedHoldings);
    }

    [Fact]
    public async Task PendingPayment_StaysPendingUntilAuthorised()
    {
        var cart = await CreateCartAsync("STD");
        var session = await StartCheckoutAsync(cart);

        await SimulateAsync(session.PaymentId, "Pending");

        var status = await GetStatusAsync(session.PaymentId);
        Assert.Equal(PaymentStates.Pending, status.PaymentState);
        Assert.Equal(ProvisioningStatuses.NotStarted, status.ProvisioningStatus);
    }

    [Fact]
    public async Task StubNotify_RejectsUnknownOutcome()
    {
        var cart = await CreateCartAsync("STD");
        var session = await StartCheckoutAsync(cart);

        var response = await _client.PostAsJsonAsync($"{Api}/checkout/stub/{session.PaymentId}/notify", new SimulatePaymentRequest("Maybe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_AcceptsButIgnoresUnsignedNotifications()
    {
        var cart = await CreateCartAsync("STD");
        var session = await StartCheckoutAsync(cart);
        var unsigned = """
            {"live":"false","notificationItems":[{"NotificationRequestItem":{
              "eventCode":"AUTHORISATION","success":"true","pspReference":"FAKE","merchantReference":"REFERENCE",
              "amount":{"currency":"AUD","value":21000},"additionalData":{"hmacSignature":"forged"}}}]}
            """.Replace("REFERENCE", session.MerchantReference, StringComparison.Ordinal);

        var response = await _client.PostAsync($"{Api}/notifications/adyen", new StringContent(unsigned));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[accepted]", await response.Content.ReadAsStringAsync());
        Assert.Equal(PaymentStates.Pending, (await GetStatusAsync(session.PaymentId)).PaymentState);
    }

    [Fact]
    public async Task Cors_AllowsOnlyConfiguredOrigins()
    {
        Assert.True((await PreflightAsync(CommerceApiFactory.AllowedOrigin)).Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False((await PreflightAsync("https://attacker.example")).Headers.Contains("Access-Control-Allow-Origin"));
    }

    private async Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, $"{Api}/carts");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await _client.SendAsync(request);
    }

    private PaymentMessageHandler CreateCartProcessor()
    {
        var sender = new Mock<IServiceBusSender>();
        sender
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => _publishedHoldings.Add(message))
            .Returns(Task.CompletedTask);
        return new PaymentMessageHandler(
            _commerceTools.CreateCommerceClient(),
            new ContactEventPublisher(sender.Object, NullLogger<ContactEventPublisher>.Instance, TimeProvider.System),
            Options.Create(new CartProcessorSettings()),
            NullLogger<PaymentMessageHandler>.Instance);
    }

    private async Task<CommerceMessage> WaitForMessageAsync(string type, string resourceId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var message = _commerceTools.Publisher.Messages
                .Select(outbox => outbox.Message)
                .FirstOrDefault(message => message.Type == type && message.Resource.Id == resourceId);
            if (message is not null)
            {
                return message;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No {type} message for {resourceId}");
    }

    private async Task<CartView> CreateCartAsync(string coverId)
    {
        var response = await _client.PostAsJsonAsync(
            $"{Api}/carts",
            new CreateCartRequest(CrmId, "No", new VehicleSelection("RegoLookup", "1abc123"), coverId));
        return await ReadAsync<CartView>(response, HttpStatusCode.Created);
    }

    private async Task<CheckoutSessionResponse> StartCheckoutAsync(CartView cart)
    {
        var response = await _client.PostAsJsonAsync(
            $"{Api}/checkout/session",
            new StartCheckoutRequest(cart.Id, CrmId, $"{CommerceApiFactory.AllowedOrigin}/digital-site", true, true));
        return await ReadAsync<CheckoutSessionResponse>(response, HttpStatusCode.OK);
    }

    private async Task SimulateAsync(string paymentId, string outcome)
    {
        var response = await _client.PostAsJsonAsync($"{Api}/checkout/stub/{paymentId}/notify", new SimulatePaymentRequest(outcome));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<CheckoutStatus> GetStatusAsync(string paymentId) =>
        (await _client.GetFromJsonAsync<CheckoutStatus>($"{Api}/checkout/status/{paymentId}", Json))!;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected} but got {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }
}
