namespace ServiceBusPoc.Carwash.Services;

/// <summary>
/// Verifies a membership number against the configured membership source.
/// </summary>
public interface IMembershipVerifier
{
    /// <summary>
    /// Verifies whether a membership number is currently valid.
    /// </summary>
    Task<bool> VerifyAsync(string membershipNumber, CancellationToken cancellationToken = default);
}
