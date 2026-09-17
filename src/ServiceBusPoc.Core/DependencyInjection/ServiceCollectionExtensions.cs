using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering common services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Service Bus configuration from environment variables.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddServiceBusConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServiceBusSettings>(options =>
            configuration.GetSection("ServiceBus").Bind(options));
        return services;
    }

    /// <summary>
    /// Registers Carwash API configuration from environment variables.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCarwashConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CarwashSettings>(options =>
            configuration.GetSection("Carwash").Bind(options));
        return services;
    }

    /// <summary>
    /// Registers dashboard configuration from environment variables, for both the
    /// reporting clients and the dashboard process itself.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDashboardConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(DashboardSettings.SectionName);
        services.Configure<DashboardSettings>(section.Bind);
        services.Configure<DashboardHostSettings>(section.Bind);
        return services;
    }

    /// <summary>
    /// Registers the dashboard reporter used by producers and consumers.
    /// Reporting is best-effort: a stopped dashboard never interrupts message flow.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDashboardReporting(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDashboardReporter>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<DashboardSettings>>().Value;
            if (!settings.Enabled)
            {
                return NullDashboardReporter.Instance;
            }

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(settings.Url),
                Timeout = settings.RequestTimeout
            };

            return new HttpDashboardReporter(
                httpClient,
                provider.GetRequiredService<ILogger<HttpDashboardReporter>>());
        });

        return services;
    }

    /// <summary>
    /// Registers everything required to publish <c>contact.events</c> messages.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContactEventPublishing(this IServiceCollection services)
    {
        services.AddServiceBusClient();
        services.TryAddSingleton<IServiceBusSender, ServiceBusSenderAdapter>();
        services.TryAddSingleton<ContactEventPublisher>();
        return services;
    }

    /// <summary>
    /// Registers everything required to consume one <c>contact.events</c> subscription.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContactEventConsuming(this IServiceCollection services)
    {
        services.AddServiceBusClient();
        services.TryAddSingleton<IServiceBusReceiver, ServiceBusReceiverAdapter>();
        services.TryAddSingleton<SubscriptionConsumerRunner>();
        return services;
    }

    private static IServiceCollection AddServiceBusClient(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<ServiceBusSettings>>().Value;
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException(
                    "ServiceBus:ConnectionString must be configured (environment variable ServiceBus__ConnectionString).");
            }

            return new ServiceBusClient(settings.ConnectionString);
        });

        return services;
    }
}
