namespace ServiceBusPoc.Core.Dashboard;

/// <summary>
/// Reporter used when dashboard reporting is switched off (<c>Dashboard:Enabled=false</c>).
/// </summary>
public sealed class NullDashboardReporter : IDashboardReporter
{
    /// <summary>Gets the shared instance.</summary>
    public static NullDashboardReporter Instance { get; } = new();

    /// <inheritdoc />
    public Task ReportAsync(ServiceHeartbeat heartbeat, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
