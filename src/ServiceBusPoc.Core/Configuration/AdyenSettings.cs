using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Adyen Checkout settings, used only when <see cref="DigitalSiteSettings.PaymentGateway"/> is
/// <see cref="PaymentGatewayMode.Adyen"/>. Loaded from environment variables prefixed <c>Adyen__</c>;
/// secrets must come from a secret store, never source control.
/// </summary>
public sealed class AdyenSettings
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Adyen";

    /// <summary>Gets or sets the Adyen environment: <c>test</c> or <c>live</c>.</summary>
    [Required]
    [AllowedValues("test", "live", ErrorMessage = "Adyen Environment must be test or live")]
    public string Environment { get; set; } = "test";

    /// <summary>Gets or sets the merchant account that processes payments.</summary>
    public string? MerchantAccount { get; set; }

    /// <summary>Gets or sets the server-side Checkout API key. Not required when APIM injects it.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the client key used by the browser Drop-in.</summary>
    public string? ClientKey { get; set; }

    /// <summary>Gets or sets the hex HMAC key used to verify standard notifications.</summary>
    public string? HmacKey { get; set; }

    /// <summary>
    /// Gets or sets an optional Checkout API base URL without trailing slash, for example an APIM proxy
    /// (<c>https://apim.example/adyen/checkout/v72</c>). When omitted, the Adyen test endpoint is used.
    /// </summary>
    [Url(ErrorMessage = "Adyen CheckoutApiUrl must be a valid URL")]
    public string? CheckoutApiUrl { get; set; }

    /// <summary>Gets or sets the APIM subscription key. When set, calls are routed through APIM.</summary>
    public string? ApimSubscriptionKey { get; set; }

    /// <summary>Gets a value indicating whether calls go through an APIM proxy that injects the Adyen API key.</summary>
    public bool UsesApim => !string.IsNullOrWhiteSpace(ApimSubscriptionKey);
}
