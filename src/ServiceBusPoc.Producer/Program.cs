using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Logging;
using ServiceBusPoc.Producer.Services;

var commandLineMappings = new Dictionary<string, string>
{
    ["--contact-id"] = "Producer:ContactId",
    ["--first-name"] = "Producer:FirstName",
    ["--last-name"] = "Producer:LastName",
    ["--email"] = "Producer:Email",
    ["--phone"] = "Producer:Phone",
    ["--source"] = "Producer:Source",
    ["--correlation-id"] = "Producer:CorrelationId",
    ["--has-insurance"] = "Producer:HasInsurance",
    ["--has-parks-resorts"] = "Producer:HasParksResorts",
    ["--has-carwash-product"] = "Producer:HasCarwashProduct"
};

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config
            .AddEnvironmentVariables()
            .AddCommandLine(args, commandLineMappings)
            .Build();
    })
    .ConfigureServices((context, services) =>
    {
        services
            .AddLogging(builder => builder.AddStructuredConsoleLogging())
            .AddServiceBusConfiguration(context.Configuration)
            .AddProducerConfiguration(context.Configuration)
            .AddDashboardConfiguration(context.Configuration)
            .AddDashboardReporting()
            .AddSingleton(TimeProvider.System)
            .AddTransient<IServiceBusMessagePublisher, ServiceBusMessagePublisher>()
            .AddTransient<ProducerService>();
    })
    .Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await using var scope = host.Services.CreateAsyncScope();
var producer = scope.ServiceProvider.GetRequiredService<ProducerService>();
await producer.RunAsync(cts.Token);
