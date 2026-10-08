namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools;

/// <summary>The commercetools error response body.</summary>
public sealed class CommerceToolsErrorResponse
{
    /// <summary>Gets or sets the HTTP status code.</summary>
    public int StatusCode { get; set; }

    /// <summary>Gets or sets the first error message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the individual errors.</summary>
    public List<CommerceToolsError> Errors { get; set; } = [];

    /// <summary>Creates a response with a single error.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The error response.</returns>
    public static CommerceToolsErrorResponse Single(int statusCode, string code, string message) =>
        new() { StatusCode = statusCode, Message = message, Errors = [new CommerceToolsError { Code = code, Message = message }] };
}

/// <summary>A single commercetools error.</summary>
public sealed class CommerceToolsError
{
    /// <summary>Gets or sets the error code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the error message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>commercetools error codes used by the POC.</summary>
public static class CommerceToolsErrorCodes
{
    /// <summary>The expected version did not match.</summary>
    public const string ConcurrentModification = "ConcurrentModification";

    /// <summary>The request body or action was invalid.</summary>
    public const string InvalidInput = "InvalidInput";

    /// <summary>The operation is not allowed in the current state.</summary>
    public const string InvalidOperation = "InvalidOperation";

    /// <summary>The resource was not found.</summary>
    public const string ResourceNotFound = "ResourceNotFound";

    /// <summary>A unique value is already used.</summary>
    public const string DuplicateField = "DuplicateField";

    /// <summary>The bearer token is missing or invalid.</summary>
    public const string InvalidToken = "invalid_token";
}
