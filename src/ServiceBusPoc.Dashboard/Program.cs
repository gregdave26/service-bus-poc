using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Dashboard.Api;
using ServiceBusPoc.Dashboard.Server;
using ServiceBusPoc.Dashboard.Status;

var builder = Host.CreateApplicationBuilder(args);

// Load configuration from environment variables (ADR-006)
builder.Configuration.AddEnvironmentVariables();

// Register services
builder.Services.AddServiceBusConfiguration(builder.Configuration);
builder.Services.AddDashboardConfiguration(builder.Configuration);
builder.Services.AddContactEventPublishing();
builder.Services.AddDashboardReporting();
builder.Services.AddSingleton<StatusRegistry>();
builder.Services.AddSingleton<PublishEventHandler>();
builder.Services.AddSingleton<DashboardHttpServer>();

var host = builder.Build();

// Start the HTTP server
var server = host.Services.GetRequiredService<DashboardHttpServer>();
_ = server.StartAsync(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);

await host.RunAsync();
