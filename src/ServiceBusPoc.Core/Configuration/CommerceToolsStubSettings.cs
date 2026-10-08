using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Settings for the local commercetools stub. Loaded from environment variables prefixed <c>CommerceToolsStub__</c>.
/// </summary>
public sealed class CommerceToolsStubSettings
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "CommerceToolsStub";

    /// <summary>Gets or sets the SQLite database file path.</summary>
    [Required]
    public string DatabasePath { get; set; } = "data/commercetools-stub.db";

    /// <summary>Gets or sets the only project key the stub serves.</summary>
    [Required]
    public string ProjectKey { get; set; } = "rac-rsa-poc";

    /// <summary>Gets or sets the client identifier accepted by the stub token endpoint. Authentication is disabled when omitted.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the client secret accepted by the stub token endpoint.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets a value indicating whether API calls require a bearer token issued by the stub.</summary>
    public bool RequiresAuthentication =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
