using ServiceBusPoc.Carwash.Api.Contracts;
using System.Text.Json;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="VerifyMemberResponse"/>.
/// </summary>
public class VerifyMemberResponseTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidMember_CanBeSet(bool value)
    {
        var response = new VerifyMemberResponse { ValidMember = value };

        Assert.Equal(value, response.ValidMember);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SerializeAndDeserialize_PreservesValidMember(bool value)
    {
        var original = new VerifyMemberResponse { ValidMember = value };
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<VerifyMemberResponse>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(value, deserialized.ValidMember);
    }

    [Fact]
    public void Deserialize_WithCaseInsensitivePropertyName_SetsValidMember()
    {
        const string json = """{"VALIDMEMBER":false}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var response = JsonSerializer.Deserialize<VerifyMemberResponse>(json, options);

        Assert.NotNull(response);
        Assert.False(response.ValidMember);
    }
}
