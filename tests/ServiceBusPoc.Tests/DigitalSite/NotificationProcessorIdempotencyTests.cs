using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ServiceBusPoc.DigitalSite.CommerceApi.Notifications;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.Tests.DigitalSite;

/// <summary>Tests for notification processor idempotency and state transitions.</summary>
public sealed class NotificationProcessorIdempotencyTests
{
    private const string MerchantReference = "RSA-test123";
    private const string PspReference = "PSP-abc123";

    private static ILogger<NotificationProcessor> CreateMockLogger() =>
        new Mock<ILogger<NotificationProcessor>>().Object;

    [Fact]
    public async Task ApplyAsync_DetectsDuplicateNotification()
    {
        var payment = PaymentWithInteraction("AUTHORISATION", PspReference, "true");
        var client = new Mock<ICommerceToolsClient>();
        client.Setup(c => c.GetPaymentByKeyAsync(MerchantReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var processor = new NotificationProcessor(client.Object, TimeProvider.System, CreateMockLogger());
        var update = PaymentUpdate(PspReference, "AUTHORISATION", true);

        var outcome = await processor.ApplyAsync(update, CancellationToken.None);

        Assert.Equal(NotificationOutcome.Duplicate, outcome);
        client.Verify(c => c.UpdatePaymentAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyAsync_ReturnsUnknownPaymentWhenNotFound()
    {
        var client = new Mock<ICommerceToolsClient>();
        client.Setup(c => c.GetPaymentByKeyAsync(MerchantReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var processor = new NotificationProcessor(client.Object, TimeProvider.System, CreateMockLogger());
        var update = PaymentUpdate(PspReference, "AUTHORISATION", true);

        var outcome = await processor.ApplyAsync(update, CancellationToken.None);

        Assert.Equal(NotificationOutcome.UnknownPayment, outcome);
    }

    [Fact]
    public async Task ApplyAsync_PreservesStateHierarchy_CannotRegressOnNonAuthorizationTransaction()
    {
        var payment = PaymentWithTransaction(TransactionTypes.Charge, TransactionStates.Failure, PspReference);
        var client = new Mock<ICommerceToolsClient>();
        client.Setup(c => c.GetPaymentByKeyAsync(MerchantReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        client.Setup(c => c.UpdatePaymentAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var processor = new NotificationProcessor(client.Object, TimeProvider.System, CreateMockLogger());
        var update = PaymentUpdate(PspReference, "CAPTURE", true);

        var outcome = await processor.ApplyAsync(update, CancellationToken.None);

        Assert.Equal(NotificationOutcome.InteractionRecorded, outcome);
    }

    [Fact]
    public async Task ApplyAsync_AllowsCorrectionOnAuthorization_FailureToSuccess()
    {
        var payment = PaymentWithTransaction(TransactionTypes.Authorization, TransactionStates.Failure, PspReference);
        var client = new Mock<ICommerceToolsClient>();
        client.Setup(c => c.GetPaymentByKeyAsync(MerchantReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        client.Setup(c => c.UpdatePaymentAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var processor = new NotificationProcessor(client.Object, TimeProvider.System, CreateMockLogger());
        var update = PaymentUpdate(PspReference, "AUTHORISATION", true);

        var outcome = await processor.ApplyAsync(update, CancellationToken.None);

        Assert.Equal(NotificationOutcome.TransactionUpdated, outcome);
    }

    [Fact]
    public async Task ApplyAsync_RecordsInteractionForEveryNotification()
    {
        var payment = PaymentWithVersion(1);
        var client = new Mock<ICommerceToolsClient>();
        client.Setup(c => c.GetPaymentByKeyAsync(MerchantReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        client.Setup(c => c.UpdatePaymentAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var processor = new NotificationProcessor(client.Object, TimeProvider.System, CreateMockLogger());
        var update = PaymentUpdate(PspReference, "PENDING", true);

        var outcome = await processor.ApplyAsync(update, CancellationToken.None);

        Assert.Equal(NotificationOutcome.InteractionRecorded, outcome);
        var calls = client.Invocations.Where(i => i.Method.Name == "UpdatePaymentAsync").ToList();
        Assert.Single(calls);
    }

    [Fact]
    public async Task ApplyAsync_UpdatesTransactionForNewNotification()
    {
        var payment = PaymentWithVersion(1);
        var client = new Mock<ICommerceToolsClient>();
        client.Setup(c => c.GetPaymentByKeyAsync(MerchantReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        client.Setup(c => c.UpdatePaymentAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyList<AbstractUpdateAction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var processor = new NotificationProcessor(client.Object, TimeProvider.System, CreateMockLogger());
        var update = PaymentUpdate(PspReference, "AUTHORISATION", true);

        var outcome = await processor.ApplyAsync(update, CancellationToken.None);

        Assert.Equal(NotificationOutcome.TransactionUpdated, outcome);
        var calls = client.Invocations.Where(i => i.Method.Name == "UpdatePaymentAsync").ToList();
        var actions = (IReadOnlyList<AbstractUpdateAction>)calls[0].Arguments[2];
        Assert.Equal(2, actions.Count);
        Assert.IsType<AddTransactionAction>(actions[0]);
        Assert.IsType<AddInterfaceInteractionAction>(actions[1]);
    }

    private static Payment PaymentWithVersion(long version)
    {
        return new Payment
        {
            Id = "payment-id",
            Key = MerchantReference,
            Version = version,
            AmountPlanned = new Money { CurrencyCode = "AUD", CentAmount = 1000 },
            Transactions = [],
            InterfaceInteractions = [],
        };
    }

    private static Payment PaymentWithTransaction(string type, string state, string interactionId)
    {
        return new Payment
        {
            Id = "payment-id",
            Key = MerchantReference,
            Version = 1,
            AmountPlanned = new Money { CurrencyCode = "AUD", CentAmount = 1000 },
            Transactions = [
                new Transaction
                {
                    Id = "transaction-id",
                    Type = type,
                    State = state,
                    Amount = new Money { CurrencyCode = "AUD", CentAmount = 1000 },
                    InteractionId = interactionId,
                    Timestamp = DateTimeOffset.UtcNow,
                },
            ],
            InterfaceInteractions = [],
        };
    }

    private static Payment PaymentWithInteraction(string eventCode, string pspReference, string success)
    {
        var interaction = new CustomFields
        {
            Type = new ResourceReference { Key = NotificationProcessor.InteractionTypeKey },
            Fields = new Dictionary<string, JsonElement>
            {
                ["eventCode"] = JsonSerializer.SerializeToElement(eventCode),
                ["pspReference"] = JsonSerializer.SerializeToElement(pspReference),
                ["success"] = JsonSerializer.SerializeToElement(success),
                ["createdAt"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("O")),
                ["merchantReference"] = JsonSerializer.SerializeToElement(MerchantReference),
                ["amount"] = JsonSerializer.SerializeToElement(1000),
                ["currency"] = JsonSerializer.SerializeToElement("AUD"),
            },
        };

        return new Payment
        {
            Id = "payment-id",
            Key = MerchantReference,
            Version = 1,
            AmountPlanned = new Money { CurrencyCode = "AUD", CentAmount = 1000 },
            Transactions = [],
            InterfaceInteractions = [interaction],
        };
    }

    private static PaymentUpdate PaymentUpdate(string pspReference, string eventCode, bool success)
    {
        var notification = new PaymentNotification(
            EventCode: eventCode,
            Success: success,
            PspReference: pspReference,
            OriginalReference: null,
            MerchantReference: MerchantReference,
            MerchantAccountCode: null,
            Amount: new Money { CurrencyCode = "AUD", CentAmount = 1000 },
            EventDate: DateTimeOffset.UtcNow,
            Reason: null,
            PaymentMethod: "visa",
            ModificationAction: null);

        return AdyenEventMapping.ToPaymentUpdate(notification);
    }
}
