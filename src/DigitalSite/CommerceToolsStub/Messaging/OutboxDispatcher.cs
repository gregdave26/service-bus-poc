using Azure.Messaging.ServiceBus;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Messaging;

/// <summary>
/// Publishes committed outbox messages in order. A message is marked published only after the
/// broker accepts it, so delivery is at-least-once, as with a real commercetools subscription.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IResourceStore _store;
    private readonly ICommerceMessagePublisher _publisher;
    private readonly IOutboxSignal _signal;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="OutboxDispatcher"/> class.</summary>
    public OutboxDispatcher(
        IResourceStore store,
        ICommerceMessagePublisher publisher,
        IOutboxSignal signal,
        ILogger<OutboxDispatcher> logger,
        TimeProvider timeProvider)
    {
        _store = store;
        _publisher = publisher;
        _signal = signal;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>Publishes every waiting message, stopping at the first failure so ordering is preserved.</summary>
    /// <returns>The number of messages published.</returns>
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var published = 0;
        foreach (var message in await _store.GetUnpublishedMessagesAsync(BatchSize, cancellationToken))
        {
            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
            }
            catch (ServiceBusException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not publish {MessageType} {MessageId}; will retry: {FailureReason}",
                    message.Message.Type,
                    message.Message.Id,
                    ex.Reason);
                break;
            }

            await _store.MarkPublishedAsync(message.Message.Id, _timeProvider.GetUtcNow(), cancellationToken);
            published++;
        }

        return published;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DispatchPendingAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Outbox dispatch failed; will retry");
                }

                await _signal.WaitAsync(PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Outbox dispatcher stopping");
        }
    }
}
