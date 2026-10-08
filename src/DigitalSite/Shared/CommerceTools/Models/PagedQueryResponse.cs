namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>A page of query results.</summary>
/// <typeparam name="T">The resource type.</typeparam>
public sealed class PagedQueryResponse<T>
{
    /// <summary>Gets or sets the requested page size.</summary>
    public int Limit { get; set; }

    /// <summary>Gets or sets the number of skipped results.</summary>
    public int Offset { get; set; }

    /// <summary>Gets or sets the number of results in this page.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets the total number of matching results.</summary>
    public int? Total { get; set; }

    /// <summary>Gets or sets the results.</summary>
    public List<T> Results { get; set; } = [];
}
