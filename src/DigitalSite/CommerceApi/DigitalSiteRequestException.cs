namespace ServiceBusPoc.DigitalSite.CommerceApi;

/// <summary>A request the API rejects with a message the shopper can read.</summary>
public sealed class DigitalSiteRequestException : Exception
{
    private DigitalSiteRequestException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>Gets the HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>Creates a 400 Bad Request error.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The exception.</returns>
    public static DigitalSiteRequestException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);

    /// <summary>Creates a 404 Not Found error.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The exception.</returns>
    public static DigitalSiteRequestException NotFound(string message) => new(StatusCodes.Status404NotFound, message);

    /// <summary>Creates a 409 Conflict error.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The exception.</returns>
    public static DigitalSiteRequestException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
}
