namespace ServiceBusPoc.Core.Dashboard;

/// <summary>
/// A message observation sent to the dashboard.
/// </summary>
public sealed class DashboardMessage
{
    /// <summary>Gets or sets the broker message identifier.</summary>
    public required string MessageId { get; init; }

    /// <summary>Gets or sets the event identifier from the envelope.</summary>
    public required string EventId { get; init; }

    /// <summary>Gets or sets the service that sent or received the message.</summary>
    public required string ServiceName { get; init; }

    /// <summary>Gets or sets whether the message was sent or received.</summary>
    public required string Direction { get; init; }

    /// <summary>Gets or sets the observation timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets or sets the optional subscription name.</summary>
    public string? SubscriptionName { get; init; }

    /// <summary>Gets or sets the original JSON message body.</summary>
    public required string Payload { get; init; }
}
