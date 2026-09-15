using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace ServiceBusPoc.Carwash.Api;

/// <summary>
/// Minimal HTTP API server for Carwash verification endpoint.
/// Handles POST /carwash/v1/verify for Pulse member validation.
/// </summary>
public class CarwashApiServer
{
    private readonly HttpListener _httpListener;
    private readonly ILogger<CarwashApiServer> _logger;
    private CancellationToken _cancellationToken;

    public CarwashApiServer(ILogger<CarwashApiServer> logger, int port = 5000)
    {
        _logger = logger;
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{port}/");
    }

    /// <summary>
    /// Starts the HTTP listener and begins processing requests.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        
        try
        {
            _httpListener.Start();
            _logger.LogInformation("Carwash API server started on http://localhost:5000");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    // Use async wait for requests
                    var getContextTask = _httpListener.GetContextAsync();
                    context = await getContextTask.ConfigureAwait(false);
                    
                    await HandleRequestAsync(context);
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    // Server was stopped
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request");
                    context?.Response.Close();
                }
            }
        }
        finally
        {
            _httpListener?.Stop();
            _httpListener?.Close();
            _logger.LogInformation("Carwash API server stopped");
        }
    }

    /// <summary>
    /// Processes incoming HTTP requests.
    /// </summary>
    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Only handle POST to /carwash/v1/verify
            if (request.HttpMethod != "POST" || request.Url?.AbsolutePath != "/carwash/v1/verify")
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.ContentType = "application/json";
                
                var error = new { message = "Endpoint not found" };
                var json = JsonSerializer.Serialize(error);
                await WriteResponseAsync(response, json);
                return;
            }

            // Read and parse request body
            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                RespondWithBadRequest(response, "'Rac Id' must not be empty.");
                return;
            }

            Contracts.VerifyMemberRequest? verifyRequest = null;
            try
            {
                verifyRequest = JsonSerializer.Deserialize<Contracts.VerifyMemberRequest>(body, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse request body");
                RespondWithBadRequest(response, "Invalid JSON format");
                return;
            }

            // Validate RacId
            if (verifyRequest is null || string.IsNullOrWhiteSpace(verifyRequest.RacId))
            {
                RespondWithBadRequest(response, "'Rac Id' must not be empty.");
                return;
            }

            // Mock validation: RAC IDs starting with "VALID" are valid members
            var isValidMember = verifyRequest.RacId.StartsWith("VALID", StringComparison.OrdinalIgnoreCase);

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "application/json";

            var successResponse = new Contracts.VerifyMemberResponse
            {
                ValidMember = isValidMember
            };

            var responseJson = JsonSerializer.Serialize(successResponse);
            await WriteResponseAsync(response, responseJson);

            _logger.LogInformation("Verified member {RacId}: ValidMember={ValidMember}", verifyRequest.RacId, isValidMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing verify request");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.ContentType = "application/json";
            
            var error = new { message = "Internal server error" };
            var json = JsonSerializer.Serialize(error);
            await WriteResponseAsync(response, json);
        }
    }

    /// <summary>
    /// Sends a 400 Bad Request response with error message.
    /// </summary>
    private static void RespondWithBadRequest(HttpListenerResponse response, string errorMessage)
    {
        response.StatusCode = (int)HttpStatusCode.BadRequest;
        response.ContentType = "application/json";

        var errorResponse = new Contracts.ErrorResponse
        {
            Errors = new List<string> { errorMessage }
        };

        var json = JsonSerializer.Serialize(errorResponse);
        _ = WriteResponseAsync(response, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes response body and closes the connection.
    /// </summary>
    private static async Task WriteResponseAsync(HttpListenerResponse response, string content)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        
        using var output = response.OutputStream;
        await output.WriteAsync(buffer, 0, buffer.Length);
        response.Close();
    }

    /// <summary>
    /// Stops the API server.
    /// </summary>
    public void Stop()
    {
        try
        {
            _httpListener?.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already stopped
        }
    }
}
