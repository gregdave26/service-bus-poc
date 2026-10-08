namespace ServiceBusPoc.DigitalSite.Shared.Dashboard;

/// <summary>Activity counters a web service reports in its dashboard heartbeat.</summary>
public interface IServiceActivity
{
    /// <summary>Gets the dashboard service name.</summary>
    string ServiceName { get; }

    /// <summary>Gets the number of messages handled or emitted.</summary>
    long MessagesHandled { get; }

    /// <summary>Gets when the last message was handled.</summary>
    DateTimeOffset? LastMessageAt { get; }

    /// <summary>Gets the last message id.</summary>
    string? LastEventId { get; }

    /// <summary>Records a handled message.</summary>
    /// <param name="eventId">The message id.</param>
    void Record(string eventId);
}

/// <summary>Thread-safe <see cref="IServiceActivity"/>.</summary>
public sealed class ServiceActivity : IServiceActivity
{
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private long _messagesHandled;
    private DateTimeOffset? _lastMessageAt;
    private string? _lastEventId;

    /// <summary>Initializes a new instance of the <see cref="ServiceActivity"/> class.</summary>
    /// <param name="serviceName">The dashboard service name.</param>
    /// <param name="timeProvider">The clock.</param>
    public ServiceActivity(string serviceName, TimeProvider timeProvider)
    {
        ServiceName = serviceName;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public string ServiceName { get; }

    /// <inheritdoc />
    public long MessagesHandled { get { lock (_lock) { return _messagesHandled; } } }

    /// <inheritdoc />
    public DateTimeOffset? LastMessageAt { get { lock (_lock) { return _lastMessageAt; } } }

    /// <inheritdoc />
    public string? LastEventId { get { lock (_lock) { return _lastEventId; } } }

    /// <inheritdoc />
    public void Record(string eventId)
    {
        lock (_lock)
        {
            _messagesHandled++;
            _lastMessageAt = _timeProvider.GetUtcNow();
            _lastEventId = eventId;
        }
    }
}
