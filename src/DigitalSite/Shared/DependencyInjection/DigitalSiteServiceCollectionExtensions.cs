using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.Dashboard;
using ServiceBusPoc.DigitalSite.Shared.Messaging;

namespace ServiceBusPoc.DigitalSite.Shared.DependencyInjection;

/// <summary>DI registrations shared by the Digital Site services.</summary>
public static class DigitalSiteServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ICommerceToolsClient"/> and its settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCommerceToolsClient(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CommerceToolsSettings>()
            .Bind(configuration.GetSection(CommerceToolsSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(CommerceToolsAuthHandler.TokenClientName);
        services.AddTransient<CommerceToolsAuthHandler>();

        var builder = services.AddHttpClient<ICommerceToolsClient, CommerceToolsClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<CommerceToolsSettings>>().Value;
            client.BaseAddress = new Uri($"{settings.ApiUrl!.TrimEnd('/')}/{settings.ProjectKey}/");
        });

        var configured = configuration.GetSection(CommerceToolsSettings.SectionName).Get<CommerceToolsSettings>();
        if (configured?.UsesAuthentication == true)
        {
            builder.AddHttpMessageHandler<CommerceToolsAuthHandler>();
        }

        return services;
    }

    /// <summary>
    /// Registers a <see cref="CommerceSubscriptionRunner"/> that passes <c>commerce.events</c> messages to <typeparamref name="THandler"/>.
    /// </summary>
    /// <typeparam name="THandler">The message handler.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCommerceMessageConsuming<THandler>(this IServiceCollection services)
        where THandler : class, ICommerceMessageHandler
    {
        services.AddServiceBusClient();
        services.TryAddSingleton<IServiceBusReceiver, ServiceBusReceiverAdapter>();
        services.TryAddSingleton<ICommerceMessageHandler, THandler>();
        services.TryAddSingleton<CommerceSubscriptionRunner>();
        return services;
    }

    /// <summary>Registers dashboard heartbeats for a web service.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">The dashboard service name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDashboardHeartbeat(this IServiceCollection services, string serviceName)
    {
        services.AddDashboardReporting();
        services.TryAddSingleton<IServiceActivity>(provider => new ServiceActivity(serviceName, provider.GetRequiredService<TimeProvider>()));
        services.AddHostedService<DashboardHeartbeatService>();
        return services;
    }
}
