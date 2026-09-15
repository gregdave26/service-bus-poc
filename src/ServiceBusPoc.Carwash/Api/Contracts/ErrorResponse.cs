namespace ServiceBusPoc.Carwash.Api.Contracts;

/// <summary>
/// Error response contract for API validation failures.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Validation or error messages.
    /// </summary>
    public required List<string> Errors { get; set; }
}
