using ServiceBusPoc.DigitalSite.Shared.CommerceTools;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>A request the stub rejects with a commercetools-style error.</summary>
public sealed class CommerceStubException : Exception
{
    private CommerceStubException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>Gets the HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>Gets the commercetools error code.</summary>
    public string Code { get; }

    /// <summary>The resource was not found.</summary>
    public static CommerceStubException NotFound(string typeId, string id) =>
        new(StatusCodes.Status404NotFound, CommerceToolsErrorCodes.ResourceNotFound, $"The Resource with ID '{id}' of type '{typeId}' was not found.");

    /// <summary>The expected version did not match.</summary>
    public static CommerceStubException ConcurrentModification(long expected, long actual) =>
        new(StatusCodes.Status409Conflict, CommerceToolsErrorCodes.ConcurrentModification, $"Object has a different version than expected. Expected: {expected} - Actual: {actual}.");

    /// <summary>The request was invalid.</summary>
    public static CommerceStubException InvalidInput(string message) =>
        new(StatusCodes.Status400BadRequest, CommerceToolsErrorCodes.InvalidInput, message);

    /// <summary>The operation is not allowed in the current state.</summary>
    public static CommerceStubException InvalidOperation(string message) =>
        new(StatusCodes.Status400BadRequest, CommerceToolsErrorCodes.InvalidOperation, message);

    /// <summary>A unique value is already used.</summary>
    public static CommerceStubException DuplicateField(string field, string value) =>
        new(StatusCodes.Status400BadRequest, CommerceToolsErrorCodes.DuplicateField, $"A duplicate value '\"{value}\"' exists for field '{field}'.");

    /// <summary>The bearer token was missing or invalid.</summary>
    public static CommerceStubException Unauthorized() =>
        new(StatusCodes.Status401Unauthorized, CommerceToolsErrorCodes.InvalidToken, "invalid_token");
}
