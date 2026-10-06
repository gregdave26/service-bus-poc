namespace ServiceBusPoc.Carwash.Api.Contracts;

/// <summary>
/// Success response contract for the member verification API.
/// </summary>
public class VerifyMemberResponse
{
    /// <summary>
    /// Indicates whether the membership number is valid.
    /// </summary>
    public bool ValidMember { get; set; }
}
