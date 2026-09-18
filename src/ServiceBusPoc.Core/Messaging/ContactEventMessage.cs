namespace ServiceBusPoc.Core.Messaging;

/// <summary>
/// Canonical constants for <c>contact.events</c> messages.
/// The application property names must stay identical to the SQL filter expressions
/// declared in the broker topology (<c>infra/servicebus/config.json</c> and the Bicep templates),
/// because subscription routing is evaluated by the broker and never in consumer code.
/// </summary>
public static class ContactEventMessage
{
    /// <summary>The envelope type for contact update events.</summary>
    public const string ContactUpdatedType = "contact.updated";

    /// <summary>The envelope data version published by this POC.</summary>
    public const string ContactUpdatedDataVersion = "1.0";

    /// <summary>The content type applied to published messages.</summary>
    public const string ContentType = "application/json";

    /// <summary>Application property carrying the insurance capability flag.</summary>
    public const string HasInsuranceProperty = "hasInsurance";

    /// <summary>Application property carrying the Parks &amp; Resorts capability flag.</summary>
    public const string HasParksResortsProperty = "hasParksResorts";

    /// <summary>Application property carrying the Carwash product flag.</summary>
    public const string HasCarwashProductProperty = "hasCarwashProduct";
}
