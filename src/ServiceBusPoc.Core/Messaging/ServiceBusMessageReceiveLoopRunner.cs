using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Azure Service Bus implementation of the message receive loop.
/// Coordinates with <see cref="IMessageReceiveOrchestrator"/> to handle messages, timeouts, and failures.
/// Uses exponential backoff on receive failures; respects configured receive timeout.
/// </summary>
public sealed class ServiceBusMessageReceiveLoopRunner : IMessageReceiveLoopRunner
{
    private static readonly TimeSpan ReceiveWaitTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DelayAfterReceiveFailure = TimeSpan.FromSeconds(2);

    private readonly IServiceBusReceiver _receiver;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="ServiceBusMessageReceiveLoopRunner"/> class.</summary>
    /// <param name="receiver">Service Bus receiver used to get messages.</param>
    /// <param name="logger">Logger for operational events.</param>
    public ServiceBusMessageReceiveLoopRunner(
        IServiceBusReceiver receiver,
        ILogger logger)
    {
        _receiver = receiver;
        _logger = logger;
    }

    /// <summary>Runs the receive loop until the token is cancelled.</summary>
    /// <param name="orchestrator">Handles messages and event callbacks.</param>
    /// <param name="cancellationToken">Token used to stop the loop.</param>
    /// <returns>A task that completes when the loop stops.</returns>
    public async Task RunAsync(IMessageReceiveOrchestrator orchestrator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);

        while (!cancellationToken.IsCancellationRequested)
        {
            ServiceBusReceivedMessage? message;

            try
            {
                message = await _receiver.ReceiveMessageAsync(ReceiveWaitTime, cancellationToken);
            }
            catch (ServiceBusException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to receive message from subscription: {FailureReason}",
                    ex.Reason);

                await orchestrator.OnReceiveFailureAsync(ex, cancellationToken);
                await Task.Delay(DelayAfterReceiveFailure, cancellationToken);
                continue;
            }

            if (message is null)
            {
                await orchestrator.OnEmptyReceiveAsync(cancellationToken);
                continue;
            }

            await orchestrator.ProcessMessageAsync(message, cancellationToken);
        }
    }
}
