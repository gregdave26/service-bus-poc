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

var verifier = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Verifier>("verifier")
    .WithArgs("--connectivity-probe");
ConfigureServiceBus(verifier);

builder.Build().Run();
