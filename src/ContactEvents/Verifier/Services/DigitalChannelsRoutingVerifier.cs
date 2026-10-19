using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Contracts;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

public sealed class DigitalChannelsRoutingVerifier(
    IDigitalChannelsRoutingTransport transport,
    ILogger<DigitalChannelsRoutingVerifier> logger) : IDigitalChannelsRoutingVerifier
{
    private static readonly TimeSpan ReceiveWaitTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan VerificationTimeout = TimeSpan.FromSeconds(60);

    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        var scenarios = CreateScenarios();
        var expectedCorrelationIds = scenarios
            .Select(scenario => scenario.CorrelationId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var scenario in scenarios)
        {
            await transport.PublishAsync(
                CreateContact(scenario),
                scenario.CorrelationId,
                cancellationToken);
        }

        var deadline = DateTimeOffset.UtcNow.Add(VerificationTimeout);
        while (expectedCorrelationIds.Count > 0 && DateTimeOffset.UtcNow < deadline)
        {
            var message = await transport.ReceiveAsync(ReceiveWaitTime, cancellationToken);
            if (message is null)
            {
                continue;
            }

            try
            {
                if (message.CorrelationId is not null &&
                    expectedCorrelationIds.Remove(message.CorrelationId))
                {
                    logger.LogInformation(
                        "Digital-channels received routing scenario {CorrelationId}",
                        message.CorrelationId);
                }
                else
                {
                    logger.LogWarning(
                        "Digital-channels received an unexpected message {MessageId}",
                        message.MessageId);
                }
            }
            finally
            {
                await transport.CompleteAsync(message, cancellationToken);
            }
        }

        if (expectedCorrelationIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Digital-channels did not receive {expectedCorrelationIds.Count} routing scenario message(s): " +
                string.Join(", ", expectedCorrelationIds));
        }

        logger.LogInformation(
            "Digital-channels routing validation completed successfully for {ScenarioCount} scenarios",
            scenarios.Count);
    }

    private static List<RoutingScenario> CreateScenarios() =>
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

    private static ContactData CreateContact(RoutingScenario scenario) =>
        new()
        {
            ContactId = $"routing-{scenario.CorrelationId}",
            FirstName = "Routing",
            LastName = "Scenario",
            MembershipNumber = scenario.HasCarwashProduct ? "ROUTING-MEMBER" : null,
            Attributes = new ContactAttributes
            {
                HasInsurance = scenario.HasInsurance,
                HasParksResorts = scenario.HasParksResorts,
                HasCarwashProduct = scenario.HasCarwashProduct
            }
        };

    private sealed record RoutingScenario(
        bool HasInsurance,
        bool HasParksResorts,
        bool HasCarwashProduct)
    {
        public string CorrelationId =>
            $"digital-channels-{HasInsurance}-{HasParksResorts}-{HasCarwashProduct}";
    }
}
