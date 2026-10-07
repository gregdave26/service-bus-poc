namespace ServiceBusPoc.DigitalSite.CartProcessor;

/// <summary>Cart Processor configuration, bound from the <c>CartProcessor</c> section.</summary>
public sealed class CartProcessorSettings
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "CartProcessor";

    /// <summary>Gets or sets the topic that receives <c>ProductHoldingChange</c> events.</summary>
    public string ContactEventsTopicName { get; set; } = "contact.events";

    /// <summary>Gets or sets the event source recorded on published events.</summary>
    public string EventSource { get; set; } = "digital-site";
}
