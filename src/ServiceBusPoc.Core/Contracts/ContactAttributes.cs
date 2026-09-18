namespace ServiceBusPoc.Core.Contracts;

/// <summary>
/// Contact capability and product holding flags for subscription filtering.
/// </summary>
public class ContactAttributes
{
    /// <summary>
    /// Gets or sets a value indicating whether the contact has insurance products or holdings.
    /// </summary>
    public bool HasInsurance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the contact has Parks & Resorts products or holdings.
    /// </summary>
    public bool HasParksResorts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the contact has Carwash products or memberships.
    /// </summary>
    public bool HasCarwashProduct { get; set; }

    /// <summary>
    /// Gets or sets the RAC (Roadside Assistance Company) ID for the contact.
    /// </summary>
    public string? RacId { get; set; }
}
