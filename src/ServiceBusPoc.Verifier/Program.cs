using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Logging;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.Verifier.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config
            .AddEnvironmentVariables()
            .Build();
    })
    .ConfigureServices((context, services) =>
    {
        services
            .AddLogging(builder => builder.AddStructuredConsoleLogging())
            .AddServiceBusConfiguration(context.Configuration)
            .AddScoped<ITopologyValidator, TopologyValidator>()
            .AddScoped<VerifierService>();
    })
    .Build();

await using var scope = host.Services.CreateAsyncScope();
var verifier = scope.ServiceProvider.GetRequiredService<VerifierService>();
await verifier.RunAsync();
