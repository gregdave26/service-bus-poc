using System.ComponentModel.DataAnnotations;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Settings used by the dashboard process itself.
/// Bound from the same <c>Dashboard__*</c> environment variables as <see cref="DashboardSettings"/> (ADR-006).
/// </summary>
public class DashboardHostSettings
{
    private static readonly char[] ServiceSeparators = [',', ';'];

    /// <summary>
    /// Gets or sets the port the dashboard listens on.
    /// Loaded from environment variable 'Dashboard:Port' (default: 5100).
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Dashboard Port must be between 1 and 65535")]
    public int Port { get; set; } = 5100;

    /// <summary>
    /// Gets or sets how long a service may go without a heartbeat before it is shown as offline.
    /// Loaded from environment variable 'Dashboard:OfflineAfterSeconds' (default: 15).
    /// </summary>
    [Range(1, 3600, ErrorMessage = "Dashboard OfflineAfterSeconds must be between 1 and 3600")]
    public int OfflineAfterSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets the comma-separated services the dashboard expects to hear from.
    /// They are listed as <c>unknown</c> until their first heartbeat arrives.
    /// Loaded from environment variable 'Dashboard:ExpectedServices'.
    /// </summary>
    public string ExpectedServices { get; set; } =
        $"producer,{SubscriptionNames.DigitalChannels},{SubscriptionNames.Insurance}," +
        $"{SubscriptionNames.ParksResorts},{SubscriptionNames.Carwash}";

    /// <summary>Gets the offline threshold as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan OfflineAfter => TimeSpan.FromSeconds(OfflineAfterSeconds);

    /// <summary>Gets or sets the hostname/IP the dashboard listens on (default: localhost).</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Gets the dashboard's own base URL.</summary>
    public string Url => $"http://{Host}:{Port}";

    /// <summary>
    /// Splits <see cref="ExpectedServices"/> into individual service names.
    /// </summary>
    public IReadOnlyList<string> GetExpectedServices() =>
        ExpectedServices
            .Split(ServiceSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
