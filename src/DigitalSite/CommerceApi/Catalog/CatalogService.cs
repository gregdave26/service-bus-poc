using System.Text.Json;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Catalog;

/// <summary>A cover the shopper can buy: commercetools product and price plus display copy.</summary>
/// <param name="Id">The SKU, used as the cover id.</param>
/// <param name="ProductId">The commercetools product id.</param>
/// <param name="Name">The cover level name.</param>
/// <param name="AnnualPrice">The annual price in dollars.</param>
/// <param name="Price">The annual price as commercetools money.</param>
/// <param name="Chip">The highlight chip.</param>
/// <param name="Perks">The perks.</param>
public sealed record CoverOffer(
    string Id,
    string ProductId,
    string Name,
    decimal AnnualPrice,
    Money Price,
    string Chip,
    IReadOnlyList<string> Perks);

/// <summary>The catalog response.</summary>
/// <param name="Covers">The covers in display order.</param>
/// <param name="PaymentPlans">The payment plans.</param>
public sealed record CatalogResponse(IReadOnlyList<CoverOffer> Covers, IReadOnlyList<PaymentPlanContent> PaymentPlans);

/// <summary>Builds the cover catalog from commercetools products, which own SKUs and prices.</summary>
public sealed class CatalogService
{
    /// <summary>The product attribute holding the cover level.</summary>
    public const string LevelAttribute = "Level";

    private readonly ICommerceToolsClient _commerceTools;
    private readonly SiteContent _content;
    private readonly DigitalSiteSettings _settings;

    /// <summary>Initializes a new instance of the <see cref="CatalogService"/> class.</summary>
    /// <param name="commerceTools">The commercetools client.</param>
    /// <param name="content">The display copy.</param>
    /// <param name="settings">The Digital Site settings.</param>
    public CatalogService(ICommerceToolsClient commerceTools, SiteContent content, IOptions<DigitalSiteSettings> settings)
    {
        _commerceTools = commerceTools;
        _content = content;
        _settings = settings.Value;
    }

    /// <summary>Gets the catalog. Products without display copy or a price are not offered.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The catalog.</returns>
    public async Task<CatalogResponse> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var productType = await _commerceTools.GetProductTypeByKeyAsync(_settings.ProductTypeKey, cancellationToken)
            ?? throw new InvalidOperationException($"commercetools has no product type '{_settings.ProductTypeKey}'");
        var products = await _commerceTools.QueryProductProjectionsAsync(
            CommercePredicate.ReferenceIdEqualTo("productType", productType.Id),
            cancellationToken);

        var covers = _content.Covers
            .Select(content => ToOffer(content, products))
            .OfType<CoverOffer>()
            .ToArray();
        return new CatalogResponse(covers, _content.PaymentPlans);
    }

    /// <summary>Finds a cover by SKU.</summary>
    /// <param name="sku">The SKU.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cover, or <see langword="null"/> when it is not offered.</returns>
    public async Task<CoverOffer?> FindCoverAsync(string? sku, CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        return catalog.Covers.FirstOrDefault(cover => cover.Id == sku);
    }

    private CoverOffer? ToOffer(CoverContent content, IReadOnlyList<ProductProjection> products)
    {
        var product = products.FirstOrDefault(candidate => candidate.MasterVariant.Sku == content.Sku);
        var price = product?.MasterVariant.Prices.FirstOrDefault(candidate =>
            string.Equals(candidate.Value.CurrencyCode, _settings.Currency, StringComparison.OrdinalIgnoreCase));
        if (product is null || price is null)
        {
            return null;
        }

        return new CoverOffer(
            content.Sku,
            product.Id,
            LevelLabel(product.MasterVariant) ?? content.Sku,
            price.Value.ToDecimal(),
            price.Value,
            content.Chip,
            content.Perks);
    }

    /// <summary>Gets the cover level label of a variant, for example <c>Classic</c>.</summary>
    /// <param name="variant">The product variant.</param>
    /// <returns>The label, or <see langword="null"/> when the variant has no level.</returns>
    public static string? LevelLabel(ProductVariant variant) =>
        variant.Attributes.FirstOrDefault(attribute => attribute.Name == LevelAttribute) is { } level
        && level.Value.ValueKind == JsonValueKind.Object
        && level.Value.TryGetProperty("label", out var label)
        && label.ValueKind == JsonValueKind.String
            ? label.GetString()
            : null;
}
