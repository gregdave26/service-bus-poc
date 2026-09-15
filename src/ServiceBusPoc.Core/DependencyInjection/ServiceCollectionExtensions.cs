using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ServiceBusPoc.Core.Configuration;

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
}
