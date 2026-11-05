using Microsoft.Extensions.Configuration;

namespace ServiceBusPoc.AppHost;

/// <summary>
/// Resolves the opt-in AppHost's Producer defaults without changing the standalone Producer.
/// </summary>
public static class ProducerEnvironment
{
    /// <summary>
    /// Creates the Producer environment, retaining explicit routing values including false.
    /// </summary>
    /// <param name="configuration">The AppHost configuration.</param>
    /// <returns>The environment variables to supply to the Producer resource.</returns>
    public static IReadOnlyDictionary<string, string> Create(IConfiguration configuration)
    {
        string Text(string key, string fallback) =>
            string.IsNullOrWhiteSpace(configuration[key]) ? fallback : configuration[key]!;

        string Flag(string key) => configuration.GetValue(key, true) ? "true" : "false";

        return new Dictionary<string, string>
        {
            ["Producer__ContactId"] = Text("Producer:ContactId", "aspire-local-contact"),
            ["Producer__FirstName"] = Text("Producer:FirstName", "Aspire"),
            ["Producer__LastName"] = Text("Producer:LastName", "Local"),
            ["Producer__Source"] = Text("Producer:Source", "aspire-apphost"),
            ["Producer__MembershipNumber"] = Text("Producer:MembershipNumber", "ASPIRE-LOCAL-001"),
            ["Producer__HasInsurance"] = Flag("Producer:HasInsurance"),
            ["Producer__HasParksResorts"] = Flag("Producer:HasParksResorts"),
            ["Producer__HasCarwashProduct"] = Flag("Producer:HasCarwashProduct")
        };
    }
}
