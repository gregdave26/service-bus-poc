using System.Text.Json;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Querying;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>Applies predicate, sort and paging to an in-memory result set.</summary>
public static class QueryResults
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 500;

    /// <summary>Builds a page of results.</summary>
    /// <exception cref="CommerceStubException">The predicate or sort is not supported.</exception>
    public static PagedQueryResponse<T> Page<T>(IEnumerable<T> items, string? where, string? sort, int? limit, int? offset)
    {
        QueryPredicate predicate;
        try
        {
            predicate = QueryPredicate.Parse(where);
        }
        catch (FormatException ex)
        {
            throw CommerceStubException.InvalidInput($"Malformed parameter: where: {ex.Message}");
        }

        var matches = items
            .Select(item => (Item: item, Node: JsonSerializer.SerializeToNode(item, StubJson.Options)))
            .Where(entry => predicate.Matches(entry.Node))
            .ToList();

        if (!string.IsNullOrWhiteSpace(sort))
        {
            var parts = sort.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var field = parts[0];
            var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
            Func<(T Item, System.Text.Json.Nodes.JsonNode? Node), string?> key = entry => entry.Node?[field]?.ToJsonString();
            matches = (descending
                ? matches.OrderByDescending(key, StringComparer.Ordinal)
                : matches.OrderBy(key, StringComparer.Ordinal)).ToList();
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 0, MaxLimit);
        var skip = Math.Max(offset ?? 0, 0);
        var results = matches.Skip(skip).Take(take).Select(entry => entry.Item).ToList();
        return new PagedQueryResponse<T> { Limit = take, Offset = skip, Count = results.Count, Total = matches.Count, Results = results };
    }
}
