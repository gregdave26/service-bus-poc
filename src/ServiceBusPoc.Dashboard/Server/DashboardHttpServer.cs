using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.Dashboard.Api;
using ServiceBusPoc.Dashboard.Status;

namespace ServiceBusPoc.Dashboard.Server;

/// <summary>
/// Lightweight HTTP server for the dashboard using HttpListener.
/// Provides endpoints for receiving heartbeats, retrieving status, and publishing events.
/// Follows the pattern established by CarwashApiServer.
/// </summary>
public sealed class DashboardHttpServer : IDisposable
{
    private readonly HttpListener _httpListener;
    private readonly DashboardHostSettings _settings;
    private readonly StatusRegistry _statusRegistry;
    private readonly PublishEventHandler _publishHandler;
    private readonly ILogger<DashboardHttpServer> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    public DashboardHttpServer(
        IOptions<DashboardHostSettings> settings,
        StatusRegistry statusRegistry,
        PublishEventHandler publishHandler,
        ILogger<DashboardHttpServer> logger)
    {
        _settings = settings.Value;
        _statusRegistry = statusRegistry;
        _publishHandler = publishHandler;
        _logger = logger;
        _httpListener = new HttpListener();
    }

    /// <summary>
    /// Starts the HTTP server and runs until cancellation is requested.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var baseUrl = $"http://{_settings.Host}:{_settings.Port}/";
        _httpListener.Prefixes.Add(baseUrl);

        try
        {
            _httpListener.Start();
            _logger.LogInformation("Dashboard server listening on {BaseUrl}", baseUrl);

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await HandleRequestsAsync(_cancellationTokenSource.Token);
        }
        catch (HttpListenerException ex)
        {
            _logger.LogError(ex, "Failed to start dashboard HTTP server on {BaseUrl}", baseUrl);
            throw;
        }
        finally
        {
            _httpListener.Stop();
            _httpListener.Close();
            _logger.LogInformation("Dashboard server stopped");
        }
    }

    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _httpListener.GetContextAsync().WaitAsync(cancellationToken);
                var request = context.Request;
                var response = context.Response;

                _logger.LogDebug("Received {Method} {Path}", request.HttpMethod, request.Url?.PathAndQuery);

                if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/api/heartbeat")
                {
                    await HandleHeartbeatAsync(request, response);
                }
                else if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/api/status")
                {
                    await HandleStatusAsync(response);
                }
                else if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/api/publish")
                {
                    await HandlePublishAsync(request, response);
                }
                else if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/")
                {
                    await HandleRootAsync(response);
                }
                else
                {
                    SendResponse(response, 404, "Not found");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling HTTP request");
                try
                {
                    context?.Response.StatusCode = 500;
                    context?.Response.Close();
                }
                catch { }
            }
        }
    }

    private async Task HandleHeartbeatAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            var heartbeat = JsonSerializer.Deserialize<ServiceHeartbeat>(json, JsonSerializerOptionsHelper.DefaultOptions);

            if (heartbeat != null)
            {
                _statusRegistry.RecordHeartbeat(heartbeat);
                SendResponse(response, 200, "OK");
            }
            else
            {
                SendResponse(response, 400, "Invalid heartbeat");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process heartbeat");
            SendResponse(response, 400, "Invalid heartbeat");
        }
    }

    private async Task HandleStatusAsync(HttpListenerResponse response)
    {
        var statuses = _statusRegistry.GetServiceStatuses().ToList();
        var json = JsonSerializer.Serialize(statuses, JsonSerializerOptionsHelper.DefaultOptions);

        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.StatusCode = (int)HttpStatusCode.OK;

        await using var output = response.OutputStream;
        await output.WriteAsync(Encoding.UTF8.GetBytes(json));
    }

    private async Task HandlePublishAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            var publishRequest = JsonSerializer.Deserialize<PublishEventRequest>(json, JsonSerializerOptionsHelper.DefaultOptions);

            if (publishRequest == null)
            {
                SendResponse(response, 400, "Invalid publish request");
                return;
            }

            var result = await _publishHandler.PublishEventAsync(publishRequest);
            var responseJson = JsonSerializer.Serialize(result, JsonSerializerOptionsHelper.DefaultOptions);

            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.StatusCode = (int)HttpStatusCode.OK;

            await using var output = response.OutputStream;
            await output.WriteAsync(Encoding.UTF8.GetBytes(responseJson));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event");
            SendResponse(response, 400, "Failed to publish event");
        }
    }

    private async Task HandleRootAsync(HttpListenerResponse response)
    {
        var htmlContent = GetDashboardHtml();
        response.ContentType = "text/html";
        response.ContentEncoding = Encoding.UTF8;
        response.StatusCode = (int)HttpStatusCode.OK;

        await using var output = response.OutputStream;
        await output.WriteAsync(Encoding.UTF8.GetBytes(htmlContent));
    }

    private static void SendResponse(HttpListenerResponse response, int statusCode, string body)
    {
        response.ContentType = "text/plain";
        response.ContentEncoding = Encoding.UTF8;
        response.StatusCode = statusCode;

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        response.ContentLength64 = bodyBytes.Length;

        response.OutputStream.Write(bodyBytes, 0, bodyBytes.Length);
        response.Close();
    }

    private static string GetDashboardHtml()
    {
        return """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Service Bus Dashboard</title>
                <style>
                    * { margin: 0; padding: 0; box-sizing: border-box; }
                    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: #f5f5f5; padding: 20px; }
                    .container { max-width: 1200px; margin: 0 auto; }
                    h1 { color: #333; margin-bottom: 30px; }
                    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 30px; }
                    .card { background: white; border-radius: 8px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
                    .section-title { font-size: 18px; font-weight: 600; margin-bottom: 15px; color: #333; }
                    .service-row { padding: 12px; border-bottom: 1px solid #eee; display: flex; justify-content: space-between; align-items: center; }
                    .service-row:last-child { border-bottom: none; }
                    .service-name { font-weight: 500; color: #333; }
                    .status-badge { padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; }
                    .status-online { background: #d4edda; color: #155724; }
                    .status-listening { background: #cfe2ff; color: #084298; }
                    .status-offline { background: #f8d7da; color: #842029; }
                    .form-group { margin-bottom: 15px; }
                    label { display: block; margin-bottom: 5px; font-weight: 500; color: #333; }
                    input, select { width: 100%; padding: 8px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px; }
                    button { background: #007bff; color: white; padding: 10px 20px; border: none; border-radius: 4px; font-size: 14px; font-weight: 600; cursor: pointer; }
                    button:hover { background: #0056b3; }
                    .error { background: #f8d7da; color: #842029; padding: 12px; border-radius: 4px; margin-bottom: 15px; display: none; }
                    .success { background: #d4edda; color: #155724; padding: 12px; border-radius: 4px; margin-bottom: 15px; display: none; }
                    .message-count { color: #666; font-size: 12px; }
                    .checkbox-group { display: flex; gap: 20px; align-items: center; }
                    .checkbox-group label { margin-bottom: 0; }
                    .checkbox-group input[type="checkbox"] { width: auto; }
                </style>
            </head>
            <body>
                <div class="container">
                    <h1>🚌 Service Bus Dashboard</h1>
                    
                    <div class="grid">
                        <div class="card">
                            <div class="section-title">Service Status</div>
                            <div id="services-container"></div>
                        </div>
                        
                        <div class="card">
                            <div class="section-title">Publish Event</div>
                            <div class="error" id="error-message"></div>
                            <div class="success" id="success-message"></div>
                            <form id="publish-form">
                                <div class="form-group">
                                    <label for="contact-id">Contact ID</label>
                                    <input type="text" id="contact-id" value="contact-123" required>
                                </div>
                                <div class="form-group">
                                    <label for="rac-id">RAC ID</label>
                                    <input type="text" id="rac-id" value="RAC-001" required>
                                </div>
                                <div class="form-group">
                                    <label for="first-name">First Name *</label>
                                    <input type="text" id="first-name" value="John" required>
                                </div>
                                <div class="form-group">
                                    <label for="last-name">Last Name *</label>
                                    <input type="text" id="last-name" value="Doe" required>
                                </div>
                                <div class="form-group">
                                    <label for="phone">Phone</label>
                                    <input type="text" id="phone" value="+1-555-0100" required>
                                </div>
                                <div class="form-group">
                                    <label for="email">Email</label>
                                    <input type="email" id="email" value="contact@example.com" required>
                                </div>
                                <div class="form-group">
                                    <label>Attributes</label>
                                    <div class="checkbox-group">
                                        <div>
                                            <input type="checkbox" id="has-insurance" name="hasInsurance">
                                            <label for="has-insurance">Has Insurance</label>
                                        </div>
                                        <div>
                                            <input type="checkbox" id="has-parks" name="hasParksResorts">
                                            <label for="has-parks">Has Parks/Resorts</label>
                                        </div>
                                        <div>
                                            <input type="checkbox" id="has-carwash" name="hasCarwashProduct">
                                            <label for="has-carwash">Has Carwash</label>
                                        </div>
                                    </div>
                                </div>
                                <button type="submit">Publish Event</button>
                            </form>
                        </div>
                    </div>
                </div>

                <script>
                    const STATUS_API = '/api/status';
                    const PUBLISH_API = '/api/publish';
                    const POLL_INTERVAL = 2000;

                    function getStateColor(state) {
                        const stateMap = {
                            'online': 'status-online',
                            'listening': 'status-listening',
                            'offline': 'status-offline',
                            'starting': 'status-listening'
                        };
                        return stateMap[state.toLowerCase()] || 'status-offline';
                    }

                    function formatTime(isoString) {
                        if (!isoString) return 'Never';
                        const date = new Date(isoString);
                        return date.toLocaleTimeString();
                    }

                    async function updateStatus() {
                        try {
                            const response = await fetch(STATUS_API);
                            const services = await response.json();
                            const container = document.getElementById('services-container');
                            
                            if (services.length === 0) {
                                container.innerHTML = '<div class="service-row" style="justify-content: center; color: #999;">No services connected</div>';
                                return;
                            }

                            container.innerHTML = services.map(svc => `
                                <div class="service-row">
                                    <div>
                                        <div class="service-name">${svc.serviceName}</div>
                                        ${svc.subscriptionName ? `<div style="font-size: 12px; color: #666;">Sub: ${svc.subscriptionName}</div>` : ''}
                                        <div class="message-count">${svc.messagesHandled} messages</div>
                                    </div>
                                    <span class="status-badge ${getStateColor(svc.state)}">${svc.state.toUpperCase()}</span>
                                </div>
                            `).join('');
                        } catch (error) {
                            console.error('Failed to fetch status:', error);
                        }
                    }

                    document.getElementById('publish-form').addEventListener('submit', async (e) => {
                        e.preventDefault();
                        
                        const errorDiv = document.getElementById('error-message');
                        const successDiv = document.getElementById('success-message');
                        errorDiv.style.display = 'none';
                        successDiv.style.display = 'none';

                        const request = {
                            contactId: document.getElementById('contact-id').value,
                            firstName: document.getElementById('first-name').value,
                            lastName: document.getElementById('last-name').value,
                            phone: document.getElementById('phone').value,
                            email: document.getElementById('email').value,
                            racId: document.getElementById('rac-id').value,
                            hasInsurance: document.getElementById('has-insurance').checked,
                            hasParksResorts: document.getElementById('has-parks').checked,
                            hasCarwashProduct: document.getElementById('has-carwash').checked
                        };

                        try {
                            const response = await fetch(PUBLISH_API, {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify(request)
                            });
                            
                            if (response.ok) {
                                const result = await response.json();
                                successDiv.textContent = `Event published! ID: ${result.eventId}`;
                                successDiv.style.display = 'block';
                                document.getElementById('publish-form').reset();
                                setTimeout(() => updateStatus(), 500);
                            } else {
                                const error = await response.text();
                                errorDiv.textContent = `Error: ${error}`;
                                errorDiv.style.display = 'block';
                            }
                        } catch (error) {
                            errorDiv.textContent = `Error: ${error.message}`;
                            errorDiv.style.display = 'block';
                        }
                    });

                    // Initial load and polling
                    updateStatus();
                    setInterval(updateStatus, POLL_INTERVAL);
                </script>
            </body>
            </html>
            """;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        try
        {
            _httpListener?.Close();
        }
        catch { }
    }
}
