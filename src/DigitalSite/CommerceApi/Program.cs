using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Logging;
using ServiceBusPoc.DigitalSite.CommerceApi.DependencyInjection;
using ServiceBusPoc.DigitalSite.CommerceApi.Endpoints;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.DependencyInjection;

const string ServiceName = "digital-site-commerce-api";

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddStructuredConsoleLogging();

var paymentGateway = await builder.AddCommerceApiAsync();
builder.Services
    .AddDashboardConfiguration(builder.Configuration)
    .AddDashboardHeartbeat(ServiceName);

var app = builder.Build();
app.UseCors(CommerceApiServiceCollectionExtensions.CorsPolicy);
app.MapDigitalSiteEndpoints(paymentGateway);
using (var scope = app.Services.CreateScope())
{
    var runtimeGateway = scope.ServiceProvider.GetRequiredService<PaymentGatewayRuntimeState>();
    app.Logger.LogInformation("Digital Site commerce API configured with the {PaymentGateway} payment gateway; startup validation has not completed", runtimeGateway.ConfiguredMode);
}
await app.RunAsync();
