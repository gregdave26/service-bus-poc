using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Configuration settings for Carwash API integration.
/// Loaded from environment variables using IOptions&lt;T&gt;.
/// </summary>
public class CarwashSettings
{
    /// <summary>
    /// Gets or sets the base URL of the Carwash API.
    /// Must be loaded from environment variable 'Carwash:ApiUrl'.
    /// </summary>
    [Required(ErrorMessage = "Carwash ApiUrl is required")]
    [Url(ErrorMessage = "Carwash ApiUrl must be a valid URL")]
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use mock mode instead of real HTTP calls.
    /// Loaded from environment variable 'Carwash:MockMode' (default: false).
    /// </summary>
    public bool MockMode { get; set; } = false;

    /// <summary>
    /// Gets or sets the API authentication token or key.
    /// Loaded from environment variable 'Carwash:AuthToken'.
    /// </summary>
    public string? AuthToken { get; set; }
}
