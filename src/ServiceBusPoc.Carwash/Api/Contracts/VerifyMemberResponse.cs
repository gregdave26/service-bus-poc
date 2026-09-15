namespace ServiceBusPoc.Carwash.Api.Contracts;

/// <summary>
/// Success response contract for the member verification API.
/// </summary>
public class VerifyMemberResponse
{
    /// <summary>
    /// Indicates whether the RAC ID is a valid member with carwash product.
    /// </summary>
    public bool ValidMember { get; set; }
}
