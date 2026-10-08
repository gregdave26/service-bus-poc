namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>A postal address; digital products only need the country.</summary>
public sealed record Address
{
    /// <summary>Gets the ISO 3166 country code.</summary>
    public string Country { get; init; } = string.Empty;
}
