using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services
            .AddOptions<ServiceBusSettings>()
            .Bind(configuration.GetSection("ServiceBus"))
            .ValidateDataAnnotations()
            .Validate(
                settings =>
                    !string.IsNullOrWhiteSpace(settings.ConnectionString)
                    && !string.IsNullOrWhiteSpace(settings.Namespace)
                    && !string.IsNullOrWhiteSpace(settings.TopicName),
                "Service Bus connection string, namespace, and topic name must not be empty.")
            .ValidateOnStart();
        return services;
    }

    /// <summary>
    /// Registers producer input configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProducerConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ProducerSettings>()
            .Bind(configuration.GetSection("Producer"))
            .ValidateDataAnnotations()
            .Validate(
                settings =>
                    !string.IsNullOrWhiteSpace(settings.ContactId)
                    && !string.IsNullOrWhiteSpace(settings.FirstName)
                    && !string.IsNullOrWhiteSpace(settings.LastName)
                    && !string.IsNullOrWhiteSpace(settings.Source),
                "Producer contact ID, first name, last name, and source must not be empty.")
            .ValidateOnStart();
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
