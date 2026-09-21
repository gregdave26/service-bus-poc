using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Tests;

public sealed class SubscriptionRoutingTests
{
    [Fact]
    public void ExpectedSubscriptions_NullAttributes_ReturnsDigitalChannelsOnly()
    {
        var result = SubscriptionRouting.ExpectedSubscriptions(null);

        Assert.Equal([SubscriptionNames.DigitalChannels], result);
    }

    [Fact]
    public void ExpectedSubscriptions_AllCapabilities_ReturnsTopologyOrder()
    {
        var result = SubscriptionRouting.ExpectedSubscriptions(new ContactAttributes
        {
            HasInsurance = true,
            HasParksResorts = true,
            HasCarwashProduct = true
        });

        Assert.Equal(
            [
                SubscriptionNames.DigitalChannels,
                SubscriptionNames.Insurance,
                SubscriptionNames.ParksResorts,
                SubscriptionNames.Carwash
            ],
            result);
    }

    [Fact]
    public void ExpectedSubscriptions_NoCapabilities_ReturnsDigitalChannelsOnly()
    {
        var result = SubscriptionRouting.ExpectedSubscriptions(new ContactAttributes());

        Assert.Equal([SubscriptionNames.DigitalChannels], result);
    }
}
