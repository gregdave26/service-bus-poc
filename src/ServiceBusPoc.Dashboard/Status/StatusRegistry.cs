using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;

namespace ServiceBusPoc.Dashboard.Status;

/// <summary>
/// Tracks service heartbeats and ages them to detect when services become offline.
/// A single heartbeat never marks a service permanently alive; status is derived from
/// the time elapsed since the last heartbeat was received.
/// </summary>
public sealed class StatusRegistry
{
    private const int HeartbeatTimeoutSeconds = 15;
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(HeartbeatTimeoutSeconds);

    private readonly ConcurrentDictionary<string, ServiceHeartbeat> _heartbeats = new();
    private readonly ILogger<StatusRegistry> _logger;
    private readonly TimeProvider _timeProvider;

    public StatusRegistry(ILogger<StatusRegistry> logger)
    {
        _logger = logger;
        _timeProvider = TimeProvider.System;
    }

    /// <summary>
    /// Records a heartbeat from a service.
    /// </summary>
    public void RecordHeartbeat(ServiceHeartbeat heartbeat)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        _heartbeats[heartbeat.ServiceName] = heartbeat;
        _logger.LogDebug(
            "Recorded heartbeat from {ServiceName} ({State}): {MessagesHandled} messages",
            heartbeat.ServiceName,
            heartbeat.State,
            heartbeat.MessagesHandled);
    }

    /// <summary>
    /// Gets the current status of all known services, aging heartbeats to offline when timeout exceeded.
    /// </summary>
    public IEnumerable<ServiceStatus> GetServiceStatuses()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var (serviceName, heartbeat) in _heartbeats)
        {
            var isOnline = now - heartbeat.SentAt < HeartbeatTimeout;
            var state = isOnline ? heartbeat.State : ServiceState.Offline;

            yield return new ServiceStatus
            {
                ServiceName = serviceName,
                SubscriptionName = heartbeat.SubscriptionName,
                State = state,
                LastHeartbeatAt = heartbeat.SentAt,
                MessagesHandled = heartbeat.MessagesHandled,
                LastMessageAt = heartbeat.LastMessageAt,
                LastEventId = heartbeat.LastEventId
            };
        }
    }
}

/// <summary>
/// Current status of a service as derived from aged heartbeats.
/// </summary>
public sealed record ServiceStatus
{
    public required string ServiceName { get; init; }
    public string? SubscriptionName { get; init; }
    public required ServiceState State { get; init; }
    public required DateTimeOffset LastHeartbeatAt { get; init; }
    public required long MessagesHandled { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }
    public string? LastEventId { get; init; }
}
