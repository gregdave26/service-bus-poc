using System.Text.Json;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub;

/// <summary>JSON options for the stub: the project defaults, tolerant of update actions whose <c>action</c> is not the first property.</summary>
public static class StubJson
{
    /// <summary>Gets the serializer options.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerOptionsHelper.DefaultOptions)
    {
        AllowOutOfOrderMetadataProperties = true
    };
}
