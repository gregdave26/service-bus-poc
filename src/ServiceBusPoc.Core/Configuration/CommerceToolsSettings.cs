using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Connection settings for the commercetools HTTP API, or the local commercetools stub.
/// Loaded from environment variables prefixed <c>CommerceTools__</c>.
/// </summary>
public sealed class CommerceToolsSettings
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "CommerceTools";

    /// <summary>Gets or sets the API base URL, for example <c>https://api.australia-southeast1.gcp.commercetools.com</c>.</summary>
    [Required(ErrorMessage = "CommerceTools ApiUrl is required")]
    [Url(ErrorMessage = "CommerceTools ApiUrl must be a valid URL")]
    public string? ApiUrl { get; set; }

    /// <summary>Gets or sets the project key used as the first path segment of every API call.</summary>
    [Required(ErrorMessage = "CommerceTools ProjectKey is required")]
    public string? ProjectKey { get; set; }

    /// <summary>Gets or sets the OAuth base URL. When omitted, requests are sent without a bearer token.</summary>
    [Url(ErrorMessage = "CommerceTools AuthUrl must be a valid URL")]
    public string? AuthUrl { get; set; }

    /// <summary>Gets or sets the API client identifier used for the client-credentials grant.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the API client secret used for the client-credentials grant.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets or sets the optional space-separated scopes requested with the token.</summary>
    public string? Scope { get; set; }

    /// <summary>Gets a value indicating whether OAuth client credentials are configured.</summary>
    public bool UsesAuthentication =>
        !string.IsNullOrWhiteSpace(AuthUrl)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);
}
