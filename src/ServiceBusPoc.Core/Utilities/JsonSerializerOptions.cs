using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceBusPoc.Core.Utilities;

/// <summary>
/// Shared JSON serialization options for consistent serialization across all projects.
/// Ensures camelCase property naming and ISO 8601 datetime formatting.
/// </summary>
public static class JsonSerializerOptionsHelper
{
    /// <summary>
    /// Gets the default JsonSerializerOptions configured for this project.
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    /// <summary>
    /// Gets the JsonSerializerOptions configured for indented/pretty-print output.
    /// Useful for logging and debugging.
    /// </summary>
    public static JsonSerializerOptions IndentedOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
}
