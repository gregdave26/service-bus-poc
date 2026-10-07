using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Logging;
using ServiceBusPoc.DigitalSite.CommerceApi.DependencyInjection;
using ServiceBusPoc.DigitalSite.CommerceApi.Endpoints;
using ServiceBusPoc.DigitalSite.Shared.DependencyInjection;

const string ServiceName = "digital-site-commerce-api";

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddStructuredConsoleLogging();

var paymentGateway = builder.AddCommerceApi();
builder.Services
    .AddDashboardConfiguration(builder.Configuration)
    .AddDashboardHeartbeat(ServiceName);

var app = builder.Build();
app.UseCors(CommerceApiServiceCollectionExtensions.CorsPolicy);
app.MapDigitalSiteEndpoints(paymentGateway);
app.Logger.LogInformation("Digital Site commerce API started with the {PaymentGateway} payment gateway", paymentGateway);
await app.RunAsync();
