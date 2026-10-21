namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Runs the receive loop for a Service Bus subscription with resilience and error handling.
/// Encapsulates common logic: timeout management, exponential backoff, cancellation handling.
/// </summary>
public interface IMessageReceiveLoopRunner
{
    /// <summary>
    /// Runs the message receive loop until cancellation.
    /// Handles:
    /// - Message reception with configurable timeout
    /// - Exponential backoff on receive failures
    /// - Invoking the orchestrator for message processing and event callbacks
    /// </summary>
    /// <param name="orchestrator">Handles messages and events.</param>
    /// <param name="cancellationToken">Token used to stop the loop.</param>
    /// <returns>A task that completes when the loop exits.</returns>
    Task RunAsync(IMessageReceiveOrchestrator orchestrator, CancellationToken cancellationToken);
}
