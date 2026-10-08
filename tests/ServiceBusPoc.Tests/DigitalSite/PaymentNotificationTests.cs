using Microsoft.Extensions.Logging.Abstractions;
using ServiceBusPoc.DigitalSite.CommerceApi.Notifications;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class PaymentNotificationTests
{
    private const string HmacKey = "44782DEF547AAA06C910C43932B1EB0C71FC68D9D0C057550C48EC2ACF6BA056";
    private const string PaymentKey = "RSA-abc";

    [Theory]
    [InlineData("AUTHORISATION", true, TransactionTypes.Authorization, TransactionStates.Success)]
    [InlineData("authorisation", false, TransactionTypes.Authorization, TransactionStates.Failure)]
    [InlineData("CAPTURE", true, TransactionTypes.Charge, TransactionStates.Success)]
    [InlineData("REFUND", true, TransactionTypes.Refund, TransactionStates.Success)]
    [InlineData("CANCELLATION", true, TransactionTypes.CancelAuthorization, TransactionStates.Success)]
    [InlineData("NOTIFICATION_OF_CHARGEBACK", true, TransactionTypes.Chargeback, TransactionStates.Pending)]
    public void EventMapping_FollowsConnectorTable(string eventCode, bool success, string type, string state)
    {
        var update = AdyenEventMapping.ToPaymentUpdate(Notification(eventCode, success));

        Assert.True(update.ChangesTransaction);
        Assert.Equal(type, update.TransactionType);
        Assert.Equal(state, update.TransactionState);
    }

    [Theory]
    [InlineData("cancel", TransactionTypes.CancelAuthorization)]
    [InlineData("refund", TransactionTypes.Refund)]
    [InlineData(null, null)]
    public void EventMapping_ResolvesCancelOrRefundFromModificationAction(string? action, string? expectedType)
    {
        var update = AdyenEventMapping.ToPaymentUpdate(Notification("CANCEL_OR_REFUND", true) with { ModificationAction = action });

        Assert.Equal(expectedType, update.TransactionType);
        Assert.Equal(expectedType is not null, update.ChangesTransaction);
    }

    [Fact]
    public void EventMapping_RecordsUnmappedEventsAsInteractionsOnly()
    {
        var update = AdyenEventMapping.ToPaymentUpdate(Notification("PENDING", true));

        Assert.False(update.ChangesTransaction);
    }

    [Fact]
    public void Reader_AcceptsSignedItemsAndRejectsTamperedOrUnsignedOnes()
    {
        var reader = new AdyenNotificationReader(HmacKey);
        var signed = Item("PSP1");
        var tampered = Item("PSP2");
        var unsigned = Item("PSP3");
        reader.Sign(signed);
        reader.Sign(tampered);
        tampered.Amount!.Value = 1;
        var cancel = Item("PSP4");
        cancel.EventCode = "CANCEL_OR_REFUND";
        cancel.AdditionalData = new Dictionary<string, string> { [AdyenNotificationReader.ModificationActionKey] = "refund" };
        reader.Sign(cancel);

        var verification = reader.Verify(Payload(signed, tampered, unsigned, cancel));

        Assert.Equal(2, verification.RejectedCount);
        var notification = verification.Authentic[0];
        Assert.Equal("PSP1", notification.PspReference);
        Assert.Equal(PaymentKey, notification.MerchantReference);
        Assert.True(notification.Success);
        Assert.Equal(Money.FromCents("AUD", 31000), notification.Amount);
        Assert.Equal("refund", verification.Authentic[1].ModificationAction);
    }

    [Fact]
    public void Reader_RejectsItemsSignedWithAnotherKey()
    {
        var item = Item("PSP1");
        new AdyenNotificationReader(new string('A', 64)).Sign(item);

        var verification = new AdyenNotificationReader(HmacKey).Verify(Payload(item));

        Assert.Empty(verification.Authentic);
        Assert.Equal(1, verification.RejectedCount);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"notificationItems\":[]}")]
    public void Reader_RejectsPayloadsThatAreNotNotifications(string payload)
    {
        Assert.Throws<FormatException>(() => new AdyenNotificationReader(HmacKey).Verify(payload));
    }

    [Theory]
    [InlineData(TransactionTypes.Authorization, TransactionStates.Pending, TransactionStates.Success, true)]
    [InlineData(TransactionTypes.Authorization, TransactionStates.Failure, TransactionStates.Success, true)]
    [InlineData(TransactionTypes.Charge, TransactionStates.Failure, TransactionStates.Success, false)]
    [InlineData(TransactionTypes.Authorization, TransactionStates.Success, TransactionStates.Pending, false)]
    [InlineData(TransactionTypes.Authorization, TransactionStates.Success, TransactionStates.Success, false)]
    public void CanChangeState_OnlyMovesForwardOrCorrectsFailedAuthorisations(string type, string current, string next, bool expected)
    {
        Assert.Equal(expected, NotificationProcessor.CanChangeState(type, current, next));
    }

    [Fact]
    public async Task Processor_IgnoresUnknownPayments()
    {
        var commerceTools = new Mock<ICommerceToolsClient>();

        var outcome = await CreateProcessor(commerceTools).ApplyAsync(Authorised(), CancellationToken.None);

        Assert.Equal(NotificationOutcome.UnknownPayment, outcome);
        commerceTools.Verify(c => c.UpdatePaymentAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Processor_AddsTransactionAndInteractionForNewEvents()
    {
        var commerceTools = WithPayment(new Payment { Id = "p1", Version = 3, Key = PaymentKey });
        IReadOnlyList<AbstractUpdateAction>? actions = null;
        commerceTools
            .Setup(c => c.UpdatePaymentAsync("p1", 3, It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .Callback<string, long, IReadOnlyList<AbstractUpdateAction>, CancellationToken>((_, _, a, _) => actions = a)
            .ReturnsAsync(new Payment());

        var outcome = await CreateProcessor(commerceTools).ApplyAsync(Authorised(), CancellationToken.None);

        Assert.Equal(NotificationOutcome.TransactionUpdated, outcome);
        var add = Assert.IsType<AddTransactionAction>(actions![0]);
        Assert.Equal("PSP1", add.Transaction.InteractionId);
        Assert.Equal(TransactionStates.Success, add.Transaction.State);
        var interaction = Assert.IsType<AddInterfaceInteractionAction>(actions[1]);
        Assert.Equal(NotificationProcessor.InteractionTypeKey, interaction.Type.Key);
        Assert.Equal("true", interaction.Fields["success"]);
    }

    [Fact]
    public async Task Processor_AdvancesExistingTransactionsAndSkipsBackwardMoves()
    {
        var payment = new Payment
        {
            Id = "p1",
            Version = 1,
            Key = PaymentKey,
            Transactions = [new Transaction { Id = "t1", Type = TransactionTypes.Authorization, InteractionId = "PSP1", State = TransactionStates.Failure }],
        };
        var commerceTools = WithPayment(payment);
        var updates = new List<IReadOnlyList<AbstractUpdateAction>>();
        commerceTools
            .Setup(c => c.UpdatePaymentAsync("p1", 1, It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .Callback<string, long, IReadOnlyList<AbstractUpdateAction>, CancellationToken>((_, _, a, _) => updates.Add(a))
            .ReturnsAsync(new Payment());
        var processor = CreateProcessor(commerceTools);

        Assert.Equal(NotificationOutcome.TransactionUpdated, await processor.ApplyAsync(Authorised(), CancellationToken.None));
        payment.Transactions[0].State = TransactionStates.Success;
        var pending = new PaymentUpdate(Notification("AUTHORISATION_ADJUSTMENT", true), TransactionTypes.Authorization, TransactionStates.Pending);
        Assert.Equal(NotificationOutcome.InteractionRecorded, await processor.ApplyAsync(pending, CancellationToken.None));

        Assert.Equal(new ChangeTransactionStateAction("t1", TransactionStates.Success), updates[0][0]);
        Assert.IsType<AddInterfaceInteractionAction>(Assert.Single(updates[1]));
    }

    [Fact]
    public async Task Processor_IgnoresDuplicateDeliveries()
    {
        var interaction = new CustomFields
        {
            Type = ResourceReference.ByKey(ResourceTypes.Type, NotificationProcessor.InteractionTypeKey),
            Fields = new Dictionary<string, JsonElement>
            {
                ["eventCode"] = JsonSerializer.SerializeToElement("AUTHORISATION"),
                ["pspReference"] = JsonSerializer.SerializeToElement("PSP1"),
                ["success"] = JsonSerializer.SerializeToElement("true"),
            },
        };
        var commerceTools = WithPayment(new Payment { Id = "p1", Key = PaymentKey, InterfaceInteractions = [interaction] });

        var outcome = await CreateProcessor(commerceTools).ApplyAsync(Authorised(), CancellationToken.None);

        Assert.Equal(NotificationOutcome.Duplicate, outcome);
    }

    [Fact]
    public async Task Processor_RetriesConcurrentModificationsThenGivesUp()
    {
        var commerceTools = WithPayment(new Payment { Id = "p1", Key = PaymentKey });
        commerceTools
            .SetupSequence(c => c.UpdatePaymentAsync("p1", It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(Conflict())
            .ReturnsAsync(new Payment())
            .ThrowsAsync(Conflict())
            .ThrowsAsync(Conflict())
            .ThrowsAsync(Conflict());
        var processor = CreateProcessor(commerceTools);

        Assert.Equal(NotificationOutcome.TransactionUpdated, await processor.ApplyAsync(Authorised(), CancellationToken.None));
        await Assert.ThrowsAsync<ConcurrentModificationException>(() => processor.ApplyAsync(Authorised(), CancellationToken.None));
    }

    [Fact]
    public async Task Module_AppliesAuthenticItemsOnly()
    {
        var gateway = new StubPaymentGateway(TimeProvider.System);
        var commerceTools = WithPayment(new Payment { Id = "p1", Key = PaymentKey });
        commerceTools
            .Setup(c => c.UpdatePaymentAsync("p1", It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payment());
        var module = new AdyenNotificationModule(gateway, CreateProcessor(commerceTools), NullLogger<AdyenNotificationModule>.Instance);
        var signed = gateway.CreateSignedNotification(StubPaymentOutcome.Refused, PaymentKey, Money.FromCents("AUD", 31000));

        var outcomes = await module.HandleAsync(signed, CancellationToken.None);
        var forged = await module.HandleAsync(Payload(Item("PSP9")), CancellationToken.None);

        Assert.Equal(NotificationOutcome.TransactionUpdated, Assert.Single(outcomes));
        Assert.Empty(forged);
    }

    private static NotificationProcessor CreateProcessor(Mock<ICommerceToolsClient> commerceTools) =>
        new(commerceTools.Object, TimeProvider.System, NullLogger<NotificationProcessor>.Instance);

    private static Mock<ICommerceToolsClient> WithPayment(Payment payment)
    {
        var commerceTools = new Mock<ICommerceToolsClient>();
        commerceTools.Setup(c => c.GetPaymentByKeyAsync(PaymentKey, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        return commerceTools;
    }

    private static ConcurrentModificationException Conflict() => new("conflict", ["ConcurrentModification"]);

    private static PaymentUpdate Authorised() => AdyenEventMapping.ToPaymentUpdate(Notification("AUTHORISATION", true));

    private static PaymentNotification Notification(string eventCode, bool success) =>
        new(eventCode, success, "PSP1", null, PaymentKey, "Merchant", Money.FromCents("AUD", 31000), null, null, "visa", null);

    private static AdyenNotificationItemPayload Item(string pspReference) =>
        new()
        {
            EventCode = "AUTHORISATION",
            Success = "true",
            PspReference = pspReference,
            MerchantReference = PaymentKey,
            MerchantAccountCode = "Merchant",
            Amount = new AdyenAmountPayload { Currency = "AUD", Value = 31000 },
        };

    private static string Payload(params AdyenNotificationItemPayload[] items) =>
        JsonSerializer.Serialize(new AdyenNotificationRequestPayload
        {
            Live = "false",
            NotificationItems = [.. items.Select(item => new AdyenNotificationContainerPayload { NotificationRequestItem = item })],
        });
}
