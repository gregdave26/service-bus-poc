namespace ServiceBusPoc.Core.Dashboard;

/// <summary>
/// Lifecycle state a producer or consumer reports about itself.
/// </summary>
public enum ServiceState
{
    /// <summary>The service is starting and has not yet published or received a message.</summary>
    Starting,

    /// <summary>The service is connected to Service Bus and actively publishing or receiving.</summary>
    Running,

    /// <summary>The service is listening but has not yet received a message (consumer only).</summary>
    Listening,

    /// <summary>The service is online and ready (used by dashboard to indicate last known good state).</summary>
    Online,

    /// <summary>The service has shut down and will send no further heartbeats.</summary>
    Stopped,

    /// <summary>The service has not sent a heartbeat within the timeout window (dashboard-derived state).</summary>
    Offline
}
