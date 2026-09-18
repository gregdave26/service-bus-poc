namespace ServiceBusPoc.Core.Dashboard;

/// <summary>
/// HTTP routes exposed by the dashboard. Shared so reporters and the dashboard server
/// cannot drift apart.
/// </summary>
public static class DashboardRoutes
{
    /// <summary>Route that accepts <see cref="ServiceHeartbeat"/> reports.</summary>
    public const string Heartbeat = "/api/heartbeat";

    /// <summary>Route that returns the aged status of every known service.</summary>
    public const string Status = "/api/status";

    /// <summary>Route that publishes a <c>contact.updated</c> event on request.</summary>
    public const string Publish = "/api/publish";
}
