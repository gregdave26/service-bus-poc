using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddServiceBusConfiguration_ValidValues_BindsAllSettings()
    {
        var services = new ServiceCollection();
        services.AddServiceBusConfiguration(Configuration(
            ("ServiceBus:ConnectionString", "Endpoint=sb://localhost/"),
            ("ServiceBus:Namespace", "emulator"),
            ("ServiceBus:TopicName", "contact.events"),
            ("ServiceBus:SubscriptionName", "insurance")));

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<ServiceBusSettings>>().Value;

        Assert.Equal("Endpoint=sb://localhost/", settings.ConnectionString);
        Assert.Equal("emulator", settings.Namespace);
        Assert.Equal("contact.events", settings.TopicName);
        Assert.Equal("insurance", settings.SubscriptionName);
    }

    [Fact]
    public void AddServiceBusConfiguration_MissingConnectionString_ThrowsValidation()
    {
        var services = new ServiceCollection();
        services.AddServiceBusConfiguration(Configuration(
            ("ServiceBus:Namespace", "emulator"),
            ("ServiceBus:TopicName", "contact.events")));

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ServiceBusSettings>>().Value);
    }

    [Fact]
    public void AddProducerConfiguration_InvalidEmail_ThrowsValidation()
    {
        var services = new ServiceCollection();
        services.AddProducerConfiguration(Configuration(
            ("Producer:ContactId", "C001"),
            ("Producer:FirstName", "Ada"),
            ("Producer:LastName", "Lovelace"),
            ("Producer:Email", "not-an-email"),
            ("Producer:Source", "crm")));

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ProducerSettings>>().Value);
    }

    [Fact]
    public void AddCarwashConfiguration_BindsSettings()
    {
        var services = new ServiceCollection();
        services.AddCarwashConfiguration(Configuration(
            ("Carwash:ApiUrl", "https://carwash.example"),
            ("Carwash:MockMode", "true"),
            ("Carwash:AuthToken", "test-token")));

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<CarwashSettings>>().Value;

        Assert.Equal("https://carwash.example", settings.ApiUrl);
        Assert.True(settings.MockMode);
        Assert.Equal("test-token", settings.AuthToken);
    }

    [Fact]
    public void AddDashboardConfiguration_BindsBothSettingsTypes()
    {
        var services = new ServiceCollection();
        services.AddDashboardConfiguration(Configuration(
            ("Dashboard:Enabled", "false"),
            ("Dashboard:Url", "http://dashboard.local:5200"),
            ("Dashboard:Port", "5200"),
            ("Dashboard:ExpectedServices", " producer;insurance,producer ")));

        using var provider = services.BuildServiceProvider();
        var dashboard = provider.GetRequiredService<IOptions<DashboardSettings>>().Value;
        var host = provider.GetRequiredService<IOptions<DashboardHostSettings>>().Value;

        Assert.False(dashboard.Enabled);
        Assert.Equal("http://dashboard.local:5200", dashboard.Url);
        Assert.Equal(5200, host.Port);
        Assert.Equal(["producer", "insurance"], host.GetExpectedServices());
    }

    [Fact]
    public void AddDashboardReporting_Disabled_UsesNullReporterAndSystemTime()
    {
        var services = new ServiceCollection();
        services.AddDashboardConfiguration(Configuration(("Dashboard:Enabled", "false")));
        services.AddDashboardReporting();

        using var provider = services.BuildServiceProvider();

        Assert.Same(NullDashboardReporter.Instance, provider.GetRequiredService<IDashboardReporter>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddContactEventPublishing_RegistersPublisherAndSender()
    {
        var services = new ServiceCollection();

        services.AddContactEventPublishing();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ContactEventPublisher));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IServiceBusSender));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(Azure.Messaging.ServiceBus.ServiceBusClient));
    }

    [Fact]
    public void AddContactEventConsuming_RegistersRunnerAndReceiver()
    {
        var services = new ServiceCollection();

        services.AddContactEventConsuming();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(SubscriptionConsumerRunner));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IServiceBusReceiver));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value))
            .Build();
}
