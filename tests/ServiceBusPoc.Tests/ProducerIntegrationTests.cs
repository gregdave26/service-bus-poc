using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.Producer.Services;

namespace ServiceBusPoc.Tests;

/// <summary>
/// Integration tests for publishing messages to Service Bus.
/// Tests the complete flow from ProducerService through publisher to broker validation.
/// Skipped when running locally without emulator connection.
/// </summary>
public sealed class ProducerIntegrationTests
{
    /// <summary>
    /// Validates that publishing a well-formed message produces a valid Service Bus message.
    /// This test can run locally without external dependencies.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithValidMessage_CreatesValidServiceBusMessage()
    {
        // Arrange
        var testPublisher = new TestPublisher();
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services
            .AddLogging(builder => builder.AddConsole())
            .AddServiceBusConfiguration(configuration)
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IServiceBusMessagePublisher>(testPublisher)
            .AddTransient<ProducerService>();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ProducerService>();

        var contactEvent = new ContactUpdatedEvent
        {
            ContactId = "C-integration-001",
            FirstName = "Integration",
            LastName = "TestContact",
            Email = "integration@test.local",
            Attributes = new ContactAttributes
            {
                HasInsurance = true,
                HasParksResorts = false,
                HasCarwashProduct = true
            }
        };

        // Act
        await service.PublishAsync(contactEvent, "integration-test", "correlation-123");

        // Assert
        Assert.Single(testPublisher.PublishedMessages);
        var message = testPublisher.PublishedMessages[0];
        Assert.Equal("application/json", message.ContentType);
        Assert.Equal("contact.updated", message.Subject);
        Assert.Equal("correlation-123", message.CorrelationId);
        Assert.NotNull(message.MessageId);

        var envelope = JsonSerializer.Deserialize<EventEnvelope<ContactUpdatedEvent>>(
            message.Body,
            JsonSerializerOptionsHelper.DefaultOptions);
        Assert.NotNull(envelope);
        Assert.Equal("contact.updated", envelope.Type);
        Assert.Equal("integration-test", envelope.Source);
        Assert.Equal("1", envelope.DataVersion);
        Assert.Equal("C-integration-001", envelope.Data?.ContactId);
        Assert.True((bool)message.ApplicationProperties["hasInsurance"]);
        Assert.False((bool)message.ApplicationProperties["hasParksResorts"]);
        Assert.True((bool)message.ApplicationProperties["hasCarwashProduct"]);
    }

    /// <summary>
    /// Validates routing property combinations in published messages.
    /// Verifies all 8 boolean combinations produce correct subscription properties.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, true)]
    [InlineData(false, true, false, false, true, false)]
    [InlineData(false, true, true, false, true, true)]
    [InlineData(true, false, false, true, false, false)]
    [InlineData(true, false, true, true, false, true)]
    [InlineData(true, true, false, true, true, false)]
    [InlineData(true, true, true, true, true, true)]
    public async Task PublishAsync_AllRoutingCombinations_ProducesCorrectApplicationProperties(
        bool hasInsurance,
        bool hasParksResorts,
        bool hasCarwashProduct,
        bool expectedInsurance,
        bool expectedParksResorts,
        bool expectedCarwash)
    {
        // Arrange
        var testPublisher = new TestPublisher();
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services
            .AddLogging(builder => builder.AddConsole())
            .AddServiceBusConfiguration(configuration)
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IServiceBusMessagePublisher>(testPublisher)
            .AddTransient<ProducerService>();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ProducerService>();

        var contactEvent = new ContactUpdatedEvent
        {
            ContactId = $"C-combo-{hasInsurance}-{hasParksResorts}-{hasCarwashProduct}",
            FirstName = "Test",
            LastName = "Contact",
            Attributes = new ContactAttributes
            {
                HasInsurance = hasInsurance,
                HasParksResorts = hasParksResorts,
                HasCarwashProduct = hasCarwashProduct
            }
        };

        // Act
        await service.PublishAsync(contactEvent, "test-source", "correlation-id");

        // Assert
        Assert.Single(testPublisher.PublishedMessages);
        var message = testPublisher.PublishedMessages[0];
        Assert.Equal(expectedInsurance, message.ApplicationProperties["hasInsurance"]);
        Assert.Equal(expectedParksResorts, message.ApplicationProperties["hasParksResorts"]);
        Assert.Equal(expectedCarwash, message.ApplicationProperties["hasCarwashProduct"]);
    }

    /// <summary>
    /// Validates that multiple messages can be published sequentially.
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleMessages_PublishesAll()
    {
        // Arrange
        var testPublisher = new TestPublisher();
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();
        services
            .AddLogging(builder => builder.AddConsole())
            .AddServiceBusConfiguration(configuration)
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IServiceBusMessagePublisher>(testPublisher)
            .AddTransient<ProducerService>();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ProducerService>();

        // Act
        for (int i = 0; i < 3; i++)
        {
            var contactEvent = new ContactUpdatedEvent
            {
                ContactId = $"C-bulk-{i:D3}",
                FirstName = $"Contact{i}",
                LastName = "Test",
                Attributes = new ContactAttributes { HasInsurance = i % 2 == 0 }
            };

            await service.PublishAsync(contactEvent, "bulk-test", $"correlation-{i}");
        }

        // Assert
        Assert.Equal(3, testPublisher.PublishedMessages.Count);
        for (int i = 0; i < 3; i++)
        {
            var message = testPublisher.PublishedMessages[i];
            Assert.Equal($"correlation-{i}", message.CorrelationId);
            Assert.Equal(i % 2 == 0, message.ApplicationProperties["hasInsurance"]);
        }
    }

    private static IConfiguration BuildTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceBus:ConnectionString"] = "Endpoint=sb://localhost/",
                ["ServiceBus:Namespace"] = "sbemulatorns",
                ["ServiceBus:TopicName"] = "contact.events"
            })
            .Build();

    /// <summary>
    /// Test publisher that captures published messages without connecting to Service Bus.
    /// </summary>
    private sealed class TestPublisher : IServiceBusMessagePublisher
    {
        public List<ServiceBusMessage> PublishedMessages { get; } = [];

        public Task PublishAsync(ServiceBusMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }
    }
}

