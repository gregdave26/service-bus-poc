namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Identifies a consumer application to logs and to the monitoring dashboard.
/// </summary>
/// <param name="ServiceName">Stable identifier of the consumer, for example <c>insurance</c>.</param>
/// <param name="FilterDescription">
/// The broker-side filter that decides what this consumer receives, for example <c>hasInsurance = true</c>.
/// Used for logging only; consumers never re-evaluate it.
/// </param>
public sealed record ConsumerDescriptor(string ServiceName, string FilterDescription)
{
    /// <summary>Description used by subscriptions that have no filter.</summary>
    public const string NoFilter = "none (receives all messages)";
}
