using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.Tests;

public sealed class CoreSerializationTests
{
    [Fact]
    public void EventEnvelope_RoundTripsWithCamelCaseAndOptionalNullsOmitted()
    {
        var envelope = new EventEnvelope<ContactData>
        {
            Id = "event-1",
            Type = "contact.updated",
            Source = "crm",
            Timestamp = new DateTime(2026, 9, 21, 2, 0, 0, DateTimeKind.Utc),
            DataVersion = "1.0",
            Data = new ContactData
            {
                ContactId = "C001",
                FirstName = "Ada",
                LastName = "Lovelace",
                Attributes = new ContactAttributes { HasInsurance = true }
            }
        };

        var json = JsonSerializer.Serialize(envelope, JsonSerializerOptionsHelper.DefaultOptions);
        var roundTrip = JsonSerializer.Deserialize<EventEnvelope<ContactData>>(
            json, JsonSerializerOptionsHelper.DefaultOptions);

        Assert.Contains("\"dataVersion\":\"1.0\"", json);
        Assert.DoesNotContain("correlationId", json);
        Assert.Equal(envelope.Id, roundTrip!.Id);
        Assert.Equal(envelope.Timestamp, roundTrip.Timestamp);
        Assert.True(roundTrip.Data!.Attributes!.HasInsurance);
        Assert.Null(roundTrip.Data.Email);
    }

    [Fact]
    public void IndentedOptions_ProducesMultilineJson()
    {
        var json = JsonSerializer.Serialize(
            new ContactAttributes { HasCarwashProduct = true },
            JsonSerializerOptionsHelper.IndentedOptions);

        Assert.Contains(Environment.NewLine, json);
        Assert.Contains("\"hasCarwashProduct\": true", json);
    }
}
