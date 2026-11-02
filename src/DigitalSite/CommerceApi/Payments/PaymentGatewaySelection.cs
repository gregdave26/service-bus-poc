using Microsoft.FeatureManagement;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>Resolves the payment gateway once, before registering its services.</summary>
public static class PaymentGatewaySelection
{
    /// <summary>The flag selecting the real Adyen integration.</summary>
    public const string FeatureName = "UseRealAdyen";

    /// <summary>Resolves the flag while retaining legacy payment configuration when it is absent.</summary>
    /// <param name="configuration">The application's environment-backed configuration.</param>
    /// <returns>The gateway to use for this process lifetime.</returns>
    public static async Task<PaymentGatewayMode> ResolveAsync(IConfiguration configuration)
    {
        var legacyMode = configuration.GetValue(
            $"{DigitalSiteSettings.SectionName}:{nameof(DigitalSiteSettings.PaymentGateway)}",
            PaymentGatewayMode.Stub);
        if (!Enum.IsDefined(legacyMode))
        {
            throw new InvalidOperationException("DigitalSite:PaymentGateway must be Stub, Adyen or AdyenComponent.");
        }

        var flag = configuration.GetSection($"FeatureManagement:{FeatureName}");
        if (!flag.Exists())
        {
            return legacyMode;
        }

        if (flag.GetChildren().Any() || !bool.TryParse(flag.Value, out _))
        {
            throw new InvalidOperationException(
                "FeatureManagement:UseRealAdyen must be true or false. Filters and targeting are not supported for startup payment selection.");
        }

        // Standalone evaluation avoids building a temporary DI container before gateway registration.
        using var definitions = new ConfigurationFeatureDefinitionProvider(configuration);
        var features = new FeatureManager(definitions);
        var useRealAdyen = await features.IsEnabledAsync(FeatureName);
        return useRealAdyen
            ? legacyMode == PaymentGatewayMode.AdyenComponent ? PaymentGatewayMode.AdyenComponent : PaymentGatewayMode.Adyen
            : PaymentGatewayMode.Stub;
    }
}
