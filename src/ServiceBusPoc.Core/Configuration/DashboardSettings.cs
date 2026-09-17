using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Settings used by producers and consumers to report heartbeats to the optional dashboard.
/// Loaded from environment variables using IOptions&lt;T&gt; (ADR-006).
/// </summary>
public class DashboardSettings
{
    /// <summary>The configuration section bound from <c>Dashboard__*</c> environment variables.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>
    /// Gets or sets a value indicating whether heartbeats are reported at all.
    /// Loaded from environment variable 'Dashboard:Enabled' (default: true).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the base URL of the dashboard.
    /// Loaded from environment variable 'Dashboard:Url'.
    /// </summary>
    [Required(ErrorMessage = "Dashboard Url is required")]
    [Url(ErrorMessage = "Dashboard Url must be a valid URL")]
    public string Url { get; set; } = "http://localhost:5100";

    /// <summary>
    /// Gets or sets how often a service reports that it is still alive.
    /// Loaded from environment variable 'Dashboard:HeartbeatIntervalSeconds' (default: 5).
    /// </summary>
    [Range(1, 300, ErrorMessage = "Dashboard HeartbeatIntervalSeconds must be between 1 and 300")]
    public int HeartbeatIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets how long a heartbeat request may take before it is abandoned.
    /// Loaded from environment variable 'Dashboard:RequestTimeoutSeconds' (default: 3).
    /// </summary>
    [Range(1, 60, ErrorMessage = "Dashboard RequestTimeoutSeconds must be between 1 and 60")]
    public int RequestTimeoutSeconds { get; set; } = 3;

    /// <summary>Gets the heartbeat interval as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);

    /// <summary>Gets the request timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan RequestTimeout => TimeSpan.FromSeconds(RequestTimeoutSeconds);
}
