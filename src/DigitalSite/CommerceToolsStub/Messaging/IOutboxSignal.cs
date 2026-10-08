namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Messaging;

/// <summary>Wakes the outbox dispatcher when new messages are committed.</summary>
public interface IOutboxSignal
{
    /// <summary>Signals that messages are waiting.</summary>
    void Notify();

    /// <summary>Waits for a signal or the timeout.</summary>
    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>Semaphore-based <see cref="IOutboxSignal"/>.</summary>
public sealed class OutboxSignal : IOutboxSignal, IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    /// <inheritdoc />
    public void Notify()
    {
        if (_signal.CurrentCount == 0)
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Another writer already signalled; one wake-up drains every pending message.
            }
        }
    }

    /// <inheritdoc />
    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _signal.WaitAsync(timeout, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _signal.Dispose();
}
