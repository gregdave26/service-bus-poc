namespace ServiceBusPoc.Carwash.Api.Contracts;

/// <summary>
/// Request contract for the Pulse member verification API endpoint.
/// </summary>
public class VerifyMemberRequest
{
    /// <summary>
    /// Membership number to verify against the membership source.
    /// Must not be empty.
    /// </summary>
    public string MembershipNumber { get; set; } = string.Empty;
}
