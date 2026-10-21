using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Messaging.ServiceBus.Administration;
using ServiceBusPoc.Core.Configuration;
using System.Text.Json;

namespace ServiceBusPoc.Core.Utilities;

/// <summary>
/// Validates the Service Bus topology (topic, subscriptions, and filters).
/// </summary>
public class TopologyValidator : ITopologyValidator
{
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);
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
            using var validationTimeout = new CancellationTokenSource(ValidationTimeout);
            var cancellationToken = validationTimeout.Token;

            if (IsDevelopmentEmulator(_settings.ConnectionString))
            {
                _logger.LogWarning(
                    "Service Bus emulator detected; validating the declarative topology configuration because administration APIs are unavailable.");
                return await ValidateEmulatorTopologyAsync(cancellationToken);
            }

            var client = new ServiceBusAdministrationClient(_settings.ConnectionString);
            if (!await client.TopicExistsAsync(_settings.TopicName, cancellationToken))
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
                if (!await client.SubscriptionExistsAsync(_settings.TopicName, expected.Key, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Subscription '{expected.Key}' does not exist on topic '{_settings.TopicName}'.");
                }

                var rules = new List<RuleProperties>();
                await foreach (var rule in client.GetRulesAsync(_settings.TopicName, expected.Key, cancellationToken))
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

    private async Task<bool> ValidateEmulatorTopologyAsync(CancellationToken cancellationToken)
    {
        var configPath = _settings.TopologyConfigPath;
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new InvalidOperationException(
                "TopologyConfigPath is required when validating the Service Bus emulator.");
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("The emulator topology configuration was not found.", configPath);
        }

        await using var stream = File.OpenRead(configPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var topic = document.RootElement
            .GetProperty("UserConfig")
            .GetProperty("Namespaces")
            .EnumerateArray()
            .Single(namespaceElement =>
                string.Equals(
                    namespaceElement.GetProperty("Name").GetString(),
                    _settings.Namespace,
                    StringComparison.OrdinalIgnoreCase))
            .GetProperty("Topics")
            .EnumerateArray()
            .Single(topicElement =>
                string.Equals(
                    topicElement.GetProperty("Name").GetString(),
                    _settings.TopicName,
                    StringComparison.OrdinalIgnoreCase));

        var expectedFilters = new Dictionary<string, string?>
        {
            ["digital-channels"] = null,
            ["insurance"] = "hasInsurance = true",
            ["parks-resorts"] = "hasParksResorts = true",
            ["carwash"] = "hasCarwashProduct = true"
        };

        var subscriptions = topic.GetProperty("Subscriptions").EnumerateArray()
            .ToDictionary(
                subscription => subscription.GetProperty("Name").GetString()!,
                StringComparer.OrdinalIgnoreCase);

        foreach (var expected in expectedFilters)
        {
            if (!subscriptions.TryGetValue(expected.Key, out var subscription))
            {
                throw new InvalidOperationException(
                    $"Subscription '{expected.Key}' does not exist in emulator topology configuration.");
            }

            var rules = subscription.TryGetProperty("Rules", out var rulesElement)
                ? rulesElement.EnumerateArray().ToArray()
                : [];

            if (expected.Value is null)
            {
                if (rules.Length != 0)
                {
                    throw new InvalidOperationException(
                        $"Subscription '{expected.Key}' must not have a custom filter.");
                }
            }
            else
            {
                var matchingFilter = rules.Any(rule =>
                    rule.GetProperty("Properties")
                        .GetProperty("SqlFilter")
                        .GetProperty("SqlExpression")
                        .GetString() is { } expression &&
                    string.Equals(
                        NormalizeFilter(expression),
                        NormalizeFilter(expected.Value),
                        StringComparison.OrdinalIgnoreCase));

                if (!matchingFilter)
                {
                    throw new InvalidOperationException(
                        $"Subscription '{expected.Key}' does not have filter '{expected.Value}'.");
                }
            }

            _logger.LogInformation("Validated emulator subscription {Subscription}", expected.Key);
        }

        _logger.LogInformation(
            "Validated emulator topology configuration at {TopologyConfigPath}",
            configPath);
        return true;
    }

    private static bool IsDevelopmentEmulator(string? connectionString) =>
        connectionString?.Split(';')
            .Any(part => string.Equals(
                part.Trim(),
                "UseDevelopmentEmulator=true",
                StringComparison.OrdinalIgnoreCase)) == true;

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
