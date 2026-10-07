using Adyen.Checkout.Extensions;
using Adyen.Core.Options;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.Carts;
using ServiceBusPoc.DigitalSite.CommerceApi.Catalog;
using ServiceBusPoc.DigitalSite.CommerceApi.Checkout;
using ServiceBusPoc.DigitalSite.CommerceApi.Notifications;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.CommerceApi.Vehicles;
using ServiceBusPoc.DigitalSite.Shared.DependencyInjection;

namespace ServiceBusPoc.DigitalSite.CommerceApi.DependencyInjection;

/// <summary>DI registrations for the Digital Site commerce API.</summary>
public static class CommerceApiServiceCollectionExtensions
{
    /// <summary>The CORS policy for the dashboard origins.</summary>
    public const string CorsPolicy = "DigitalSite";

    /// <summary>Registers the commerce API services and the configured payment gateway.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The configured payment gateway mode.</returns>
    public static PaymentGatewayMode AddCommerceApi(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        services
            .AddOptions<DigitalSiteSettings>()
            .Bind(configuration.GetSection(DigitalSiteSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddCommerceToolsClient(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_ => SiteContent.Load(Path.Combine(AppContext.BaseDirectory, SiteContent.FileName)));
        services.AddSingleton<VehicleRegister>();
        services.AddScoped<CatalogService>();
        services.AddScoped<MemberCartService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<NotificationProcessor>();
        services.AddScoped<AdyenNotificationModule>();

        var siteSettings = configuration.GetSection(DigitalSiteSettings.SectionName).Get<DigitalSiteSettings>() ?? new DigitalSiteSettings();
        services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins([.. siteSettings.AllowedOriginList])
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Content-Type")));

        if (siteSettings.PaymentGateway == PaymentGatewayMode.Adyen)
        {
            builder.AddAdyenGateway();
        }
        else
        {
            services.AddSingleton<StubPaymentGateway>();
            services.AddSingleton<IPaymentGatewayAdapter>(provider => provider.GetRequiredService<StubPaymentGateway>());
            services.AddScoped<StubPaymentSimulator>();
        }

        return siteSettings.PaymentGateway;
    }

    private static void AddAdyenGateway(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AdyenSettings>()
            .Bind(builder.Configuration.GetSection(AdyenSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(HasRequiredAdyenSettings, "Adyen MerchantAccount, ClientKey, HmacKey, and either ApiKey or ApimSubscriptionKey with CheckoutApiUrl, are required when PaymentGateway is Adyen")
            .ValidateOnStart();

        var adyen = builder.Configuration.GetSection(AdyenSettings.SectionName).Get<AdyenSettings>() ?? new AdyenSettings();
        builder.Host.ConfigureCheckout((_, services, checkout) =>
        {
            checkout.ConfigureAdyenOptions(options =>
            {
                options.Environment = adyen.Environment == "live" ? AdyenEnvironment.Live : AdyenEnvironment.Test;
                options.AdyenApiKey = adyen.UsesApim ? null : adyen.ApiKey;
            });
            services.AddPaymentsService(
                httpClientOptions: client =>
                {
                    if (!string.IsNullOrWhiteSpace(adyen.CheckoutApiUrl))
                    {
                        client.BaseAddress = new Uri(adyen.CheckoutApiUrl.TrimEnd('/') + "/");
                    }
                },
                httpClientBuilderOptions: clientBuilder =>
                {
                    if (adyen.UsesApim)
                    {
                        clientBuilder.AddHttpMessageHandler(() => new ApimSubscriptionHandler(adyen.ApimSubscriptionKey!));
                    }
                });
        });
        builder.Services.AddScoped<IPaymentGatewayAdapter, AdyenPaymentAdapter>();
    }

    private static bool HasRequiredAdyenSettings(AdyenSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.MerchantAccount)
        && !string.IsNullOrWhiteSpace(settings.ClientKey)
        && !string.IsNullOrWhiteSpace(settings.HmacKey)
        && (settings.UsesApim
            ? !string.IsNullOrWhiteSpace(settings.CheckoutApiUrl)
            : !string.IsNullOrWhiteSpace(settings.ApiKey));
}
