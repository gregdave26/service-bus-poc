using Microsoft.Extensions.Configuration;
using Aspire.Hosting;
using ServiceBusPoc.AppHost;

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

void ConfigureServiceBus(
    IResourceBuilder<ProjectResource> resource,
    string? subscriptionName = null,
    string? topicName = null)
{
    resource
        .WithEnvironment(context =>
        {
            foreach (var setting in commonSettings)
            {
                context.EnvironmentVariables[setting.Key] = setting.Value;
            }

            if (topicName is not null)
            {
                context.EnvironmentVariables["ServiceBus__TopicName"] = topicName;
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
    foreach (var setting in ProducerEnvironment.Create(builder.Configuration))
    {
        context.EnvironmentVariables[setting.Key] = setting.Value;
    }
});

var verifier = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Verifier>("verifier")
    .WithArgs("--connectivity-probe");
ConfigureServiceBus(verifier);

const string commerceEventsTopic = "commerce.events";
var commerceToolsProjectKey = builder.Configuration["CommerceTools:ProjectKey"] ?? "rac-rsa-poc";

var commerceToolsStub = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CommerceToolsStub>("commerce-tools-stub")
    .WithHttpEndpoint(port: 5201, name: "http");
ConfigureServiceBus(commerceToolsStub, topicName: commerceEventsTopic);

void ConfigureCommerceTools(IResourceBuilder<ProjectResource> resource)
{
    resource
        .WithEnvironment("CommerceTools__ApiUrl", commerceToolsStub.GetEndpoint("http"))
        .WithEnvironment("CommerceTools__ProjectKey", commerceToolsProjectKey);
}

var commerceApi = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CommerceApi>("commerce-api")
    .WithHttpEndpoint(port: 5200, name: "http")
    .WithEnvironment("DigitalSite__AllowedOrigins", "http://localhost:5100,http://localhost:3000");
ConfigureCommerceTools(commerceApi);
ConfigureServiceBus(commerceApi);

var cartProcessor = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CartProcessor>("cart-processor");
ConfigureCommerceTools(cartProcessor);
ConfigureServiceBus(cartProcessor, "cart-processor", commerceEventsTopic);

var fulfilmentStub = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_FulfilmentStub>("fulfilment-stub");
ConfigureCommerceTools(fulfilmentStub);
ConfigureServiceBus(fulfilmentStub, "fulfilment-d365-stub", commerceEventsTopic);

builder.Build().Run();
