using System.Text.Json;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Catalog;

/// <summary>Reads the canonical local catalog used when commercetools is not required.</summary>
public sealed class LocalCatalogProvider : ICatalogProvider
{
    private readonly CatalogResponse _catalog;

    /// <summary>Initializes a provider from the local catalog file.</summary>
    public LocalCatalogProvider(string path)
    {
        _catalog = JsonSerializer.Deserialize<CatalogResponse>(
            File.ReadAllText(path),
            JsonSerializerOptionsHelper.DefaultOptions)
            ?? throw new InvalidOperationException($"Local catalog file '{path}' is empty.");
    }

    /// <inheritdoc />
    public Task<CatalogResponse> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_catalog);

    /// <inheritdoc />
    public Task<CoverOffer?> FindCoverAsync(string? sku, CancellationToken cancellationToken = default) =>
        Task.FromResult(_catalog.Covers.FirstOrDefault(cover => cover.Id == sku));
}
