using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.Dashboard.Api;

/// <summary>
/// Publishes events from the dashboard UI to the Service Bus.
/// This is the dashboard-specific handler for the publish endpoint.
/// </summary>
public sealed class PublishEventHandler
{
    private readonly ContactEventPublisher _publisher;
    private readonly ILogger<PublishEventHandler> _logger;

    public PublishEventHandler(
        ContactEventPublisher publisher,
        ILogger<PublishEventHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Publishes a contact event from the dashboard.
    /// </summary>
    public async Task<PublishEventResponse> PublishEventAsync(PublishEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contact = new ContactData
        {
            ContactId = request.ContactId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Email = request.Email,
            Attributes = new ContactAttributes
            {
                HasInsurance = request.HasInsurance,
                HasParksResorts = request.HasParksResorts,
                HasCarwashProduct = request.HasCarwashProduct,
                RacId = request.RacId
            }
        };

        try
        {
            var eventId = await _publisher.PublishContactUpdatedAsync(
                contact,
                "dashboard",
                cancellationToken: CancellationToken.None);

            return new PublishEventResponse { EventId = eventId, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event from dashboard");
            return new PublishEventResponse
            {
                EventId = null,
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Request to publish an event from the dashboard.
/// </summary>
public sealed class PublishEventRequest
{
    public required string ContactId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
    public string? RacId { get; init; }
    public bool HasInsurance { get; init; }
    public bool HasParksResorts { get; init; }
    public bool HasCarwashProduct { get; init; }
}

/// <summary>
/// Response from publishing an event.
/// </summary>
public sealed class PublishEventResponse
{
    public string? EventId { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
}
