namespace ServiceBusPoc.Dashboard.Api;

/// <summary>
/// API routes for the dashboard HTTP server.
/// </summary>
public static class DashboardRoutes
{
    /// <summary>Endpoint where services report heartbeats.</summary>
    public const string Heartbeat = "/api/heartbeat";

    /// <summary>Endpoint for retrieving current service status.</summary>
    public const string Status = "/api/status";

    /// <summary>Endpoint for publishing events from the dashboard UI.</summary>
    public const string Publish = "/api/publish";
}
