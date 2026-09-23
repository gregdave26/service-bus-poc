using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Messaging.ServiceBus;
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

if (args.Contains("--connectivity-probe", StringComparer.OrdinalIgnoreCase))
{
    var settings = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceBusSettings>>().Value;
    try
    {
        await using var client = new ServiceBusClient(settings.ConnectionString);
        await using var sender = client.CreateSender(settings.TopicName);
        using var batch = await sender.CreateMessageBatchAsync();
        Console.WriteLine($"SDK connectivity probe passed for topic '{settings.TopicName}'.");
        Environment.ExitCode = 0;
        return;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"SDK connectivity probe failed: {exception.Message}");
        Environment.ExitCode = 1;
        return;
    }
}

await using var scope = host.Services.CreateAsyncScope();
var verifier = scope.ServiceProvider.GetRequiredService<VerifierService>();
await verifier.RunAsync();
