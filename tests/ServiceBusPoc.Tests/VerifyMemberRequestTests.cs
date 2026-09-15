using ServiceBusPoc.Carwash.Api.Contracts;
using System.Text.Json;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="VerifyMemberRequest"/>.
/// </summary>
public class VerifyMemberRequestTests
{
    [Fact]
    public void Constructor_SetsRacIdToEmptyString()
    {
        var request = new VerifyMemberRequest();

        Assert.Equal(string.Empty, request.RacId);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("VALID-ABC123")]
    [InlineData("INVALID-XYZ")]
    public void RacId_CanBeSet(string racId)
    {
        var request = new VerifyMemberRequest { RacId = racId };

        Assert.Equal(racId, request.RacId);
    }

    [Fact]
    public void Deserialize_WithCaseInsensitivePropertyName_SetsRacId()
    {
        const string json = """{"racid":"CASE-TEST"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var request = JsonSerializer.Deserialize<VerifyMemberRequest>(json, options);

        Assert.NotNull(request);
        Assert.Equal("CASE-TEST", request.RacId);
    }

    [Fact]
    public void SerializeAndDeserialize_PreservesRacId()
    {
        var original = new VerifyMemberRequest { RacId = "ROUNDTRIP-123" };
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<VerifyMemberRequest>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(original.RacId, deserialized.RacId);
    }
}
