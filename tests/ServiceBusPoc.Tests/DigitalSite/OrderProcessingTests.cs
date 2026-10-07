using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalSite.CartProcessor;
using ServiceBusPoc.DigitalSite.FulfilmentStub;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class OrderProcessingTests
{
    private readonly Mock<ICommerceToolsClient> _commerceTools = new();
    private readonly Mock<IServiceBusSender> _sender = new();

    [Fact]
    public async Task CartProcessor_IgnoresOtherMessagesAndMissingPayments()
    {
        var handler = CreateCartProcessor();

        await handler.HandleAsync(Message(CommerceMessageTypes.OrderCreated, "o1"), CancellationToken.None);
        await handler.HandleAsync(Message(CommerceMessageTypes.PaymentTransactionAdded, "missing"), CancellationToken.None);

        _commerceTools.Verify(c => c.GetPaymentAsync("o1", It.IsAny<CancellationToken>()), Times.Never);
        _commerceTools.Verify(c => c.CreateOrderFromCartAsync(It.IsAny<OrderFromCartDraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CartProcessor_WaitsWhileAuthorisationIsPending()
    {
        SetupPayment(Payment(TransactionStates.Pending));

        await CreateCartProcessor().HandleAsync(Message(CommerceMessageTypes.PaymentTransactionAdded, "p1"), CancellationToken.None);

        VerifyNoPaymentUpdate();
    }

    [Fact]
    public async Task CartProcessor_RecordsMissingCart()
    {
        SetupPayment(Payment(TransactionStates.Success));

        await CreateCartProcessor().HandleAsync(Message(CommerceMessageTypes.PaymentTransactionAdded, "p1"), CancellationToken.None);

        VerifyProcessingResult(ProcessingResults.CartNotFound);
    }

    [Fact]
    public async Task CartProcessor_RecordsAmountMismatchWithoutOrdering()
    {
        SetupPayment(Payment(TransactionStates.Success));
        SetupCart(Cart(CartStates.Active, centAmount: 99));

        await CreateCartProcessor().HandleAsync(Message(CommerceMessageTypes.PaymentTransactionAdded, "p1"), CancellationToken.None);

        VerifyProcessingResult(ProcessingResults.AmountMismatch);
        _commerceTools.Verify(c => c.CreateOrderFromCartAsync(It.IsAny<OrderFromCartDraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CartProcessor_RecordsCartOrderedByAnotherPayment()
    {
        SetupPayment(Payment(TransactionStates.Success));
        SetupCart(Cart(CartStates.Ordered));
        SetupOrders(Order(paymentId: "other"));

        await CreateCartProcessor().HandleAsync(Message(CommerceMessageTypes.PaymentTransactionAdded, "p1"), CancellationToken.None);

        VerifyProcessingResult(ProcessingResults.CartAlreadyOrdered);
        _sender.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CartProcessor_UsesConcurrentlyCreatedOrderAndRetriesConflictingUpdates()
    {
        SetupPayment(Payment(TransactionStates.Success));
        _commerceTools.SetupSequence(c => c.GetCartAsync("c1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cart(CartStates.Active))
            .ReturnsAsync(Cart(CartStates.Ordered));
        _commerceTools.Setup(c => c.CreateOrderFromCartAsync(It.IsAny<OrderFromCartDraft>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CommerceToolsException(System.Net.HttpStatusCode.BadRequest, "ordered", ["InvalidOperation"]));
        SetupOrders(Order(paymentId: "p1"));
        _commerceTools.Setup(c => c.GetOrderAsync("o1", It.IsAny<CancellationToken>())).ReturnsAsync(Order(paymentId: "p1", version: 2));
        _commerceTools.SetupSequence(c => c.UpdateOrderAsync("o1", It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrentModificationException("conflict", []))
            .ReturnsAsync(new Order());
        _commerceTools.Setup(c => c.UpdatePaymentAsync("p1", It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payment());

        await CreateCartProcessor().HandleAsync(Message(CommerceMessageTypes.PaymentTransactionStateChanged, "p1"), CancellationToken.None);

        _sender.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _commerceTools.Verify(c => c.UpdateOrderAsync("o1", 2, It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyProcessingResult(ProcessingResults.OrderCreated);
    }

    [Fact]
    public async Task CartProcessor_SkipsHoldingEventForOrdersWithoutCrmId()
    {
        SetupPayment(Payment(TransactionStates.Success));
        SetupCart(Cart(CartStates.Ordered));
        var order = Order(paymentId: "p1");
        order.AnonymousId = null;
        SetupOrders(order);

        await CreateCartProcessor().HandleAsync(Message(CommerceMessageTypes.PaymentTransactionAdded, "p1"), CancellationToken.None);

        _sender.Verify(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyProcessingResult(ProcessingResults.OrderCreated);
    }

    [Theory]
    [InlineData("6e5e468b-cbf5-4edd-8bd7-abd80f03cd86", "RSA-6E5E468BCB")]
    [InlineData("c-1", "RSA-C1")]
    public void OrderNumberFor_IsStablePerCart(string cartId, string expected)
    {
        Assert.Equal(expected, PaymentMessageHandler.OrderNumberFor(cartId));
    }

    [Fact]
    public async Task Fulfilment_IgnoresOtherMessagesMissingAndCompleteOrders()
    {
        var handler = new OrderCreatedHandler(_commerceTools.Object, NullLogger<OrderCreatedHandler>.Instance);
        var complete = Order(paymentId: "p1");
        complete.OrderState = OrderStates.Complete;
        _commerceTools.Setup(c => c.GetOrderAsync("o1", It.IsAny<CancellationToken>())).ReturnsAsync(complete);

        await handler.HandleAsync(Message(CommerceMessageTypes.PaymentTransactionAdded, "o1"), CancellationToken.None);
        await handler.HandleAsync(Message(CommerceMessageTypes.OrderCreated, "missing"), CancellationToken.None);
        await handler.HandleAsync(Message(CommerceMessageTypes.OrderCreated, "o1"), CancellationToken.None);

        _commerceTools.Verify(c => c.UpdateOrderAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fulfilment_RetriesVersionConflicts()
    {
        _commerceTools.SetupSequence(c => c.GetOrderAsync("o1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Order(paymentId: "p1", version: 1))
            .ReturnsAsync(Order(paymentId: "p1", version: 2));
        _commerceTools.SetupSequence(c => c.UpdateOrderAsync("o1", It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrentModificationException("conflict", []))
            .ReturnsAsync(new Order());

        await new OrderCreatedHandler(_commerceTools.Object, NullLogger<OrderCreatedHandler>.Instance)
            .HandleAsync(Message(CommerceMessageTypes.OrderCreated, "o1"), CancellationToken.None);

        _commerceTools.Verify(
            c => c.UpdateOrderAsync("o1", 2, It.Is<IReadOnlyList<AbstractUpdateAction>>(a => a.Single().Equals(new ChangeOrderStateAction(OrderStates.Complete))), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private PaymentMessageHandler CreateCartProcessor() =>
        new(
            _commerceTools.Object,
            new ContactEventPublisher(_sender.Object, NullLogger<ContactEventPublisher>.Instance, TimeProvider.System),
            Options.Create(new CartProcessorSettings()),
            NullLogger<PaymentMessageHandler>.Instance);

    private void SetupPayment(Payment payment) =>
        _commerceTools.Setup(c => c.GetPaymentAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

    private void SetupCart(Cart cart) =>
        _commerceTools.Setup(c => c.GetCartAsync(cart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

    private void SetupOrders(params Order[] orders) =>
        _commerceTools.Setup(c => c.QueryOrdersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(orders);

    private void VerifyProcessingResult(string result) =>
        _commerceTools.Verify(
            c => c.UpdatePaymentAsync("p1", It.IsAny<long>(), It.Is<IReadOnlyList<AbstractUpdateAction>>(a => a.Single().Equals(new SetCustomFieldAction("processingResult", result))), It.IsAny<CancellationToken>()),
            Times.Once);

    private void VerifyNoPaymentUpdate() =>
        _commerceTools.Verify(c => c.UpdatePaymentAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()), Times.Never);

    private static CommerceMessage Message(string type, string resourceId) =>
        new() { Type = type, Resource = ResourceReference.ById(ResourceTypes.Payment, resourceId) };

    private static Payment Payment(string authorisationState) =>
        new()
        {
            Id = "p1",
            Key = "RSA-1",
            Transactions = [new Transaction { Id = "t1", Type = TransactionTypes.Authorization, State = authorisationState, Amount = Money.FromCents("AUD", 31000), InteractionId = "PSP1" }],
            Custom = Fields(("cartId", "c1"), ("correlationId", "corr-1")),
        };

    private static Cart Cart(string state, long centAmount = 31000) =>
        new() { Id = "c1", Version = 4, AnonymousId = "CRM-12345678", CartState = state, TotalPrice = Money.FromCents("AUD", centAmount) };

    private static Order Order(string paymentId, long version = 1) =>
        new()
        {
            Id = "o1",
            Version = version,
            OrderNumber = "RSA-C1",
            AnonymousId = "CRM-12345678",
            TotalPrice = Money.FromCents("AUD", 31000),
            PaymentInfo = new PaymentInfo { Payments = [ResourceReference.ById(ResourceTypes.Payment, paymentId)] },
            LineItems = [new LineItem { Name = new Dictionary<string, string> { ["en-AU"] = "Classic" }, Variant = new ProductVariant { Sku = "CLAS" } }],
        };

    private static CustomFields Fields(params (string Name, string Value)[] fields) =>
        new() { Fields = fields.ToDictionary(field => field.Name, field => JsonSerializer.SerializeToElement(field.Value)) };
}
