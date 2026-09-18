using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.Core.Dashboard;

/// <summary>
/// Reports heartbeats to the dashboard over HTTP.
/// The dashboard is an observability side channel: every transport failure is logged and absorbed
/// so that messaging keeps working when the dashboard is stopped, restarted, or never started.
/// Repeated failures are logged once per availability change to keep consumer logs readable.
/// </summary>
public sealed class HttpDashboardReporter : IDashboardReporter, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpDashboardReporter> _logger;
    private bool _dashboardWasReachable = true;

    /// <summary>
    /// Creates a reporter that takes ownership of <paramref name="httpClient"/>.
    /// </summary>
    /// <param name="httpClient">Client whose base address points at the dashboard.</param>
    /// <param name="logger">Logger for dashboard availability changes.</param>
    public HttpDashboardReporter(HttpClient httpClient, ILogger<HttpDashboardReporter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ReportAsync(ServiceHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                DashboardRoutes.Heartbeat,
                heartbeat,
                JsonSerializerOptionsHelper.DefaultOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                RecordReachable(heartbeat);
                return;
            }

            RecordUnreachable(heartbeat, $"HTTP {(int)response.StatusCode}", exception: null);
        }
        catch (HttpRequestException ex)
        {
            RecordUnreachable(heartbeat, "the dashboard is not reachable", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            RecordUnreachable(heartbeat, "the dashboard did not respond in time", ex);
        }
    }

    /// <inheritdoc />
    public async Task ReportMessageAsync(DashboardMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                DashboardRoutes.Message,
                message,
                JsonSerializerOptionsHelper.DefaultOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                RecordUnreachable(message.ServiceName, $"HTTP {(int)response.StatusCode}", exception: null);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            RecordUnreachable(message.ServiceName, "the dashboard message report was not delivered", ex);
        }
    }

    private void RecordReachable(ServiceHeartbeat heartbeat)
    {
        if (_dashboardWasReachable)
        {
            return;
        }

        _dashboardWasReachable = true;
        _logger.LogInformation(
            "Dashboard reporting resumed for service {ServiceName}",
            heartbeat.ServiceName);
    }

    private void RecordUnreachable(ServiceHeartbeat heartbeat, string reason, Exception? exception)
    {
        if (!_dashboardWasReachable)
        {
            return;
        }

        _dashboardWasReachable = false;
        _logger.LogWarning(
            exception,
            "Heartbeat for service {ServiceName} was not delivered because {Reason}; messaging continues unaffected",
            heartbeat.ServiceName,
            reason);
    }

    private void RecordUnreachable(string serviceName, string reason, Exception? exception)
    {
        if (!_dashboardWasReachable)
        {
            return;
        }

        _dashboardWasReachable = false;
        _logger.LogWarning(
            exception,
            "Dashboard report for service {ServiceName} was not delivered because {Reason}; messaging continues unaffected",
            serviceName,
            reason);
    }

    /// <inheritdoc />
    public void Dispose() => _httpClient.Dispose();
}
