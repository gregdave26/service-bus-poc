using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>
/// A commercetools subscription <c>MessageDeliveryPayload</c>. Message-specific fields are kept in
/// <see cref="Extensions"/> because handlers re-read the resource rather than trust the snapshot.
/// </summary>
public sealed class CommerceMessage
{
    /// <summary>Gets or sets the notification type; always <c>Message</c>.</summary>
    public string NotificationType { get; set; } = "Message";

    /// <summary>Gets or sets the project key.</summary>
    public string ProjectKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the message id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the message version.</summary>
    public long Version { get; set; } = 1;

    /// <summary>Gets or sets the per-resource sequence number.</summary>
    public long SequenceNumber { get; set; }

    /// <summary>Gets or sets the resource that changed.</summary>
    public ResourceReference Resource { get; set; } = new();

    /// <summary>Gets or sets the resource version after the change.</summary>
    public long ResourceVersion { get; set; }

    /// <summary>Gets or sets the message type, for example <c>PaymentTransactionAdded</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets when the message was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets message-specific fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}

/// <summary>commercetools message types the POC emits or handles.</summary>
public static class CommerceMessageTypes
{
    /// <summary>A transaction was added to a payment.</summary>
    public const string PaymentTransactionAdded = "PaymentTransactionAdded";

    /// <summary>A payment transaction changed state.</summary>
    public const string PaymentTransactionStateChanged = "PaymentTransactionStateChanged";

    /// <summary>An order was created.</summary>
    public const string OrderCreated = "OrderCreated";

    /// <summary>An order changed state.</summary>
    public const string OrderStateChanged = "OrderStateChanged";

    /// <summary>The Service Bus application property carrying the message type, used by subscription filters.</summary>
    public const string MessageTypeProperty = "messageType";
}
