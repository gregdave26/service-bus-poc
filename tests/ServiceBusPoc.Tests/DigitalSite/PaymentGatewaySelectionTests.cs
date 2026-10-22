using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.DependencyInjection;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class PaymentGatewaySelectionTests
{
    [Theory]
    [InlineData("false", "Adyen", PaymentGatewayMode.Stub)]
    [InlineData("false", "AdyenComponent", PaymentGatewayMode.Stub)]
    [InlineData("true", "Stub", PaymentGatewayMode.Adyen)]
    [InlineData("true", "AdyenComponent", PaymentGatewayMode.AdyenComponent)]
    [InlineData(null, "Stub", PaymentGatewayMode.Stub)]
    public async Task AddCommerceApiAsync_UsesSameSnapshotForOptionsRuntimeStateAndAdapter(
        string? flag, string mode, PaymentGatewayMode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DigitalSite:PaymentGateway"] = mode,
            ["FeatureManagement:UseRealAdyen"] = flag,
            ["CommerceTools:ApiUrl"] = "http://localhost:5201",
            ["CommerceTools:ProjectKey"] = "project",
            ["Adyen:MerchantAccount"] = "merchant",
            ["Adyen:ApiKey"] = "test-key",
            ["Adyen:ClientKey"] = "client",
            ["Adyen:HmacKey"] = new string('A', 64),
        });
        var selected = await builder.AddCommerceApiAsync();
        await using var app = builder.Build();
        builder.Configuration["FeatureManagement:UseRealAdyen"] = flag == "false" ? "true" : "false";
        await using var scope = app.Services.CreateAsyncScope();

        Assert.Equal(expected, selected);
        Assert.Equal(expected, scope.ServiceProvider.GetRequiredService<IOptions<DigitalSiteSettings>>().Value.PaymentGateway);
        Assert.Equal(expected, scope.ServiceProvider.GetRequiredService<PaymentGatewayRuntimeState>().ConfiguredMode);
        Assert.Equal(expected, scope.ServiceProvider.GetRequiredService<IPaymentGatewayAdapter>().Mode);
    }

    [Theory]
    [InlineData(null, null, PaymentGatewayMode.Stub)]
    [InlineData(null, "Stub", PaymentGatewayMode.Stub)]
    [InlineData(null, "Adyen", PaymentGatewayMode.Adyen)]
    [InlineData(null, "AdyenComponent", PaymentGatewayMode.AdyenComponent)]
    [InlineData("false", null, PaymentGatewayMode.Stub)]
    [InlineData("false", "Adyen", PaymentGatewayMode.Stub)]
    [InlineData("false", "AdyenComponent", PaymentGatewayMode.Stub)]
    [InlineData("true", null, PaymentGatewayMode.Adyen)]
    [InlineData("true", "Stub", PaymentGatewayMode.Adyen)]
    [InlineData("true", "Adyen", PaymentGatewayMode.Adyen)]
    [InlineData("true", "AdyenComponent", PaymentGatewayMode.AdyenComponent)]
    public async Task ResolveAsync_PreservesModesAndAppliesExplicitFlag(
        string? flag, string? legacyMode, PaymentGatewayMode expected)
    {
        using var configuration = BuildConfiguration(flag, legacyMode);

        Assert.Equal(expected, await PaymentGatewaySelection.ResolveAsync(configuration));
    }

    [Theory]
    [InlineData("")]
    [InlineData("yes")]
    [InlineData("1")]
    public async Task ResolveAsync_InvalidFlag_FailsInsteadOfSelectingStub(string flag)
    {
        using var configuration = BuildConfiguration(flag, "Adyen");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PaymentGatewaySelection.ResolveAsync(configuration));

        Assert.Contains("must be true or false", exception.Message);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("Unknown")]
    public async Task ResolveAsync_InvalidLegacyMode_FailsEvenWhenFlagIsFalse(string mode)
    {
        using var configuration = BuildConfiguration("false", mode);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PaymentGatewaySelection.ResolveAsync(configuration));
    }

    [Fact]
    public async Task ResolveAsync_ConditionalFlag_RejectsPerRequestTargeting()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FeatureManagement:UseRealAdyen:EnabledFor:0:Name"] = "AlwaysOn",
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PaymentGatewaySelection.ResolveAsync(configuration));
    }

    [Fact]
    public async Task ResolveAsync_EnvironmentProviderBindsDocumentedKey_AndResultIsASnapshot()
    {
        var prefix = $"PaymentFlagTest_{Guid.NewGuid():N}_";
        var key = $"{prefix}FeatureManagement__UseRealAdyen";
        try
        {
            Environment.SetEnvironmentVariable(key, "true");
            using var configuration = new ConfigurationManager();
            configuration.AddEnvironmentVariables(prefix);
            var selected = await PaymentGatewaySelection.ResolveAsync(configuration);
            Environment.SetEnvironmentVariable(key, "false");
            ((IConfigurationRoot)configuration).Reload();

            Assert.Equal(PaymentGatewayMode.Adyen, selected);
            Assert.Equal(PaymentGatewayMode.Stub, await PaymentGatewaySelection.ResolveAsync(configuration));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    private static ConfigurationManager BuildConfiguration(string? flag, string? mode)
    {
        var settings = new Dictionary<string, string?>();
        if (flag is not null) settings["FeatureManagement:UseRealAdyen"] = flag;
        if (mode is not null) settings["DigitalSite:PaymentGateway"] = mode;
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(settings);
        return configuration;
    }
}
