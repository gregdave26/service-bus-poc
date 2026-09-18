namespace ServiceBusPoc.Core.Dashboard;

/// <summary>
/// Reports listener heartbeats to the optional monitoring dashboard.
/// Implementations must never propagate dashboard failures to messaging code:
/// the dashboard is an observability side channel, not a dependency of message flow.
/// </summary>
public interface IDashboardReporter
{
    /// <summary>
    /// Sends a heartbeat, logging and absorbing any dashboard failure.
    /// </summary>
    /// <param name="heartbeat">The heartbeat to report.</param>
    /// <param name="cancellationToken">Token used to cancel the report.</param>
    Task ReportAsync(ServiceHeartbeat heartbeat, CancellationToken cancellationToken = default);

    /// <summary>Reports a sent or received message.</summary>
    /// <param name="message">The message observation to report.</param>
    /// <param name="cancellationToken">Token used to cancel the report.</param>
    Task ReportMessageAsync(DashboardMessage message, CancellationToken cancellationToken = default);
}
