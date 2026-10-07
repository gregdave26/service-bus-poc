using System.Net;
using System.Net.Http.Headers;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class CommerceToolsStubTests : IDisposable
{
    private readonly CommerceToolsStubFactory _factory = new();
    private readonly CommerceToolsClient _client;

    public CommerceToolsStubTests()
    {
        _client = _factory.CreateCommerceClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Catalog_IsSeededWithRoadsideAssistanceProducts()
    {
        var productType = await _client.GetProductTypeByKeyAsync("roadside-assistance");

        var products = await _client.QueryProductProjectionsAsync(
            $"productType(id = \"{productType!.Id}\")");

        Assert.Equal(4, products.Count);
        var classic = Assert.Single(products, product => product.MasterVariant.Sku == "CLAS");
        Assert.Equal("level-classic", classic.MasterVariant.GetEnumKey("Level"));
        Assert.Equal(31000, classic.MasterVariant.Prices[0].Value.CentAmount);
        Assert.Null(await _client.GetProductTypeByKeyAsync("missing"));
    }

    [Fact]
    public async Task Cart_CreateQueryUpdateAndDelete()
    {
        var cart = await CreateCartAsync("CRM-00000001", "CLAS");

        Assert.Equal(1, cart.Version);
        Assert.Equal(31000, cart.TotalPrice.CentAmount);
        Assert.Equal("AUD", cart.TotalPrice.CurrencyCode);
        Assert.Equal("corr-1", cart.Custom!.GetString("correlationId"));

        var active = await _client.QueryCartsAsync(CommercePredicate.And(
            CommercePredicate.EqualTo("anonymousId", "CRM-00000001"),
            CommercePredicate.EqualTo("cartState", CartStates.Active)));
        Assert.Equal(cart.Id, Assert.Single(active).Id);
        Assert.Empty(await _client.QueryCartsAsync(CommercePredicate.EqualTo("anonymousId", "CRM-99999999")));

        var updated = await _client.UpdateCartAsync(cart.Id, cart.Version,
        [
            new RemoveLineItemAction(cart.LineItems[0].Id),
            new AddLineItemAction("ULTI"),
            new SetCustomFieldAction("vehicleRego", "1ABC123")
        ]);
        Assert.Equal(2, updated.Version);
        Assert.Equal(41000, updated.TotalPrice.CentAmount);
        Assert.Equal("ULTI", Assert.Single(updated.LineItems).Variant.Sku);
        Assert.Equal("1ABC123", updated.Custom!.GetString("vehicleRego"));

        await Assert.ThrowsAsync<ConcurrentModificationException>(
            () => _client.UpdateCartAsync(cart.Id, cart.Version, [new AddLineItemAction("STD")]));

        await _client.DeleteCartAsync(cart.Id, updated.Version);
        Assert.Null(await _client.GetCartAsync(cart.Id));
    }

    [Fact]
    public async Task Cart_InvalidRequests_ReturnCommerceToolsErrors()
    {
        var unknownSku = await Assert.ThrowsAsync<CommerceToolsException>(() => CreateCartAsync("CRM-00000002", "NOPE"));
        Assert.Equal(HttpStatusCode.BadRequest, unknownSku.StatusCode);
        Assert.Contains(CommerceToolsErrorCodes.InvalidInput, unknownSku.ErrorCodes);

        var cart = await CreateCartAsync("CRM-00000002", "STD");
        var unknownLine = await Assert.ThrowsAsync<CommerceToolsException>(
            () => _client.UpdateCartAsync(cart.Id, cart.Version, [new RemoveLineItemAction("missing")]));
        Assert.Equal(HttpStatusCode.BadRequest, unknownLine.StatusCode);

        var unsupported = await Assert.ThrowsAsync<CommerceToolsException>(
            () => _client.UpdateCartAsync(cart.Id, cart.Version, [new ChangeOrderStateAction(OrderStates.Complete)]));
        Assert.Contains(CommerceToolsErrorCodes.InvalidInput, unsupported.ErrorCodes);
    }

    [Fact]
    public async Task Payment_TransactionsEmitSequencedMessages()
    {
        var payment = await CreatePaymentAsync("RSAPAY-1", 31000);
        var duplicate = await Assert.ThrowsAsync<CommerceToolsException>(() => CreatePaymentAsync("RSAPAY-1", 31000));
        Assert.Contains(CommerceToolsErrorCodes.DuplicateField, duplicate.ErrorCodes);
        Assert.Equal(payment.Id, (await _client.GetPaymentByKeyAsync("RSAPAY-1"))!.Id);
        Assert.Null(await _client.GetPaymentByKeyAsync("RSAPAY-missing"));

        var withTransaction = await _client.UpdatePaymentAsync(payment.Id, payment.Version,
        [
            new AddTransactionAction(new TransactionDraft
            {
                Type = TransactionTypes.Authorization,
                Amount = Money.FromCents("AUD", 31000),
                InteractionId = "PSP1",
                State = TransactionStates.Pending
            }),
            new AddInterfaceInteractionAction(ResourceReference.ByKey(ResourceTypes.Type, "ctp-adyen-integration-interaction-notification"), new Dictionary<string, object?> { ["eventCode"] = "AUTHORISATION" })
        ]);
        var transaction = Assert.Single(withTransaction.Transactions);
        Assert.Single(withTransaction.InterfaceInteractions);
        await _client.UpdatePaymentAsync(payment.Id, withTransaction.Version, [new ChangeTransactionStateAction(transaction.Id, TransactionStates.Success)]);

        var messages = await _factory.Publisher.WaitForAsync(2);
        Assert.Collection(
            messages,
            added =>
            {
                Assert.Equal(CommerceMessageTypes.PaymentTransactionAdded, added.Message.Type);
                Assert.Equal(1, added.Message.SequenceNumber);
                Assert.Equal(payment.Id, added.Message.Resource.Id);
                Assert.Equal("corr-1", added.CorrelationId);
                Assert.Equal("PSP1", added.Message.Extensions!["transaction"].GetProperty("interactionId").GetString());
            },
            changed =>
            {
                Assert.Equal(CommerceMessageTypes.PaymentTransactionStateChanged, changed.Message.Type);
                Assert.Equal(2, changed.Message.SequenceNumber);
                Assert.Equal("Success", changed.Message.Extensions!["state"].GetString());
            });
    }

    [Fact]
    public async Task Order_CreatedFromCartOnceAndCompleted()
    {
        var cart = await CreateCartAsync("CRM-00000003", "ULPL");
        var payment = await CreatePaymentAsync("RSAPAY-2", 51000);
        cart = await _client.UpdateCartAsync(cart.Id, cart.Version, [new AddPaymentAction(ResourceReference.ById(ResourceTypes.Payment, payment.Id))]);

        var order = await _client.CreateOrderFromCartAsync(new OrderFromCartDraft
        {
            Cart = ResourceReference.ById(ResourceTypes.Cart, cart.Id),
            Version = cart.Version,
            OrderNumber = "RSA-00000001"
        });

        Assert.Equal(OrderStates.Open, order.OrderState);
        Assert.Equal("CRM-00000003", order.AnonymousId);
        Assert.Equal(51000, order.TotalPrice.CentAmount);
        Assert.Equal(payment.Id, Assert.Single(order.PaymentInfo!.Payments).Id);
        Assert.Equal(CartStates.Ordered, (await _client.GetCartAsync(cart.Id))!.CartState);

        var again = await Assert.ThrowsAsync<CommerceToolsException>(() => _client.CreateOrderFromCartAsync(new OrderFromCartDraft
        {
            Cart = ResourceReference.ById(ResourceTypes.Cart, cart.Id),
            Version = cart.Version + 1
        }));
        Assert.Contains(CommerceToolsErrorCodes.InvalidOperation, again.ErrorCodes);

        var completed = await _client.UpdateOrderAsync(order.Id, order.Version,
            [new ChangeOrderStateAction(OrderStates.Complete), new SetCustomFieldAction("holdingEventId", "evt-1")]);
        Assert.Equal(OrderStates.Complete, completed.OrderState);
        Assert.Equal("evt-1", completed.Custom!.GetString("holdingEventId"));

        var history = await _client.QueryOrdersAsync(CommercePredicate.EqualTo("anonymousId", "CRM-00000003"));
        Assert.Equal(order.Id, Assert.Single(history).Id);
        Assert.Equal(order.Id, (await _client.GetOrderAsync(order.Id))!.Id);

        var messages = await _factory.Publisher.WaitForAsync(2);
        Assert.Equal([CommerceMessageTypes.OrderCreated, CommerceMessageTypes.OrderStateChanged], messages.Select(message => message.Message.Type));
        Assert.Equal("RSA-00000001", messages[0].Message.Extensions!["order"].GetProperty("orderNumber").GetString());
    }

    [Fact]
    public async Task Order_DuplicateOrderNumberAndStaleCartAreRejected()
    {
        var first = await CreateCartAsync("CRM-00000004", "STD");
        await _client.CreateOrderFromCartAsync(new OrderFromCartDraft { Cart = ResourceReference.ById(ResourceTypes.Cart, first.Id), Version = 1, OrderNumber = "RSA-DUP" });
        var second = await CreateCartAsync("CRM-00000004", "STD");

        var duplicate = await Assert.ThrowsAsync<CommerceToolsException>(() => _client.CreateOrderFromCartAsync(
            new OrderFromCartDraft { Cart = ResourceReference.ById(ResourceTypes.Cart, second.Id), Version = 1, OrderNumber = "RSA-DUP" }));
        Assert.Contains(CommerceToolsErrorCodes.DuplicateField, duplicate.ErrorCodes);
        await Assert.ThrowsAsync<ConcurrentModificationException>(() => _client.CreateOrderFromCartAsync(
            new OrderFromCartDraft { Cart = ResourceReference.ById(ResourceTypes.Cart, second.Id), Version = 7 }));
    }

    [Fact]
    public async Task UnknownProjectKey_ReturnsNotFound()
    {
        var httpClient = _factory.CreateClient();

        var response = await httpClient.GetAsync("/other-project/carts?where=");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(CommerceToolsErrorCodes.ResourceNotFound, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MalformedWhere_ReturnsInvalidInput()
    {
        var error = await Assert.ThrowsAsync<CommerceToolsException>(() => _client.QueryCartsAsync("anonymousId > 1"));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task Outbox_RetriesAfterPublishFailure()
    {
        _factory.Publisher.FailuresRemaining = 1;
        var payment = await CreatePaymentAsync("RSAPAY-3", 21000);

        await _client.UpdatePaymentAsync(payment.Id, payment.Version, [new AddTransactionAction(new TransactionDraft
        {
            Type = TransactionTypes.Authorization,
            Amount = Money.FromCents("AUD", 21000),
            State = TransactionStates.Success
        })]);

        var messages = await _factory.Publisher.WaitForAsync(1);
        Assert.Single(messages);
        Assert.Equal(0, _factory.Publisher.FailuresRemaining);
    }

    private Task<Cart> CreateCartAsync(string anonymousId, string sku) =>
        _client.CreateCartAsync(new CartDraft
        {
            Currency = "AUD",
            AnonymousId = anonymousId,
            Country = "AU",
            ShippingMethod = ResourceReference.ByKey(ResourceTypes.ShippingMethod, "digital"),
            ShippingAddress = new Address { Country = "AU" },
            LineItems = [new LineItemDraft { Sku = sku }],
            Custom = new CustomFieldsDraft
            {
                Type = ResourceReference.ByKey(ResourceTypes.Type, "rsa-cart"),
                Fields = new Dictionary<string, object?> { ["correlationId"] = "corr-1" }
            }
        });

    private Task<Payment> CreatePaymentAsync(string key, long centAmount) =>
        _client.CreatePaymentAsync(new PaymentDraft
        {
            Key = key,
            AmountPlanned = Money.FromCents("AUD", centAmount),
            PaymentMethodInfo = new PaymentMethodInfo { PaymentInterface = "adyen" },
            Custom = new CustomFieldsDraft
            {
                Type = ResourceReference.ByKey(ResourceTypes.Type, "rsa-payment"),
                Fields = new Dictionary<string, object?> { ["correlationId"] = "corr-1" }
            }
        });
}

public sealed class CommerceToolsStubAuthenticationTests : IDisposable
{
    private readonly CommerceToolsStubFactory _factory = new("client-id", "client-secret");

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ApiCallsWithoutToken_AreRejected()
    {
        var error = await Assert.ThrowsAsync<CommerceToolsException>(
            () => _factory.CreateCommerceClient().QueryCartsAsync(""));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task TokenEndpoint_RejectsWrongCredentials()
    {
        var httpClient = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String("client-id:wrong"u8.ToArray()));

        var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthHandler_ObtainsAndReusesToken()
    {
        var tokenClientFactory = new Mock<IHttpClientFactory>();
        tokenClientFactory.Setup(factory => factory.CreateClient(CommerceToolsAuthHandler.TokenClientName))
            .Returns(() => _factory.CreateClient());
        var settings = Microsoft.Extensions.Options.Options.Create(new ServiceBusPoc.Core.Configuration.CommerceToolsSettings
        {
            ApiUrl = "http://localhost",
            ProjectKey = CommerceToolsStubFactory.ProjectKey,
            AuthUrl = "http://localhost",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });
        var handler = new CommerceToolsAuthHandler(tokenClientFactory.Object, settings, TimeProvider.System)
        {
            InnerHandler = _factory.Server.CreateHandler()
        };
        var client = new CommerceToolsClient(new HttpClient(handler) { BaseAddress = new Uri($"http://localhost/{CommerceToolsStubFactory.ProjectKey}/") });

        Assert.NotNull(await client.GetProductTypeByKeyAsync("roadside-assistance"));
        Assert.Empty(await client.QueryCartsAsync(""));
        tokenClientFactory.Verify(factory => factory.CreateClient(CommerceToolsAuthHandler.TokenClientName), Times.Once);
    }

    [Fact]
    public async Task AuthHandler_WrongCredentials_Throws()
    {
        var tokenClientFactory = new Mock<IHttpClientFactory>();
        tokenClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(() => _factory.CreateClient());
        var settings = Microsoft.Extensions.Options.Options.Create(new ServiceBusPoc.Core.Configuration.CommerceToolsSettings
        {
            ApiUrl = "http://localhost",
            ProjectKey = CommerceToolsStubFactory.ProjectKey,
            AuthUrl = "http://localhost",
            ClientId = "client-id",
            ClientSecret = "nope",
            Scope = "manage_project:rac-rsa-poc"
        });
        var handler = new CommerceToolsAuthHandler(tokenClientFactory.Object, settings, TimeProvider.System)
        {
            InnerHandler = _factory.Server.CreateHandler()
        };
        var client = new CommerceToolsClient(new HttpClient(handler) { BaseAddress = new Uri($"http://localhost/{CommerceToolsStubFactory.ProjectKey}/") });

        var error = await Assert.ThrowsAsync<CommerceToolsException>(() => client.QueryCartsAsync(""));
        Assert.Contains(CommerceToolsErrorCodes.InvalidToken, error.ErrorCodes);
    }
}
