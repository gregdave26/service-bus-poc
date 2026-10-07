using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>Read-only product catalogue endpoints.</summary>
public sealed class CatalogService
{
    private readonly IResourceStore _store;

    /// <summary>Initializes a new instance of the <see cref="CatalogService"/> class.</summary>
    public CatalogService(IResourceStore store)
    {
        _store = store;
    }

    /// <summary>Gets a product type by key.</summary>
    public async Task<ProductType> GetProductTypeByKeyAsync(string key, CancellationToken cancellationToken) =>
        await _store.GetByKeyAsync<ProductType>(ResourceTypes.ProductType, key, cancellationToken)
        ?? throw CommerceStubException.NotFound(ResourceTypes.ProductType, key);

    /// <summary>Queries product projections.</summary>
    public async Task<PagedQueryResponse<ProductProjection>> QueryProductProjectionsAsync(
        string? where, string? sort, int? limit, int? offset, CancellationToken cancellationToken) =>
        QueryResults.Page(await _store.ListAsync<ProductProjection>(ResourceTypes.Product, cancellationToken), where, sort, limit, offset);

    /// <summary>Finds the product whose master variant has the SKU, or the product with the id.</summary>
    public async Task<ProductProjection> FindProductAsync(LineItemDraft draft, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(draft.ProductId))
        {
            return await _store.GetAsync<ProductProjection>(ResourceTypes.Product, draft.ProductId, cancellationToken)
                ?? throw CommerceStubException.InvalidInput($"A product with ID '{draft.ProductId}' not found.");
        }

        if (!string.IsNullOrWhiteSpace(draft.Sku))
        {
            var products = await _store.ListAsync<ProductProjection>(ResourceTypes.Product, cancellationToken);
            return products.FirstOrDefault(product => product.MasterVariant.Sku == draft.Sku)
                ?? throw CommerceStubException.InvalidInput($"A product with SKU '{draft.Sku}' not found.");
        }

        throw CommerceStubException.InvalidInput("Either productId or sku is required for a line item.");
    }

    /// <summary>Resolves a shipping method by id or key.</summary>
    public async Task<ShippingMethod> FindShippingMethodAsync(ResourceReference reference, CancellationToken cancellationToken)
    {
        var method = reference.Id is not null
            ? await _store.GetAsync<ShippingMethod>(ResourceTypes.ShippingMethod, reference.Id, cancellationToken)
            : reference.Key is not null
                ? await _store.GetByKeyAsync<ShippingMethod>(ResourceTypes.ShippingMethod, reference.Key, cancellationToken)
                : null;
        return method ?? throw CommerceStubException.InvalidInput("The referenced shipping method was not found.");
    }
}
