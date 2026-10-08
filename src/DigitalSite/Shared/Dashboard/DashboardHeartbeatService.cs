using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;

namespace ServiceBusPoc.DigitalSite.Shared.Dashboard;

/// <summary>
/// Reports periodic heartbeats for a web service so it appears on the dashboard.
/// </summary>
public sealed class DashboardHeartbeatService : BackgroundService
{
    private readonly IDashboardReporter _reporter;
    private readonly IServiceActivity _activity;
    private readonly DashboardSettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="DashboardHeartbeatService"/> class.</summary>
    public DashboardHeartbeatService(
        IDashboardReporter reporter,
        IServiceActivity activity,
        IOptions<DashboardSettings> settings,
        TimeProvider timeProvider)
    {
        _reporter = reporter;
        _activity = activity;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ReportAsync(ServiceState.Online, stoppingToken);
                await Task.Delay(_settings.HeartbeatInterval, _timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await ReportAsync(ServiceState.Stopped, CancellationToken.None);
        }
    }

    private Task ReportAsync(ServiceState state, CancellationToken cancellationToken) =>
        _reporter.ReportAsync(
            new ServiceHeartbeat
            {
                ServiceName = _activity.ServiceName,
                State = state,
                SentAt = _timeProvider.GetUtcNow(),
                MessagesHandled = _activity.MessagesHandled,
                LastMessageAt = _activity.LastMessageAt,
                LastEventId = _activity.LastEventId
            },
            cancellationToken);
}
