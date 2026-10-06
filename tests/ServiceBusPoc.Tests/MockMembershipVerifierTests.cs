using ServiceBusPoc.Carwash.Services;

namespace ServiceBusPoc.Tests;

public class MockMembershipVerifierTests
{
    private readonly MockMembershipVerifier _verifier = new();

    [Theory]
    [InlineData("VALID-123")]
    [InlineData("valid-abc")]
    public async Task VerifyAsync_MembershipNumberStartingWithValid_ReturnsTrue(string membershipNumber)
    {
        var result = await _verifier.VerifyAsync(membershipNumber);

        Assert.True(result);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("INVALID-123")]
    public async Task VerifyAsync_MembershipNumberNotStartingWithValid_ReturnsFalse(string membershipNumber)
    {
        var result = await _verifier.VerifyAsync(membershipNumber);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _verifier.VerifyAsync("VALID-123", cancellationSource.Token));
    }
}
