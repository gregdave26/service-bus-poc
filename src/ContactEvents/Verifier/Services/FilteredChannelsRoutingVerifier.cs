using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;

namespace ServiceBusPoc.ContactEvents.Verifier.Services;

public sealed class FilteredChannelsRoutingVerifier(
    IFilteredChannelsRoutingTransport transport,
    ILogger<FilteredChannelsRoutingVerifier> logger) : IFilteredChannelsRoutingVerifier
{
    private static readonly TimeSpan ReceiveWaitTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan VerificationTimeout = TimeSpan.FromSeconds(60);

    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        var scenarios = CreateScenarios();
        foreach (var scenario in scenarios)
        {
            await transport.PublishAsync(
                CreateContact(scenario),
                scenario.CorrelationId,
                cancellationToken);
        }

        await VerifySubscriptionAsync(
            SubscriptionNames.Insurance,
            scenarios.Where(scenario => scenario.HasInsurance),
            cancellationToken);
        await VerifySubscriptionAsync(
            SubscriptionNames.ParksResorts,
            scenarios.Where(scenario => scenario.HasParksResorts),
            cancellationToken);
    }

    private async Task VerifySubscriptionAsync(
        string subscriptionName,
        IEnumerable<RoutingScenario> expectedScenarios,
        CancellationToken cancellationToken)
    {
        var expectedCorrelationIds = expectedScenarios
            .Select(scenario => scenario.CorrelationId)
            .ToHashSet(StringComparer.Ordinal);
        var deadline = DateTimeOffset.UtcNow.Add(VerificationTimeout);

        while (expectedCorrelationIds.Count > 0 && DateTimeOffset.UtcNow < deadline)
        {
            var message = await transport.ReceiveAsync(
                subscriptionName,
                ReceiveWaitTime,
                cancellationToken);
            if (message is null)
            {
                continue;
            }

            if (message.CorrelationId is null)
            {
                throw new InvalidOperationException(
                    $"Subscription '{subscriptionName}' received a message without a correlation ID.");
            }

            if (!message.CorrelationId.StartsWith("filtered-", StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Subscription {Subscription} discarded a message from an earlier routing phase {CorrelationId}",
                    subscriptionName,
                    message.CorrelationId);
                continue;
            }

            if (!expectedCorrelationIds.Remove(message.CorrelationId))
            {
                throw new InvalidOperationException(
                    $"Subscription '{subscriptionName}' received unexpected current-phase routing message " +
                    $"'{message.CorrelationId}'.");
            }

            logger.LogInformation(
                "Subscription {Subscription} received routing scenario {CorrelationId}",
                subscriptionName,
                message.CorrelationId);
        }

        if (expectedCorrelationIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Subscription '{subscriptionName}' did not receive {expectedCorrelationIds.Count} expected routing message(s): " +
                string.Join(", ", expectedCorrelationIds));
        }

        logger.LogInformation(
            "Subscription {Subscription} routing validation completed successfully for {ScenarioCount} scenarios",
            subscriptionName,
            expectedScenarios.Count());
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
            ContactId = $"filtered-{scenario.CorrelationId}",
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
            $"filtered-{HasInsurance}-{HasParksResorts}-{HasCarwashProduct}";
    }
}
