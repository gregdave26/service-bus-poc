using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Contracts;

/// <summary>
/// Contact.Updated event containing contact information changes.
/// Represents the data payload in an EventEnvelope for contact.updated events.
/// </summary>
public class ContactUpdatedEvent
{
    /// <summary>
    /// Gets or sets the contact data.
    /// </summary>
    [Required(ErrorMessage = "Contact data is required")]
    public ContactData? Contact { get; set; }
}
