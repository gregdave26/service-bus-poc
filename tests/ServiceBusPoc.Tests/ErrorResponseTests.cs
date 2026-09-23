using ServiceBusPoc.Carwash.Api.Contracts;
using System.Text.Json;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="ErrorResponse"/>.
/// </summary>
public class ErrorResponseTests
{
    [Fact]
    public void Errors_CanContainMultipleMessages()
    {
        var response = new ErrorResponse
        {
            Errors = ["Error 1", "Error 2", "Error 3"]
        };

        Assert.Equal(["Error 1", "Error 2", "Error 3"], response.Errors);
    }

    [Fact]
    public void Errors_CanBeEmpty()
    {
        var response = new ErrorResponse { Errors = [] };

        Assert.Empty(response.Errors);
    }

    [Fact]
    public void SerializeAndDeserialize_PreservesErrors()
    {
        var original = new ErrorResponse
        {
            Errors = ["'Membership number' must not be empty.", "Additional error"]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ErrorResponse>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Errors, deserialized.Errors);
    }

    [Fact]
    public void Deserialize_WithCaseInsensitivePropertyName_SetsErrors()
    {
        const string json = """{"errors":["Test error"]}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var response = JsonSerializer.Deserialize<ErrorResponse>(json, options);

        Assert.NotNull(response);
        Assert.Equal(["Test error"], response.Errors);
    }
}
