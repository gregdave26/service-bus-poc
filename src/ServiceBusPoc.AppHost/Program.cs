using Microsoft.Extensions.Configuration;
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var serviceBusConnectionString = builder.Configuration["ServiceBus:ConnectionString"]
    ?? throw new InvalidOperationException(
        "ServiceBus:ConnectionString must be supplied through the Aspire host environment.");
var serviceBusNamespace = builder.Configuration["ServiceBus:Namespace"]
    ?? throw new InvalidOperationException(
        "ServiceBus:Namespace must be supplied through the Aspire host environment.");
var serviceBusTopic = builder.Configuration["ServiceBus:TopicName"]
    ?? throw new InvalidOperationException(
        "ServiceBus:TopicName must be supplied through the Aspire host environment.");

var commonSettings = new Dictionary<string, string>
{
    ["ServiceBus__ConnectionString"] = serviceBusConnectionString,
    ["ServiceBus__Namespace"] = serviceBusNamespace,
    ["ServiceBus__TopicName"] = serviceBusTopic
};

void ConfigureServiceBus(IResourceBuilder<ProjectResource> resource, string? subscriptionName = null)
{
    resource
        .WithEnvironment(context =>
        {
            foreach (var setting in commonSettings)
            {
                context.EnvironmentVariables[setting.Key] = setting.Value;
            }

            if (subscriptionName is not null)
            {
                context.EnvironmentVariables["ServiceBus__SubscriptionName"] = subscriptionName;
            }
        });
}

var digitalChannels = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Consumers_DigitalChannels>(
    "digital-channels");
ConfigureServiceBus(digitalChannels, "digital-channels");

var insurance = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Consumers_Insurance>("insurance");
ConfigureServiceBus(insurance, "insurance");

var parksResorts = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Consumers_ParksResorts>(
    "parks-resorts");
ConfigureServiceBus(parksResorts, "parks-resorts");

var carwash = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Consumers_Carwash>("carwash");
ConfigureServiceBus(carwash, "carwash");

var producer = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Producer>("producer");
ConfigureServiceBus(producer);
producer.WithEnvironment(context =>
{
    static string GetProducerSetting(IConfiguration configuration, string key, string fallback) =>
        string.IsNullOrWhiteSpace(configuration[key]) ? fallback : configuration[key]!;

    var producerSettings = new Dictionary<string, string>
    {
        ["Producer__ContactId"] = GetProducerSetting(builder.Configuration, "Producer:ContactId", "aspire-local-contact"),
        ["Producer__FirstName"] = GetProducerSetting(builder.Configuration, "Producer:FirstName", "Aspire"),
        ["Producer__LastName"] = GetProducerSetting(builder.Configuration, "Producer:LastName", "Local"),
        ["Producer__Source"] = GetProducerSetting(builder.Configuration, "Producer:Source", "aspire-apphost"),
        ["Producer__MembershipNumber"] = GetProducerSetting(builder.Configuration, "Producer:MembershipNumber", "ASPIRE-LOCAL-001"),
        ["Producer__HasInsurance"] = "true",
        ["Producer__HasParksResorts"] = "true",
        ["Producer__HasCarwashProduct"] = "true"
    };

    foreach (var setting in producerSettings)
    {
        context.EnvironmentVariables[setting.Key] = setting.Value;
    }
});

var verifier = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Verifier>("verifier")
    .WithArgs("--connectivity-probe");
ConfigureServiceBus(verifier);

builder.Build().Run();
