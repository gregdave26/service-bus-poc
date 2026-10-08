using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Catalog;

/// <summary>Provides the Digital Site catalog independently of its backing platform.</summary>
public interface ICatalogProvider
{
    /// <summary>Gets the catalog exposed to the Digital Site.</summary>
    Task<CatalogResponse> GetCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds an offered cover by its stable SKU.</summary>
    Task<CoverOffer?> FindCoverAsync(string? sku, CancellationToken cancellationToken = default);
}
