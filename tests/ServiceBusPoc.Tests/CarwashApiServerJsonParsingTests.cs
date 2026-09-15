using ServiceBusPoc.Carwash.Api.Contracts;
using System.Text.Json;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for JSON parsing used by the Carwash verification API.
/// </summary>
public class CarwashApiServerJsonParsingTests
{
    [Fact]
    public void Deserialize_ValidRequest_SetsRacId()
    {
        const string json = """{"RacId":"TEST-123"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var request = JsonSerializer.Deserialize<VerifyMemberRequest>(json, options);

        Assert.NotNull(request);
        Assert.Equal("TEST-123", request.RacId);
    }

    [Fact]
    public void Deserialize_MalformedRequest_ThrowsJsonException()
    {
        const string json = "{invalid}";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<VerifyMemberRequest>(json, options));
    }

    [Fact]
    public void Deserialize_RequestWithExtraFields_IgnoresUnknownFields()
    {
        const string json = """{"RacId":"TEST-456","ExtraField":"ignored"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var request = JsonSerializer.Deserialize<VerifyMemberRequest>(json, options);

        Assert.NotNull(request);
        Assert.Equal("TEST-456", request.RacId);
    }

    [Fact]
    public void Deserialize_RequestWithoutRacId_UsesDefaultValue()
    {
        const string json = """{}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var request = JsonSerializer.Deserialize<VerifyMemberRequest>(json, options);

        Assert.NotNull(request);
        Assert.Equal(string.Empty, request.RacId);
    }
}
