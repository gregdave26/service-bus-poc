namespace ServiceBusPoc.Carwash.Services;

/// <summary>
/// Deterministic membership verifier for local development and MVP tests.
/// </summary>
public sealed class MockMembershipVerifier : IMembershipVerifier
{
    /// <inheritdoc />
    public Task<bool> VerifyAsync(string membershipNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isValid = membershipNumber.StartsWith("VALID", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(isValid);
    }
}
