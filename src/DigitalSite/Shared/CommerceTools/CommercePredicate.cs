namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools;

/// <summary>
/// Builds commercetools query predicates from trusted field names and untrusted values.
/// </summary>
public static class CommercePredicate
{
    /// <summary>Builds <c>field = "value"</c> with the value escaped.</summary>
    /// <param name="field">The field path, for example <c>anonymousId</c>.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The predicate.</returns>
    public static string EqualTo(string field, string value) =>
        $"{field} = \"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    /// <summary>Builds <c>reference(id = "value")</c> for a reference field.</summary>
    /// <param name="referenceField">The reference field, for example <c>cart</c>.</param>
    /// <param name="id">The referenced resource id.</param>
    /// <returns>The predicate.</returns>
    public static string ReferenceIdEqualTo(string referenceField, string id) => $"{referenceField}({EqualTo("id", id)})";

    /// <summary>Joins predicates with <c>and</c>.</summary>
    /// <param name="predicates">The predicates.</param>
    /// <returns>The combined predicate.</returns>
    public static string And(params string[] predicates) => string.Join(" and ", predicates);
}
