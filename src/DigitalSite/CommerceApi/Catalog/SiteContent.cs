using System.Text.Json;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Catalog;

/// <summary>
/// Marketing copy and demo data that commercetools does not hold: cover chips and perks, the
/// payment plans on offer and the mock vehicle register.
/// </summary>
public sealed class SiteContent
{
    /// <summary>The content file, relative to the application directory.</summary>
    public const string FileName = "Catalog/site-content.json";

    /// <summary>Gets or sets the cover copy, in display order.</summary>
    public List<CoverContent> Covers { get; set; } = [];

    /// <summary>Gets or sets the payment plans.</summary>
    public List<PaymentPlanContent> PaymentPlans { get; set; } = [];

    /// <summary>Gets or sets the demo vehicles.</summary>
    public List<Vehicle> Vehicles { get; set; } = [];

    /// <summary>Loads the content file.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The content.</returns>
    public static SiteContent Load(string path) =>
        JsonSerializer.Deserialize<SiteContent>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException($"Site content file {path} is empty");
}

/// <summary>Display copy for one cover level, keyed by SKU.</summary>
public sealed class CoverContent
{
    /// <summary>Gets or sets the SKU.</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>Gets or sets the highlight chip.</summary>
    public string Chip { get; set; } = string.Empty;

    /// <summary>Gets or sets the perks.</summary>
    public List<string> Perks { get; set; } = [];
}

/// <summary>A payment plan.</summary>
/// <param name="Id">The plan id.</param>
/// <param name="Title">The title.</param>
/// <param name="Chips">The highlight chips.</param>
public sealed record PaymentPlanContent(string Id, string Title, IReadOnlyList<string> Chips);

/// <summary>A vehicle from the mock register.</summary>
/// <param name="Rego">The registration.</param>
/// <param name="Year">The year.</param>
/// <param name="Make">The make.</param>
/// <param name="Model">The model.</param>
/// <param name="Body">The body type.</param>
/// <param name="Transmission">The transmission.</param>
/// <param name="Fuel">The fuel type.</param>
public sealed record Vehicle(string Rego, int Year, string Make, string Model, string Body, string Transmission, string Fuel)
{
    /// <summary>Gets a one-line description, for example <c>2022 Toyota Corolla</c>.</summary>
    public string Description => $"{Year} {Make} {Model}";
}
