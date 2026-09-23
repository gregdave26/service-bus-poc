using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Messaging.ServiceBus.Administration;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.Core.Utilities;

/// <summary>
/// Validates the Service Bus topology (topic, subscriptions, and filters).
/// </summary>
public class TopologyValidator : ITopologyValidator
{
    private readonly ServiceBusSettings _settings;
    private readonly ILogger<TopologyValidator> _logger;

    public TopologyValidator(IOptions<ServiceBusSettings> settings, ILogger<TopologyValidator> logger)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        _settings = settings.Value ?? throw new InvalidOperationException("ServiceBusSettings configuration is required");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates the emulator topology.
    /// </summary>
    /// <returns>True if topology is valid, false otherwise.</returns>
    public async Task<bool> ValidateAsync()
    {
        _logger.LogInformation("Starting topology validation...");
        _logger.LogInformation("Connection string: {ConnectionString}", MaskConnectionString(_settings.ConnectionString));
        _logger.LogInformation("Topic name: {TopicName}", _settings.TopicName);

        try
        {
            var client = new ServiceBusAdministrationClient(_settings.ConnectionString);
            if (!await client.TopicExistsAsync(_settings.TopicName))
            {
                throw new InvalidOperationException($"Topic '{_settings.TopicName}' does not exist.");
            }

            var expectedFilters = new Dictionary<string, string?>
            {
                ["digital-channels"] = null,
                ["insurance"] = "hasInsurance = true",
                ["parks-resorts"] = "hasParksResorts = true",
                ["carwash"] = "hasCarwashProduct = true"
            };

            foreach (var expected in expectedFilters)
            {
                if (!await client.SubscriptionExistsAsync(_settings.TopicName, expected.Key))
                {
                    throw new InvalidOperationException(
                        $"Subscription '{expected.Key}' does not exist on topic '{_settings.TopicName}'.");
                }

                var rules = new List<RuleProperties>();
                await foreach (var rule in client.GetRulesAsync(_settings.TopicName, expected.Key))
                {
                    rules.Add(rule);
                }

                var matchingFilter = rules.Any(rule =>
                    expected.Value is not null &&
                    rule.Filter is SqlRuleFilter sqlFilter &&
                    string.Equals(
                        NormalizeFilter(sqlFilter.SqlExpression),
                        NormalizeFilter(expected.Value),
                        StringComparison.OrdinalIgnoreCase));

                var hasUnexpectedCustomFilter = expected.Value is null &&
                    rules.Any(rule => rule.Filter is not TrueRuleFilter);

                if (expected.Value is not null && !matchingFilter)
                {
                    throw new InvalidOperationException(
                        $"Subscription '{expected.Key}' does not have filter '{expected.Value}'.");
                }

                if (hasUnexpectedCustomFilter)
                {
                    throw new InvalidOperationException(
                        $"Subscription '{expected.Key}' has an unexpected custom filter.");
                }

                _logger.LogInformation("Validated subscription {Subscription}", expected.Key);
            }

            _logger.LogInformation("Topology validation completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Topology validation failed");
            return false;
        }
    }

    private static string NormalizeFilter(string filter) =>
        string.Join(' ', filter.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Masks the connection string for safe logging.
    /// </summary>
    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "[empty]";

        if (connectionString.Contains("SharedAccessKey="))
        {
            var parts = connectionString.Split(';');
            var maskedParts = parts.Select(p =>
                p.StartsWith("SharedAccessKey=") ? "SharedAccessKey=***" : p
            );
            return string.Join(";", maskedParts);
        }

        return connectionString;
    }
}
