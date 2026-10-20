using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.Core.Utilities;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

/// <summary>
/// Verifies the Service Bus topology and broker-side routing for all capability combinations.
/// </summary>
public sealed class VerifierService
{
    private static readonly TimeSpan ExpectedMessageWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UnexpectedMessageWait = TimeSpan.FromMilliseconds(250);
    private readonly ILogger<VerifierService> _logger;
    private readonly ServiceBusSettings _serviceBusSettings;
    private readonly ITopologyValidator _topologyValidator;
    private readonly IRoutingScenarioTransport _transport;

    public VerifierService(
        ILogger<VerifierService> logger,
        IOptions<ServiceBusSettings> serviceBusSettings,
        ITopologyValidator topologyValidator,
        IRoutingScenarioTransport transport)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceBusSettings = serviceBusSettings?.Value
            ?? throw new ArgumentNullException(nameof(serviceBusSettings));
        _topologyValidator = topologyValidator ?? throw new ArgumentNullException(nameof(topologyValidator));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <summary>
    /// Validates the topology and runs all eight routing scenarios.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifier service starting...");
        _logger.LogInformation("Service Bus namespace: {Namespace}", _serviceBusSettings.Namespace);

        if (!await _topologyValidator.ValidateAsync().ConfigureAwait(false))
        {
            _logger.LogError("Topology validation failed");
            throw new InvalidOperationException("Topology validation failed.");
        }

        foreach (var scenario in RoutingScenario.All)
        {
            await RunScenarioAsync(scenario, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("All {ScenarioCount} routing scenarios passed", RoutingScenario.All.Count);
    }

    private async Task RunScenarioAsync(RoutingScenario scenario, CancellationToken cancellationToken)
    {
        var message = CreateMessage(scenario);
        await _transport.PublishAsync(message, cancellationToken).ConfigureAwait(false);

        foreach (var subscriptionName in SubscriptionNames.All)
        {
            var received = await _transport.ReceiveAsync(
                subscriptionName,
                scenario.ExpectedSubscriptions.Contains(subscriptionName)
                    ? ExpectedMessageWait
                    : UnexpectedMessageWait,
                cancellationToken).ConfigureAwait(false);

            var receivedScenarioMessage = received?.MessageId == message.MessageId;
            var expected = scenario.ExpectedSubscriptions.Contains(subscriptionName);
            if (receivedScenarioMessage != expected)
            {
                throw new InvalidOperationException(
                    $"Scenario {scenario.Description} expected subscription '{subscriptionName}' " +
                    $"{(expected ? "to receive" : "not to receive")} message '{message.MessageId}'.");
            }
        }

        _logger.LogInformation(
            "Routing scenario {Scenario} passed; expected subscriptions: {Subscriptions}",
            scenario.Description,
            string.Join(", ", scenario.ExpectedSubscriptions));
    }

    private static ServiceBusMessage CreateMessage(RoutingScenario scenario)
    {
        var eventId = Guid.NewGuid().ToString();
        var contactEvent = new ContactUpdatedEvent
        {
            ContactId = $"verifier-{eventId}",
            FirstName = "Verifier",
            LastName = "Scenario",
            Email = "verifier@example.com",
            MembershipNumber = scenario.HasCarwashProduct ? "VERIFIER-MEMBER" : null,
            Attributes = new ContactAttributes
            {
                HasInsurance = scenario.HasInsurance,
                HasParksResorts = scenario.HasParksResorts,
                HasCarwashProduct = scenario.HasCarwashProduct
            }
        };

        Validator.ValidateObject(contactEvent, new ValidationContext(contactEvent), true);
        var envelope = new EventEnvelope<ContactUpdatedEvent>
        {
            Id = eventId,
            Type = "contact.updated",
            Source = "scenario-verifier",
            Timestamp = DateTime.UtcNow,
            DataVersion = "1",
            CorrelationId = eventId,
            Data = contactEvent
        };
        Validator.ValidateObject(envelope, new ValidationContext(envelope), true);

        var message = new ServiceBusMessage(
            JsonSerializer.Serialize(envelope, JsonSerializerOptionsHelper.DefaultOptions))
        {
            MessageId = eventId,
            CorrelationId = eventId,
            Subject = "contact.updated",
            ContentType = "application/json"
        };
        message.ApplicationProperties["hasInsurance"] = scenario.HasInsurance;
        message.ApplicationProperties["hasParksResorts"] = scenario.HasParksResorts;
        message.ApplicationProperties["hasCarwashProduct"] = scenario.HasCarwashProduct;
        return message;
    }

    private sealed record RoutingScenario(
        bool HasInsurance,
        bool HasParksResorts,
        bool HasCarwashProduct)
    {
        public string Description =>
            $"insurance={HasInsurance}, parks-resorts={HasParksResorts}, carwash={HasCarwashProduct}";

        public IReadOnlyList<string> ExpectedSubscriptions =>
            SubscriptionRouting.ExpectedSubscriptions(new ContactAttributes
            {
                HasInsurance = HasInsurance,
                HasParksResorts = HasParksResorts,
                HasCarwashProduct = HasCarwashProduct
            });

        public static IReadOnlyList<RoutingScenario> All { get; } =
        [
            new(false, false, false),
            new(false, false, true),
            new(false, true, false),
            new(false, true, true),
            new(true, false, false),
            new(true, false, true),
            new(true, true, false),
            new(true, true, true)
        ];
    }
}
