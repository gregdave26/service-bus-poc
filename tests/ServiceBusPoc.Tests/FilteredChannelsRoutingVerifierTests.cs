using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.ContactEvents.Verifier.Services;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Tests;

public sealed class FilteredChannelsRoutingVerifierTests
{
    [Fact]
    public async Task VerifyAsync_ValidatesInsuranceAndParksResortsFilters()
    {
        var transport = new Mock<IFilteredChannelsRoutingTransport>();
        var messages = new Dictionary<string, Queue<ServiceBusReceivedMessage>>
        {
            [SubscriptionNames.Insurance] = new(),
            [SubscriptionNames.ParksResorts] = new(),
            [SubscriptionNames.Carwash] = new()
        };

        transport
            .Setup(x => x.PublishAsync(
                It.IsAny<ContactData>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<ContactData, string, CancellationToken>((contact, _, _) =>
            {
                var attributes = contact.Attributes!;
                if (attributes.HasInsurance)
                {
                    messages[SubscriptionNames.Insurance].Enqueue(Message(
                        $"filtered-{attributes.HasInsurance}-{attributes.HasParksResorts}-{attributes.HasCarwashProduct}"));
                }

                if (attributes.HasParksResorts)
                {
                    messages[SubscriptionNames.ParksResorts].Enqueue(Message(
                        $"filtered-{attributes.HasInsurance}-{attributes.HasParksResorts}-{attributes.HasCarwashProduct}"));
                }

                if (attributes.HasCarwashProduct)
                {
                    messages[SubscriptionNames.Carwash].Enqueue(Message(
                        $"filtered-{attributes.HasInsurance}-{attributes.HasParksResorts}-{attributes.HasCarwashProduct}"));
                }
            })
            .Returns(Task.CompletedTask);
        transport
            .Setup(x => x.ReceiveAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string subscription, TimeSpan _, CancellationToken _) =>
                messages[subscription].Count == 0
                    ? null
                    : messages[subscription].Dequeue());

        var verifier = new FilteredChannelsRoutingVerifier(
            transport.Object,
            Mock.Of<ILogger<FilteredChannelsRoutingVerifier>>());

        await verifier.VerifyAsync();

        transport.Verify(
            x => x.PublishAsync(
                It.IsAny<ContactData>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(8));
    }

    private static ServiceBusReceivedMessage Message(string correlationId) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            messageId: correlationId,
            correlationId: correlationId);
}
