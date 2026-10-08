using ServiceBusPoc.DigitalSite.CommerceApi.Catalog;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class LocalCatalogProviderTests
{
    [Fact]
    public async Task Returns_the_same_application_catalog_shape_as_the_commerce_api()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """
                {
                  "covers": [{
                    "id": "CLAS",
                    "productId": "product-1",
                    "name": "Classic",
                    "annualPrice": 310.0,
                    "price": { "type": "centPrecision", "currencyCode": "AUD", "centAmount": 31000, "fractionDigits": 2 },
                    "chip": "Popular",
                    "perks": ["Towing"]
                  }],
                  "paymentPlans": [{
                    "id": "Annual",
                    "title": "Pay yearly by card",
                    "chips": ["Set and forget"]
                  }]
                }
                """);

            var provider = new LocalCatalogProvider(path);
            var catalog = await provider.GetCatalogAsync();
            var cover = Assert.Single(catalog.Covers);

            Assert.Equal("CLAS", cover.Id);
            Assert.Equal("product-1", cover.ProductId);
            Assert.Equal(310m, cover.AnnualPrice);
            Assert.Equal(31000, cover.Price.CentAmount);
            Assert.Equal("AUD", cover.Price.CurrencyCode);
            Assert.Equal("Annual", Assert.Single(catalog.PaymentPlans).Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Finds_covers_by_stable_sku()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """
                {
                  "covers": [{
                    "id": "CLAS",
                    "productId": "product-1",
                    "name": "Classic",
                    "annualPrice": 310.0,
                    "price": { "type": "centPrecision", "currencyCode": "AUD", "centAmount": 31000, "fractionDigits": 2 },
                    "chip": "Popular",
                    "perks": []
                  }],
                  "paymentPlans": []
                }
                """);

            var provider = new LocalCatalogProvider(path);

            Assert.NotNull(await provider.FindCoverAsync("CLAS"));
            Assert.Null(await provider.FindCoverAsync("missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
