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
    public void RacIdStartingWithValid_IsAccepted(string racId)
    {
        var isValid = racId.StartsWith("VALID", StringComparison.OrdinalIgnoreCase);

        Assert.True(isValid);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("INVALID-12345")]
    [InlineData("X-VALID-12345")]
    [InlineData("NOTVALID")]
    public void RacIdNotStartingWithValid_IsRejected(string racId)
    {
        var isValid = racId.StartsWith("VALID", StringComparison.OrdinalIgnoreCase);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void EmptyRacId_IsDetected(string? racId)
    {
        Assert.True(string.IsNullOrWhiteSpace(racId));
    }
}
