using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Producer.Services;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceBusMessagePublisher"/>.
/// </summary>
public sealed class ServiceBusMessagePublisherTests
{
    [Fact]
    public void Constructor_ValidOptions_Succeeds()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test==",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);

        var publisher = new ServiceBusMessagePublisher(options);

        Assert.NotNull(publisher);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ServiceBusMessagePublisher(null!));

        Assert.Equal("serviceBusOptions", exception.ParamName);
    }

    [Fact]
    public async Task PublishAsync_NullMessage_ThrowsArgumentNullException()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test==",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var publisher = new ServiceBusMessagePublisher(options);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => publisher.PublishAsync(null!, CancellationToken.None));

        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public async Task PublishAsync_EmptyConnectionString_ThrowsInvalidOperationException()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = null,
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var publisher = new ServiceBusMessagePublisher(options);
        var message = new ServiceBusMessage("test-body");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(message, CancellationToken.None));

        Assert.Contains("connection string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_WhitespaceConnectionString_ThrowsInvalidOperationException()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "   ",
            Namespace = "test-namespace",
            TopicName = "contact.events"
        };
        var options = Options.Create(settings);
        var publisher = new ServiceBusMessagePublisher(options);
        var message = new ServiceBusMessage("test-body");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(message, CancellationToken.None));

        Assert.Contains("connection string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_EmptyTopicName_ThrowsInvalidOperationException()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test==",
            Namespace = "test-namespace",
            TopicName = null
        };
        var options = Options.Create(settings);
        var publisher = new ServiceBusMessagePublisher(options);
        var message = new ServiceBusMessage("test-body");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(message, CancellationToken.None));

        Assert.Contains("topic name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishAsync_WhitespaceTopicName_ThrowsInvalidOperationException()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test==",
            Namespace = "test-namespace",
            TopicName = "  "
        };
        var options = Options.Create(settings);
        var publisher = new ServiceBusMessagePublisher(options);
        var message = new ServiceBusMessage("test-body");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(message, CancellationToken.None));

        Assert.Contains("topic name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
