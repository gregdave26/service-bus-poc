using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Configuration settings for Azure Service Bus connection and topology.
/// Loaded from environment variables using IOptions&lt;T&gt;.
/// </summary>
public class ServiceBusSettings
{
    /// <summary>
    /// Gets or sets the Service Bus connection string.
    /// Must be loaded from environment variable 'ServiceBus:ConnectionString'.
    /// </summary>
    [Required(ErrorMessage = "ServiceBus ConnectionString is required")]
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Service Bus namespace name.
    /// Must be loaded from environment variable 'ServiceBus:Namespace'.
    /// </summary>
    [Required(ErrorMessage = "ServiceBus Namespace is required")]
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the topic name for contact events.
    /// Must be loaded from environment variable 'ServiceBus:TopicName'.
    /// </summary>
    [Required(ErrorMessage = "ServiceBus TopicName is required")]
    public string? TopicName { get; set; }

    /// <summary>
    /// Gets or sets the subscription name for this consumer.
    /// Must be loaded from environment variable 'ServiceBus:SubscriptionName'.
    /// </summary>
    public string? SubscriptionName { get; set; }
}
