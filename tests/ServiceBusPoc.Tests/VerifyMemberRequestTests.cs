using ServiceBusPoc.Carwash.Api.Contracts;
using System.Text.Json;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="VerifyMemberRequest"/>.
/// </summary>
public class VerifyMemberRequestTests
{
    [Fact]
    public void Constructor_SetsMembershipNumberToEmptyString()
    {
        var request = new VerifyMemberRequest();

        Assert.Equal(string.Empty, request.MembershipNumber);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("VALID-ABC123")]
    [InlineData("INVALID-XYZ")]
    public void MembershipNumber_CanBeSet(string membershipNumber)
    {
        var request = new VerifyMemberRequest { MembershipNumber = membershipNumber };

        Assert.Equal(membershipNumber, request.MembershipNumber);
    }

    [Fact]
    public void Deserialize_WithCaseInsensitivePropertyName_SetsMembershipNumber()
    {
        const string json = """{"MEMBERSHIPNUMBER":"CASE-TEST"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var request = JsonSerializer.Deserialize<VerifyMemberRequest>(json, options);

        Assert.NotNull(request);
        Assert.Equal("CASE-TEST", request.MembershipNumber);
    }

    [Fact]
    public void SerializeAndDeserialize_PreservesMembershipNumber()
    {
        var original = new VerifyMemberRequest { MembershipNumber = "ROUNDTRIP-123" };
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<VerifyMemberRequest>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(original.MembershipNumber, deserialized.MembershipNumber);
    }
}
