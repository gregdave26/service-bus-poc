using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Settings for the Digital Site commerce API. Loaded from environment variables prefixed <c>DigitalSite__</c>.
/// </summary>
public sealed class DigitalSiteSettings
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "DigitalSite";

    /// <summary>Gets or sets the payment gateway feature toggle.</summary>
    public PaymentGatewayMode PaymentGateway { get; set; } = PaymentGatewayMode.Stub;

    /// <summary>
    /// Gets or sets the comma-separated browser origins allowed to call the API and to receive payment redirects.
    /// </summary>
    [Required(ErrorMessage = "DigitalSite AllowedOrigins is required")]
    public string AllowedOrigins { get; set; } = "http://localhost:5100";

    /// <summary>Gets or sets the commercetools product type key shared by Roadside Assistance products.</summary>
    [Required]
    public string ProductTypeKey { get; set; } = "roadside-assistance";

    /// <summary>Gets or sets the commercetools shipping method key used for digital products.</summary>
    [Required]
    public string ShippingMethodKey { get; set; } = "digital";

    /// <summary>Gets or sets the ISO 4217 currency for carts and payments.</summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "AUD";

    /// <summary>Gets or sets the ISO 3166 country for carts and payment sessions.</summary>
    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string Country { get; set; } = "AU";

    /// <summary>Gets or sets the shopper locale passed to the payment session.</summary>
    [Required]
    public string ShopperLocale { get; set; } = "en-AU";

    /// <summary>Gets the parsed allowed origins without trailing slashes.</summary>
    public IReadOnlyList<string> AllowedOriginList =>
        AllowedOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin.TrimEnd('/'))
            .ToArray();
}
