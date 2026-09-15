using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ServiceBusPoc.Core.Contracts;

/// <summary>
/// Generic event wrapper for all Service Bus messages.
/// Provides consistent envelope structure for routing, filtering, and correlation.
/// </summary>
/// <typeparam name="TData">The type of event-specific data payload.</typeparam>
public class EventEnvelope<TData>
    where TData : class
{
    /// <summary>
    /// Gets or sets the unique event identifier (UUID v4).
    /// </summary>
    [Required(ErrorMessage = "Id is required")]
    [StringLength(36, MinimumLength = 36, ErrorMessage = "Id must be a valid UUID")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the event type identifier (e.g., 'contact.updated', 'product.holding.changed').
    /// </summary>
    [Required(ErrorMessage = "Type is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Type must be between 1 and 255 characters")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the system or service originating the event (e.g., 'crm', 'mdm', 'product-service').
    /// </summary>
    [Required(ErrorMessage = "Source is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Source must be between 1 and 255 characters")]
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the ISO 8601 UTC timestamp when the event was created.
    /// </summary>
    [Required(ErrorMessage = "Timestamp is required")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the version of the data schema (e.g., '1', '1.0', 'v1').
    /// </summary>
    [Required(ErrorMessage = "DataVersion is required")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "DataVersion must be between 1 and 20 characters")]
    public string? DataVersion { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier for tracing related events across systems.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the event-specific payload.
    /// </summary>
    [Required(ErrorMessage = "Data is required")]
    [JsonRequired]
    public TData? Data { get; set; }
}
