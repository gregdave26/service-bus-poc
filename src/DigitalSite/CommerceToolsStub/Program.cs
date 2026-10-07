using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Logging;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalSite.CommerceToolsStub;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Auth;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Endpoints;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Messaging;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Seeding;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.DependencyInjection;

const string ServiceName = "commercetools-stub";

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddStructuredConsoleLogging();

builder.Services
    .AddOptions<CommerceToolsStubSettings>()
    .Bind(builder.Configuration.GetSection(CommerceToolsStubSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = StubJson.Options.PropertyNamingPolicy;
    options.SerializerOptions.DefaultIgnoreCondition = StubJson.Options.DefaultIgnoreCondition;
    options.SerializerOptions.AllowOutOfOrderMetadataProperties = true;
});

builder.Services
    .AddServiceBusConfiguration(builder.Configuration)
    .AddDashboardConfiguration(builder.Configuration)
    .AddDashboardHeartbeat(ServiceName)
    .AddServiceBusClient();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IServiceBusSender, ServiceBusSenderAdapter>();
builder.Services.AddSingleton<IResourceStore, SqliteResourceStore>();
builder.Services.AddSingleton<StubWriteLock>();
builder.Services.AddSingleton<MessageFactory>();
builder.Services.AddSingleton<IOutboxSignal, OutboxSignal>();
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddSingleton<CartService>();
builder.Services.AddSingleton<PaymentService>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<StubTokenService>();
builder.Services.AddSingleton<ICommerceMessagePublisher, ServiceBusCommerceMessagePublisher>();
builder.Services.AddHostedService<CatalogSeeder>();
builder.Services.AddSingleton<OutboxDispatcher>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<OutboxDispatcher>());

var app = builder.Build();
app.MapCommerceEndpoints();
await app.RunAsync();
