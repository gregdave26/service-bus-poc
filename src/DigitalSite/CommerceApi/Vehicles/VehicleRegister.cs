using System.Text.RegularExpressions;
using ServiceBusPoc.DigitalSite.CommerceApi.Catalog;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Vehicles;

/// <summary>A mock of the vehicle register: only the demo vehicles are registered.</summary>
public sealed partial class VehicleRegister
{
    private readonly SiteContent _content;

    /// <summary>Initializes a new instance of the <see cref="VehicleRegister"/> class.</summary>
    /// <param name="content">The site content holding the demo vehicles.</param>
    public VehicleRegister(SiteContent content)
    {
        _content = content;
    }

    /// <summary>Upper-cases a registration and removes whitespace.</summary>
    /// <param name="rego">The registration.</param>
    /// <returns>The normalised registration.</returns>
    public static string Normalise(string? rego) => WhitespacePattern().Replace(rego ?? string.Empty, string.Empty).ToUpperInvariant();

    /// <summary>Gets a value indicating whether a registration is well formed.</summary>
    /// <param name="rego">The registration.</param>
    /// <returns><see langword="true"/> for 1 to 9 letters or digits.</returns>
    public static bool IsValid(string? rego) => RegoPattern().IsMatch(Normalise(rego));

    /// <summary>Finds a registered vehicle.</summary>
    /// <param name="rego">The registration.</param>
    /// <returns>The vehicle, or <see langword="null"/>.</returns>
    public Vehicle? Find(string? rego)
    {
        var normalised = Normalise(rego);
        return _content.Vehicles.FirstOrDefault(vehicle => vehicle.Rego == normalised);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex("^[A-Z0-9]{1,9}$")]
    private static partial Regex RegoPattern();
}
