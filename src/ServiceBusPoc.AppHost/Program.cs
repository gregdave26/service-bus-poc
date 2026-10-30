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

var commerceToolsSettings = new Dictionary<string, string>
{
    ["CommerceTools__ApiUrl"] = "http://localhost:5201",
    ["CommerceTools__ProjectKey"] = "rac-rsa-poc"
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

// Digital Site commerce services
var commerceToolsStub = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CommerceToolsStub>("commerce-tools-stub")
    .WithHttpEndpoint(port: 5201, name: "http");
ConfigureServiceBus(commerceToolsStub);

var commerceApi = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CommerceApi>("commerce-api")
    .WithHttpEndpoint(port: 5200, name: "http")
    .WithEnvironment("DigitalSite__AllowedOrigins", "http://localhost:5100,http://localhost:3000")
    .WithEnvironment(context =>
    {
        foreach (var setting in commerceToolsSettings)
        {
            context.EnvironmentVariables[setting.Key] = setting.Value;
        }
    });
ConfigureServiceBus(commerceApi);

var cartProcessor = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CartProcessor>("cart-processor")
    .WithEnvironment("ServiceBus__TopicName", "commerce.events")
    .WithEnvironment(context =>
    {
        foreach (var setting in commerceToolsSettings)
        {
            context.EnvironmentVariables[setting.Key] = setting.Value;
        }
    });
ConfigureServiceBus(cartProcessor, "cart-processor");

var fulfilmentStub = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_FulfilmentStub>("fulfilment-stub")
    .WithEnvironment("ServiceBus__TopicName", "commerce.events")
    .WithEnvironment(context =>
    {
        foreach (var setting in commerceToolsSettings)
        {
            context.EnvironmentVariables[setting.Key] = setting.Value;
        }
    });
ConfigureServiceBus(fulfilmentStub, "fulfilment-d365-stub");

builder.Build().Run();
