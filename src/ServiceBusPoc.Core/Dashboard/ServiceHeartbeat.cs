namespace ServiceBusPoc.Core.Dashboard;

/// <summary>
/// Liveness signal a producer or consumer sends to the dashboard.
/// The dashboard ages heartbeats and treats a service as offline when they stop arriving,
/// so a single heartbeat never marks a service permanently online.
/// </summary>
public sealed record ServiceHeartbeat
{
    /// <summary>Gets the stable service identifier, for example <c>insurance</c>.</summary>
    public required string ServiceName { get; init; }

    /// <summary>Gets the Service Bus subscription the service reads from, when it is a consumer.</summary>
    public string? SubscriptionName { get; init; }

    /// <summary>Gets the lifecycle state reported by the service.</summary>
    public required ServiceState State { get; init; }

    /// <summary>Gets the UTC instant the heartbeat was created by the service.</summary>
    public required DateTimeOffset SentAt { get; init; }

    /// <summary>Gets the number of messages the service has published or received since it started.</summary>
    public long MessagesHandled { get; init; }

    /// <summary>Gets the UTC instant of the most recent message, when any was handled.</summary>
    public DateTimeOffset? LastMessageAt { get; init; }

    /// <summary>Gets the identifier of the most recent event, when any was handled.</summary>
    public string? LastEventId { get; init; }
}
