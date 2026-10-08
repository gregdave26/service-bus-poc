using System.Text.Json;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>Helpers shared by the stub resource services.</summary>
public static class StubResources
{
    /// <summary>The custom field carrying the POC correlation id.</summary>
    public const string CorrelationIdField = "correlationId";

    /// <summary>Throws when the expected version does not match.</summary>
    public static void EnsureVersion(long expected, long actual)
    {
        if (expected != actual)
        {
            throw CommerceStubException.ConcurrentModification(expected, actual);
        }
    }

    /// <summary>Converts draft custom fields into stored custom fields.</summary>
    public static CustomFields? ToCustomFields(CustomFieldsDraft? draft) =>
        draft is null
            ? null
            : new CustomFields
            {
                Type = draft.Type,
                Fields = draft.Fields
                    .Where(field => field.Value is not null)
                    .ToDictionary(field => field.Key, field => ToElement(field.Value))
            };

    /// <summary>Sets or removes a custom field, creating the container when needed.</summary>
    public static CustomFields SetCustomField(CustomFields? custom, string name, object? value)
    {
        custom ??= new CustomFields();
        if (value is null || value is JsonElement { ValueKind: JsonValueKind.Null })
        {
            custom.Fields.Remove(name);
        }
        else
        {
            custom.Fields[name] = ToElement(value);
        }

        return custom;
    }

    /// <summary>Gets the correlation id from custom fields.</summary>
    public static string? CorrelationIdOf(CustomFields? custom) => custom?.GetString(CorrelationIdField);

    /// <summary>Converts a value to a JSON element.</summary>
    public static JsonElement ToElement(object? value) =>
        value is JsonElement element ? element.Clone() : JsonSerializer.SerializeToElement(value, StubJson.Options);
}
