using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.Core.DependencyInjection;
using ServiceBusPoc.Core.Logging;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalSite.CartProcessor;
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
            .AddCommerceMessageConsuming<PaymentMessageHandler>();
        services.Configure<CartProcessorSettings>(context.Configuration.GetSection(CartProcessorSettings.SectionName));

        // The processor receives from commerce.events (ServiceBus:TopicName) but publishes holdings to contact.events.
        services.AddSingleton<IServiceBusSender>(provider => new ServiceBusSenderAdapter(
            provider.GetRequiredService<ServiceBusClient>(),
            Options.Create(new ServiceBusSettings
            {
                TopicName = provider.GetRequiredService<IOptions<CartProcessorSettings>>().Value.ContactEventsTopicName,
            })));
        services.AddSingleton<ContactEventPublisher>();
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
