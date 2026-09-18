using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="TopologyValidator"/>.
/// Tests constructor validation, async operations, and connection string masking.
/// </summary>
public sealed class TestTopologyValidator
{
    [Fact]
    public void Constructor_ValidSettings_Succeeds()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123==",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var logger = new Mock<ILogger<TopologyValidator>>();

        var validator = new TopologyValidator(options, logger.Object);

        Assert.NotNull(validator);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var logger = new Mock<ILogger<TopologyValidator>>();

        var exception = Assert.Throws<ArgumentNullException>(
            () => new TopologyValidator(null!, logger.Object));

        Assert.Equal("settings", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);

        var exception = Assert.Throws<ArgumentNullException>(
            () => new TopologyValidator(options, null!));

        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullSettingsValue_ThrowsInvalidOperationException()
    {
        var options = new Mock<IOptions<ServiceBusSettings>>();
        options.Setup(o => o.Value).Returns((ServiceBusSettings)null!);
        var logger = new Mock<ILogger<TopologyValidator>>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new TopologyValidator(options.Object, logger.Object));

        Assert.Contains("ServiceBusSettings configuration is required", exception.Message);
    }

    [Fact]
    public async Task ValidateAsync_LogsInitialInformation()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123==",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var logger = new Mock<ILogger<TopologyValidator>>();
        var validator = new TopologyValidator(options, logger.Object);

        await validator.ValidateAsync();

        // Verify initial logging
        logger.Verify(
            candidate => candidate.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Starting topology validation", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_LogsConnectionString()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123==",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var logger = new Mock<ILogger<TopologyValidator>>();
        var validator = new TopologyValidator(options, logger.Object);

        await validator.ValidateAsync();

        // Verify connection string is logged (masked)
        logger.Verify(
            candidate => candidate.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Connection string", StringComparison.Ordinal) &&
                    !state.ToString()!.Contains("abc123", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_LogsTopicName()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123==",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var logger = new Mock<ILogger<TopologyValidator>>();
        var validator = new TopologyValidator(options, logger.Object);

        await validator.ValidateAsync();

        // Verify topic name is logged
        logger.Verify(
            candidate => candidate.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Topic name: contact.events", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests MaskConnectionString with various inputs using reflection to access the private method.
    /// </summary>
    [Theory]
    [InlineData(null, "[empty]")]
    [InlineData("", "[empty]")]
    [InlineData("Endpoint=sb://localhost/", "Endpoint=sb://localhost/")]
    [InlineData(
        "Endpoint=sb://localhost/;SharedAccessKeyName=key1;SharedAccessKey=secret123==",
        "Endpoint=sb://localhost/;SharedAccessKeyName=key1;SharedAccessKey=***")]
    public void MaskConnectionString_VariousInputs_MasksCorrectly(string? input, string expected)
    {
        // Use reflection to test the private MaskConnectionString method
        var method = typeof(TopologyValidator).GetMethod(
            "MaskConnectionString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, new object?[] { input });

        Assert.Equal(expected, result);
    }

    [Fact]
    public void MaskConnectionString_WithMultipleSemicolons_HandlesCorrectly()
    {
        // Use reflection to test the private MaskConnectionString method
        var method = typeof(TopologyValidator).GetMethod(
            "MaskConnectionString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var input = "Endpoint=sb://test/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123def456ghi789==;EntityPath=mytopic";
        var result = method!.Invoke(null, new object?[] { input });
        var resultStr = result?.ToString() ?? "";

        // Verify the key is masked
        Assert.DoesNotContain("abc123def456ghi789", resultStr);
        Assert.Contains("SharedAccessKey=***", resultStr);
        // Other parts should remain intact
        Assert.Contains("Endpoint=sb://test/", resultStr);
        Assert.Contains("EntityPath=mytopic", resultStr);
    }

    [Fact]
    public void MaskConnectionString_NoSharedAccessKey_ReturnsUnmodified()
    {
        // Use reflection to test the private MaskConnectionString method
        var method = typeof(TopologyValidator).GetMethod(
            "MaskConnectionString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var input = "Endpoint=sb://localhost/;EntityPath=contact.events";
        var result = method!.Invoke(null, new object?[] { input });

        Assert.Equal(input, result);
    }

    [Fact]
    public async Task ValidateAsync_EmptyConnectionString_ReturnsFalse()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var logger = new Mock<ILogger<TopologyValidator>>();
        var validator = new TopologyValidator(options, logger.Object);

        var result = await validator.ValidateAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_InvalidConnectionString_ReturnsFalse()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "not-a-valid-connection-string",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var logger = new Mock<ILogger<TopologyValidator>>();
        var validator = new TopologyValidator(options, logger.Object);

        var result = await validator.ValidateAsync();

        Assert.False(result);
    }
}
