namespace ServiceBusPoc.Carwash.Api.Contracts;

/// <summary>
/// Request contract for the Pulse member verification API endpoint.
/// </summary>
public class VerifyMemberRequest
{
    /// <summary>
    /// RAC member ID to verify against carwash product records.
    /// Must not be empty.
    /// </summary>
    public string RacId { get; set; } = string.Empty;
}
