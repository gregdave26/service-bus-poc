using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Configuration;

/// <summary>
/// Input settings used to construct a canonical contact.updated event.
/// </summary>
public sealed class ProducerSettings
{
    /// <summary>
    /// Gets or sets the contact identifier.
    /// </summary>
    [Required]
    public string? ContactId { get; set; }

    /// <summary>
    /// Gets or sets the contact's first name.
    /// </summary>
    [Required]
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the contact's last name.
    /// </summary>
    [Required]
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the optional contact email address.
    /// </summary>
    [EmailAddress]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the optional contact phone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the originating system.
    /// </summary>
    [Required]
    public string Source { get; set; } = "producer";

    /// <summary>
    /// Gets or sets the optional correlation identifier.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the contact has insurance.
    /// </summary>
    public bool HasInsurance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the contact has Parks &amp; Resorts products.
    /// </summary>
    public bool HasParksResorts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the contact has a Carwash product.
    /// </summary>
    public bool HasCarwashProduct { get; set; }
}
