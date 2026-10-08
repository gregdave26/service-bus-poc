using System.Text.Json;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Seeding;

/// <summary>
/// Seeds the Roadside Assistance product type, its four products and the digital shipping method on
/// first start, mirroring the set-up done in the commercetools project.
/// </summary>
public sealed class CatalogSeeder : IHostedService
{
    private const string LevelAttribute = "Level";
    private const string Currency = "AUD";
    private const string Locale = "en-AU";

    private readonly IResourceStore _store;
    private readonly ILogger<CatalogSeeder> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly string _seedPath;

    /// <summary>Initializes a new instance of the <see cref="CatalogSeeder"/> class.</summary>
    public CatalogSeeder(IResourceStore store, ILogger<CatalogSeeder> logger, TimeProvider timeProvider)
        : this(store, logger, timeProvider, Path.Combine(AppContext.BaseDirectory, "Seeding", "ct-seed.json"))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CatalogSeeder"/> class with a seed file.</summary>
    public CatalogSeeder(IResourceStore store, ILogger<CatalogSeeder> logger, TimeProvider timeProvider, string seedPath)
    {
        _store = store;
        _logger = logger;
        _timeProvider = timeProvider;
        _seedPath = seedPath;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await _store.AnyAsync(ResourceTypes.ProductType, cancellationToken))
        {
            return;
        }

        await using var stream = File.OpenRead(_seedPath);
        var seed = await JsonSerializer.DeserializeAsync<CatalogSeed>(stream, StubJson.Options, cancellationToken)
            ?? throw new InvalidOperationException($"Seed file '{_seedPath}' is empty.");

        var productType = new ProductType { Id = seed.ProductType.Id, Version = 1, Key = seed.ProductType.Key, Name = seed.ProductType.Name };
        var writes = new List<ResourceWrite> { new(ResourceTypes.ProductType, productType.Id, productType.Key, productType) };
        writes.AddRange(seed.ShippingMethods.Select(method =>
            new ResourceWrite(ResourceTypes.ShippingMethod, method.Id, method.Key, method)));
        writes.AddRange(seed.Products.Select(product =>
        {
            var projection = ToProjection(product, productType.Id);
            return new ResourceWrite(ResourceTypes.Product, projection.Id, projection.Key, projection);
        }));

        await _store.CommitAsync(writes, [], cancellationToken);
        _logger.LogInformation(
            "Seeded product type {ProductTypeKey} with {ProductCount} products at {SeededAt}",
            productType.Key,
            seed.Products.Count,
            _timeProvider.GetUtcNow());
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static ProductProjection ToProjection(ProductSeed product, string productTypeId) =>
        new()
        {
            Id = product.Id,
            Version = 1,
            Key = product.Key,
            ProductType = ResourceReference.ById(ResourceTypes.ProductType, productTypeId),
            Name = new Dictionary<string, string> { [Locale] = product.Name },
            Description = new Dictionary<string, string> { [Locale] = product.Description },
            MasterVariant = new ProductVariant
            {
                Id = 1,
                Sku = product.Sku,
                Key = product.Sku,
                Prices = [new Price { Id = $"{product.Key}-{Currency}", Value = Money.FromCents(Currency, product.CentAmount) }],
                Attributes =
                [
                    new ProductAttribute
                    {
                        Name = LevelAttribute,
                        Value = JsonSerializer.SerializeToElement(new { key = product.LevelKey, label = product.LevelLabel })
                    }
                ]
            }
        };

    private sealed class CatalogSeed
    {
        public ProductTypeSeed ProductType { get; set; } = new();

        public List<ShippingMethod> ShippingMethods { get; set; } = [];

        public List<ProductSeed> Products { get; set; } = [];
    }

    private sealed class ProductTypeSeed
    {
        public string Id { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class ProductSeed
    {
        public string Id { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Sku { get; set; } = string.Empty;

        public string LevelKey { get; set; } = string.Empty;

        public string LevelLabel { get; set; } = string.Empty;

        public long CentAmount { get; set; }
    }
}
