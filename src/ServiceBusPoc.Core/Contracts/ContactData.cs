using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Contracts;

/// <summary>
/// Contact data with required and optional fields.
/// Represents contact information in the ContactUpdated event.
/// </summary>
public class ContactData
{
    /// <summary>
    /// Gets or sets the unique contact identifier in the source system.
    /// </summary>
    [Required(ErrorMessage = "ContactId is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "ContactId must be between 1 and 255 characters")]
    public string? ContactId { get; set; }

    /// <summary>
    /// Gets or sets the contact's first name.
    /// </summary>
    [Required(ErrorMessage = "FirstName is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "FirstName must be between 1 and 255 characters")]
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the contact's last name.
    /// </summary>
    [Required(ErrorMessage = "LastName is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "LastName must be between 1 and 255 characters")]
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the contact's email address (optional).
    /// </summary>
    [EmailAddress(ErrorMessage = "Email must be a valid email address")]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the contact's phone number (optional).
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the contact's capability and product attributes.
    /// </summary>
    public ContactAttributes? Attributes { get; set; }
}
