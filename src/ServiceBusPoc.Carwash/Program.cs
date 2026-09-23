using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Logging;
using ServiceBusPoc.Carwash.Services;
using ServiceBusPoc.Carwash.Api;

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
            .AddCarwashConfiguration(context.Configuration)
            .AddDashboardConfiguration(context.Configuration)
            .AddContactEventConsuming()
            .AddDashboardReporting()
            .AddSingleton<CarwashConsumerService>()
            .AddSingleton<IMembershipVerifier, MockMembershipVerifier>()
            .AddSingleton<CarwashApiServer>();
    })
    .Build();

// Start the Carwash verification API server in a background task
var apiServer = host.Services.GetRequiredService<CarwashApiServer>();
var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var apiServerTask = Task.Run(() => apiServer.StartAsync(cts.Token), cts.Token);

try
{
    await using var scope = host.Services.CreateAsyncScope();
    var consumer = scope.ServiceProvider.GetRequiredService<CarwashConsumerService>();
    await consumer.RunAsync(cts.Token);
}
finally
{
    cts.Cancel();
    apiServer.Stop();
    try
    {
        await apiServerTask;
    }
    catch (OperationCanceledException)
    {
        // Expected when cancellation is requested
    }
}
