namespace ServiceBusPoc.Core.ConsumerModules;

/// <summary>
/// Represents an optional service hosted alongside a configured consumer.
/// </summary>
public interface IConsumerModule
{
    /// <summary>
    /// Starts the module and returns once startup has been initiated.
    /// </summary>
    /// <param name="cancellationToken">Token used to stop the module.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the module and waits for its resources to be released.
    /// </summary>
    /// <param name="cancellationToken">Token used to bound shutdown.</param>
    Task StopAsync(CancellationToken cancellationToken);
}
