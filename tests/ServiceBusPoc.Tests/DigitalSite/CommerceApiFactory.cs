using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusPoc.DigitalSite.CommerceApi.Checkout;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;

namespace ServiceBusPoc.Tests.DigitalSite;

/// <summary>Hosts the Digital Site commerce API in memory against an in-memory commercetools stub.</summary>
public sealed class CommerceApiFactory : WebApplicationFactory<CheckoutService>
{
    public const string AllowedOrigin = "http://localhost:5100";

    private readonly CommerceToolsStubFactory _commerceTools;
    private readonly IReadOnlyDictionary<string, string?> _settings;
    private readonly Action<IServiceCollection>? _configureServices;

    public CommerceApiFactory(
        CommerceToolsStubFactory commerceTools,
        IReadOnlyDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null)
    {
        _commerceTools = commerceTools;
        _settings = settings ?? new Dictionary<string, string?>();
        _configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("CommerceTools:ApiUrl", "http://localhost");
        builder.UseSetting("CommerceTools:ProjectKey", CommerceToolsStubFactory.ProjectKey);
        builder.UseSetting("DigitalSite:AllowedOrigins", AllowedOrigin);
        builder.UseSetting("Dashboard:Enabled", "false");
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ICommerceToolsClient>(_ => _commerceTools.CreateCommerceClient());
            _configureServices?.Invoke(services);
        });
    }
}
