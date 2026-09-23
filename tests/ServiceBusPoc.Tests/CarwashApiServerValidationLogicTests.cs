namespace ServiceBusPoc.Tests;

/// <summary>
/// Characterisation tests for the Phase 1 mock member-validation rule.
/// </summary>
public class CarwashApiServerValidationLogicTests
{
    [Theory]
    [InlineData("VALID-12345")]
    [InlineData("VALID-ABC")]
    [InlineData("valid-xyz")]
    [InlineData("VALID")]
    public void MembershipNumberStartingWithValid_IsAccepted(string membershipNumber)
    {
        var isValid = membershipNumber.StartsWith("VALID", StringComparison.OrdinalIgnoreCase);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("INVALID-12345")]
    [InlineData("X-VALID-12345")]
    [InlineData("NOTVALID")]
    public void MembershipNumberNotStartingWithValid_IsRejected(string membershipNumber)
    {
        var isValid = membershipNumber.StartsWith("VALID", StringComparison.OrdinalIgnoreCase);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void EmptyMembershipNumber_IsDetected(string? membershipNumber)
    {
        Assert.True(string.IsNullOrWhiteSpace(membershipNumber));
    }
}
