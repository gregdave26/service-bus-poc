using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Querying;

/// <summary>
/// Evaluates the subset of commercetools query predicates the Digital Site uses: string equality on a
/// field path (<c>anonymousId = "x"</c>), reference shorthand (<c>cart(id = "x")</c>) and <c>and</c>.
/// </summary>
public sealed partial class QueryPredicate
{
    private readonly IReadOnlyList<(string Path, string Value)> _conditions;

    private QueryPredicate(IReadOnlyList<(string Path, string Value)> conditions)
    {
        _conditions = conditions;
    }

    /// <summary>Parses a predicate; an empty predicate matches everything.</summary>
    /// <param name="where">The predicate text.</param>
    /// <returns>The parsed predicate.</returns>
    /// <exception cref="FormatException">The predicate uses unsupported syntax.</exception>
    public static QueryPredicate Parse(string? where)
    {
        if (string.IsNullOrWhiteSpace(where))
        {
            return new QueryPredicate([]);
        }

        return new QueryPredicate(SplitOnAnd(where).Select(term => ParseTerm(term.Trim(), prefix: string.Empty)).ToArray());
    }

    /// <summary>Gets a value indicating whether the resource matches.</summary>
    /// <param name="resource">The resource as JSON.</param>
    /// <returns><see langword="true"/> when every condition matches.</returns>
    public bool Matches(JsonNode? resource) =>
        _conditions.All(condition => string.Equals(Resolve(resource, condition.Path), condition.Value, StringComparison.Ordinal));

    private static (string Path, string Value) ParseTerm(string term, string prefix)
    {
        var equality = EqualityPattern().Match(term);
        if (equality.Success)
        {
            var value = Regex.Unescape(equality.Groups["value"].Value);
            return (prefix + equality.Groups["field"].Value, value);
        }

        var reference = ReferencePattern().Match(term);
        if (reference.Success)
        {
            return ParseTerm(reference.Groups["inner"].Value.Trim(), $"{prefix}{reference.Groups["name"].Value}.");
        }

        throw new FormatException($"Unsupported predicate term '{term}'.");
    }

    private static IEnumerable<string> SplitOnAnd(string where)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        var depth = 0;
        for (var index = 0; index < where.Length; index++)
        {
            var character = where[index];
            if (character == '"' && (index == 0 || where[index - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && character == '(')
            {
                depth++;
            }
            else if (!inQuotes && character == ')')
            {
                depth--;
            }

            if (!inQuotes && depth == 0 && IsAndKeywordAt(where, index))
            {
                yield return current.ToString();
                current.Clear();
                index += " and ".Length - 1;
                continue;
            }

            current.Append(character);
        }

        yield return current.ToString();
    }

    private static bool IsAndKeywordAt(string text, int index) =>
        string.Compare(text, index, " and ", 0, " and ".Length, StringComparison.OrdinalIgnoreCase) == 0;

    private static string? Resolve(JsonNode? node, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            node = node is JsonObject jsonObject && jsonObject.TryGetPropertyValue(segment, out var child) ? child : null;
            if (node is null)
            {
                return null;
            }
        }

        return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node?.ToJsonString();
    }

    [GeneratedRegex("""^(?<field>[A-Za-z][A-Za-z0-9_.]*)\s*=\s*"(?<value>(?:[^"\\]|\\.)*)"$""")]
    private static partial Regex EqualityPattern();

    [GeneratedRegex("""^(?<name>[A-Za-z][A-Za-z0-9_]*)\s*\((?<inner>.+)\)$""")]
    private static partial Regex ReferencePattern();
}
