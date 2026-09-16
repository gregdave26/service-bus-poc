using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.DependencyInjection;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Tests producer and Service Bus option binding and validation.
/// </summary>
public sealed class ProducerConfigurationTests
{
    [Fact]
    public void AddProducerConfiguration_ValidValues_BindsSettings()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Producer:ContactId"] = "C001",
            ["Producer:FirstName"] = "John",
            ["Producer:LastName"] = "Smith",
            ["Producer:HasInsurance"] = "true"
        });
        var services = new ServiceCollection();
        services.AddProducerConfiguration(configuration);
        using var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<ProducerSettings>>().Value;

        Assert.Equal("C001", settings.ContactId);
        Assert.Equal("John", settings.FirstName);
        Assert.Equal("Smith", settings.LastName);
        Assert.True(settings.HasInsurance);
        Assert.Equal("producer", settings.Source);
    }

    [Fact]
    public void AddProducerConfiguration_MissingRequiredValue_ThrowsOptionsValidationException()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Producer:FirstName"] = "John",
            ["Producer:LastName"] = "Smith"
        });
        var services = new ServiceCollection();
        services.AddProducerConfiguration(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ProducerSettings>>().Value);
    }

    [Fact]
    public void AddServiceBusConfiguration_WhitespaceTopic_ThrowsOptionsValidationException()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ServiceBus:ConnectionString"] = "Endpoint=sb://localhost/",
            ["ServiceBus:Namespace"] = "sbemulatorns",
            ["ServiceBus:TopicName"] = " "
        });
        var services = new ServiceCollection();
        services.AddServiceBusConfiguration(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ServiceBusSettings>>().Value);
    }


    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
