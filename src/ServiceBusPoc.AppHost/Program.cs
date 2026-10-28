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
    foreach (var setting in ProducerEnvironment.Create(builder.Configuration))
    {
        context.EnvironmentVariables[setting.Key] = setting.Value;
    }
});

var verifier = builder.AddProject<Projects.ServiceBusPoc_ContactEvents_Verifier>("verifier")
    .WithArgs("--connectivity-probe");
ConfigureServiceBus(verifier);

// Digital Site commerce services
var commerceToolsStub = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CommerceToolsStub>("commerce-tools-stub")
    .WithHttpEndpoint(port: 5201, name: "http")
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5201")
    .WithEnvironment("ServiceBus__TopicName", "commerce.events");
ConfigureServiceBus(commerceToolsStub);

var commerceApi = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CommerceApi>("commerce-api")
    .WithHttpEndpoint(port: 5200, name: "http")
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5200")
    .WithEnvironment("DigitalSite__AllowedOrigins", "http://localhost:5100,http://localhost:3000");
ConfigureServiceBus(commerceApi);

var cartProcessor = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_CartProcessor>("cart-processor")
    .WithEnvironment("ServiceBus__TopicName", "commerce.events");
ConfigureServiceBus(cartProcessor, "cart-processor");

var fulfilmentStub = builder.AddProject<Projects.ServiceBusPoc_DigitalSite_FulfilmentStub>("fulfilment-stub")
    .WithEnvironment("ServiceBus__TopicName", "commerce.events");
ConfigureServiceBus(fulfilmentStub, "fulfilment-d365-stub");

builder.Build().Run();
