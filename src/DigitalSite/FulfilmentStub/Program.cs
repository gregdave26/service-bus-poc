using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Logging;
using ServiceBusPoc.DigitalSite.FulfilmentStub;
using ServiceBusPoc.DigitalSite.Shared.DependencyInjection;
using ServiceBusPoc.DigitalSite.Shared.Messaging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, config) => config.AddEnvironmentVariables())
    .ConfigureServices((context, services) =>
    {
        services
            .AddLogging(builder => builder.AddStructuredConsoleLogging())
            .AddServiceBusConfiguration(context.Configuration)
            .AddDashboardConfiguration(context.Configuration)
            .AddDashboardReporting()
            .AddCommerceToolsClient(context.Configuration)
            .AddCommerceMessageConsuming<OrderCreatedHandler>();
    })
    .Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var runner = host.Services.GetRequiredService<CommerceSubscriptionRunner>();
await runner.RunAsync(cts.Token);
