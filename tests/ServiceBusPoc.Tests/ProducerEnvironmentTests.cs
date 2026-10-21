using Microsoft.Extensions.Configuration;
using ServiceBusPoc.AppHost;

namespace ServiceBusPoc.Tests;

public sealed class ProducerEnvironmentTests
{
    [Fact]
    public void Create_AbsentSettings_PreservesSpikeDefaults()
    {
        var settings = ProducerEnvironment.Create(new ConfigurationBuilder().Build());

        Assert.Equal("aspire-local-contact", settings["Producer__ContactId"]);
        Assert.Equal("Aspire", settings["Producer__FirstName"]);
        Assert.Equal("Local", settings["Producer__LastName"]);
        Assert.Equal("aspire-apphost", settings["Producer__Source"]);
        Assert.Equal("ASPIRE-LOCAL-001", settings["Producer__MembershipNumber"]);
        Assert.All(settings.Where(pair => pair.Key.StartsWith("Producer__Has")),
            pair => Assert.Equal("true", pair.Value));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Create_ExplicitFlags_RetainsEveryCombination(bool insurance, bool parks, bool carwash)
    {
        var configuration = Build(new()
        {
            ["Producer:HasInsurance"] = insurance.ToString(),
            ["Producer:HasParksResorts"] = parks.ToString(),
            ["Producer:HasCarwashProduct"] = carwash.ToString()
        });

        var settings = ProducerEnvironment.Create(configuration);

        Assert.Equal(insurance ? "true" : "false", settings["Producer__HasInsurance"]);
        Assert.Equal(parks ? "true" : "false", settings["Producer__HasParksResorts"]);
        Assert.Equal(carwash ? "true" : "false", settings["Producer__HasCarwashProduct"]);
    }

    [Fact]
    public void Create_PartialOverride_DefaultsOnlyAbsentFlags()
    {
        var settings = ProducerEnvironment.Create(Build(new()
        {
            ["Producer:HasInsurance"] = "false"
        }));

        Assert.Equal("false", settings["Producer__HasInsurance"]);
        Assert.Equal("true", settings["Producer__HasParksResorts"]);
        Assert.Equal("true", settings["Producer__HasCarwashProduct"]);
    }

    [Theory]
    [InlineData("Producer:HasInsurance")]
    [InlineData("Producer:HasParksResorts")]
    [InlineData("Producer:HasCarwashProduct")]
    public void Create_InvalidFlag_RejectsConfiguration(string key)
    {
        var configuration = Build(new() { [key] = "not-a-boolean" });

        Assert.Throws<InvalidOperationException>(() => ProducerEnvironment.Create(configuration));
    }

    [Theory]
    [InlineData("Producer:ContactId", "aspire-local-contact")]
    [InlineData("Producer:FirstName", "Aspire")]
    [InlineData("Producer:LastName", "Local")]
    [InlineData("Producer:Source", "aspire-apphost")]
    [InlineData("Producer:MembershipNumber", "ASPIRE-LOCAL-001")]
    public void Create_TextOverride_PreservesNonblankAndDefaultsWhitespace(string key, string fallback)
    {
        var environmentKey = key.Replace(":", "__");

        Assert.Equal("synthetic-value",
            ProducerEnvironment.Create(Build(new() { [key] = "synthetic-value" }))[environmentKey]);
        Assert.Equal(fallback,
            ProducerEnvironment.Create(Build(new() { [key] = " \t" }))[environmentKey]);
    }

    private static IConfiguration Build(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
