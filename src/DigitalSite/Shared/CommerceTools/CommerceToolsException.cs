using System.Net;

namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools;

/// <summary>A commercetools API call failed.</summary>
public class CommerceToolsException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CommerceToolsException"/> class.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">The error message returned by the API.</param>
    /// <param name="errorCodes">The error codes returned by the API.</param>
    public CommerceToolsException(HttpStatusCode statusCode, string message, IReadOnlyList<string> errorCodes)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCodes = errorCodes;
    }

    /// <summary>Gets the HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Gets the error codes, for example <c>InvalidOperation</c>.</summary>
    public IReadOnlyList<string> ErrorCodes { get; }
}

/// <summary>
/// The resource changed since it was read (HTTP 409 <c>ConcurrentModification</c>); re-read and retry.
/// </summary>
public sealed class ConcurrentModificationException : CommerceToolsException
{
    /// <summary>Initializes a new instance of the <see cref="ConcurrentModificationException"/> class.</summary>
    /// <param name="message">The error message returned by the API.</param>
    /// <param name="errorCodes">The error codes returned by the API.</param>
    public ConcurrentModificationException(string message, IReadOnlyList<string> errorCodes)
        : base(HttpStatusCode.Conflict, message, errorCodes)
    {
    }
}
