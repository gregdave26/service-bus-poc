namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>
/// Serialises writes so the read-check-version-write sequence is atomic; the stub is a single instance.
/// </summary>
public sealed class StubWriteLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>Waits for exclusive write access.</summary>
    /// <param name="cancellationToken">Token used to cancel the wait.</param>
    /// <returns>A handle that releases access when disposed.</returns>
    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    /// <inheritdoc />
    public void Dispose() => _semaphore.Dispose();

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
