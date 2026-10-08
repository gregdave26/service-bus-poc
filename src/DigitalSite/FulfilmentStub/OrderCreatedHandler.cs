using Microsoft.Extensions.Logging;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;
using ServiceBusPoc.DigitalSite.Shared.Messaging;

namespace ServiceBusPoc.DigitalSite.FulfilmentStub;

/// <summary>
/// Stands in for D365 F&amp;O: logs the provisioning of each new order, then marks the order <c>Complete</c>.
/// Orders that are already complete are skipped, so redelivery is harmless.
/// </summary>
public sealed class OrderCreatedHandler : ICommerceMessageHandler
{
    private const int MaxAttempts = 3;

    private readonly ICommerceToolsClient _commerceTools;
    private readonly ILogger<OrderCreatedHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="OrderCreatedHandler"/> class.</summary>
    /// <param name="commerceTools">The commercetools client.</param>
    /// <param name="logger">The logger.</param>
    public OrderCreatedHandler(ICommerceToolsClient commerceTools, ILogger<OrderCreatedHandler> logger)
    {
        _commerceTools = commerceTools;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ServiceName => "fulfilment-d365-stub";

    /// <inheritdoc />
    public async Task HandleAsync(CommerceMessage message, CancellationToken cancellationToken)
    {
        if (message.Type != CommerceMessageTypes.OrderCreated)
        {
            _logger.LogDebug("Fulfilment stub ignored {MessageType} for {ResourceId}", message.Type, message.Resource.Id);
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            var order = await _commerceTools.GetOrderAsync(message.Resource.Id ?? string.Empty, cancellationToken);
            if (order is null)
            {
                _logger.LogWarning("Fulfilment stub could not find order {OrderId}", message.Resource.Id);
                return;
            }

            if (order.OrderState == OrderStates.Complete)
            {
                _logger.LogInformation("Order {OrderNumber} is already complete; nothing to provision", order.OrderNumber);
                return;
            }

            if (attempt == 1)
            {
                _logger.LogInformation(
                    "D365 F&O provisioning (stub) for order {OrderNumber} ({OrderId}): {LineItemCount} item(s), {TotalCents} {Currency}, CRM {CrmId}, correlation {CorrelationId}",
                    order.OrderNumber,
                    order.Id,
                    order.LineItems.Count,
                    order.TotalPrice.CentAmount,
                    order.TotalPrice.CurrencyCode,
                    order.AnonymousId,
                    order.Custom?.GetString(DigitalSiteCustomFields.CorrelationId));
            }

            try
            {
                await _commerceTools.UpdateOrderAsync(order.Id, order.Version, [new ChangeOrderStateAction(OrderStates.Complete)], cancellationToken);
                _logger.LogInformation("Order {OrderNumber} marked Complete", order.OrderNumber);
                return;
            }
            catch (ConcurrentModificationException) when (attempt < MaxAttempts)
            {
                _logger.LogInformation("Order {OrderNumber} changed while completing it; retrying", order.OrderNumber);
            }
        }
    }
}
