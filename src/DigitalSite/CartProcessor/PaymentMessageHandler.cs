using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Contracts;
using ServiceBusPoc.Core.Messaging;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;
using ServiceBusPoc.DigitalSite.Shared.Messaging;

namespace ServiceBusPoc.DigitalSite.CartProcessor;

/// <summary>
/// Turns a cart into an order once its payment has a successful authorisation for the cart total, then
/// publishes a <c>ProductHoldingChange</c>. Every step checks commercetools state first, so a redelivered or
/// out-of-order message never creates a second order or a second holding event.
/// </summary>
public sealed class PaymentMessageHandler : ICommerceMessageHandler
{
    private const int MaxAttempts = 3;
    private const string AnnualPaymentPlan = "Annual";

    private readonly ICommerceToolsClient _commerceTools;
    private readonly ContactEventPublisher _publisher;
    private readonly CartProcessorSettings _settings;
    private readonly ILogger<PaymentMessageHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="PaymentMessageHandler"/> class.</summary>
    /// <param name="commerceTools">The commercetools client.</param>
    /// <param name="publisher">The <c>contact.events</c> publisher.</param>
    /// <param name="settings">The Cart Processor settings.</param>
    /// <param name="logger">The logger.</param>
    public PaymentMessageHandler(
        ICommerceToolsClient commerceTools,
        ContactEventPublisher publisher,
        IOptions<CartProcessorSettings> settings,
        ILogger<PaymentMessageHandler> logger)
    {
        _commerceTools = commerceTools;
        _publisher = publisher;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ServiceName => "cart-processor";

    /// <inheritdoc />
    public async Task HandleAsync(CommerceMessage message, CancellationToken cancellationToken)
    {
        if (message.Type is not (CommerceMessageTypes.PaymentTransactionAdded or CommerceMessageTypes.PaymentTransactionStateChanged))
        {
            _logger.LogDebug("Cart Processor ignored {MessageType} for {ResourceId}", message.Type, message.Resource.Id);
            return;
        }

        var payment = await _commerceTools.GetPaymentAsync(message.Resource.Id ?? string.Empty, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning("Cart Processor could not find payment {PaymentId}", message.Resource.Id);
            return;
        }

        var correlationId = payment.Custom?.GetString(DigitalSiteCustomFields.CorrelationId);
        using (_logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId, ["PaymentId"] = payment.Id }))
        {
            await ProcessPaymentAsync(payment, correlationId, cancellationToken);
        }
    }

    private async Task ProcessPaymentAsync(Payment payment, string? correlationId, CancellationToken cancellationToken)
    {
        var authorisations = payment.Transactions.Where(transaction => transaction.Type == TransactionTypes.Authorization).ToList();
        var authorised = authorisations.FirstOrDefault(transaction => transaction.State == TransactionStates.Success);
        if (authorised is null)
        {
            if (authorisations.Any(transaction => transaction.State == TransactionStates.Failure))
            {
                _logger.LogInformation("Payment {PaymentKey} was refused; no order is created", payment.Key);
                await RecordProcessingResultAsync(payment, ProcessingResults.PaymentRefused, cancellationToken);
            }

            return;
        }

        var cartId = payment.Custom?.GetString(DigitalSiteCustomFields.CartId);
        var cart = cartId is null ? null : await _commerceTools.GetCartAsync(cartId, cancellationToken);
        if (cart is null)
        {
            _logger.LogWarning("Authorised payment {PaymentKey} references missing cart {CartId}", payment.Key, cartId);
            await RecordProcessingResultAsync(payment, ProcessingResults.CartNotFound, cancellationToken);
            return;
        }

        var order = cart.CartState == CartStates.Ordered
            ? await FindOrderForCartAsync(cart.Id, cancellationToken)
            : null;
        if (order is null)
        {
            if (!authorised.Amount.IsSameAmountAs(cart.TotalPrice))
            {
                _logger.LogWarning(
                    "Payment {PaymentKey} authorised {AuthorisedCents} {Currency} but cart {CartId} totals {CartCents}; no order is created",
                    payment.Key,
                    authorised.Amount.CentAmount,
                    authorised.Amount.CurrencyCode,
                    cart.Id,
                    cart.TotalPrice.CentAmount);
                await RecordProcessingResultAsync(payment, ProcessingResults.AmountMismatch, cancellationToken);
                return;
            }

            order = await CreateOrderAsync(cart, cancellationToken);
        }

        if (order.PaymentInfo?.Payments.Any(reference => reference.Id == payment.Id) != true)
        {
            _logger.LogWarning("Cart {CartId} was already ordered as {OrderId} by a different payment than {PaymentKey}", cart.Id, order.Id, payment.Key);
            await RecordProcessingResultAsync(payment, ProcessingResults.CartAlreadyOrdered, cancellationToken);
            return;
        }

        await EnsureHoldingPublishedAsync(order, authorised, correlationId, cancellationToken);
        await RecordProcessingResultAsync(payment, ProcessingResults.OrderCreated, cancellationToken);
    }

    private async Task<Order> CreateOrderAsync(Cart cart, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var order = await _commerceTools.CreateOrderFromCartAsync(
                    new OrderFromCartDraft
                    {
                        Cart = ResourceReference.ById(ResourceTypes.Cart, cart.Id),
                        Version = cart.Version,
                        OrderNumber = OrderNumberFor(cart.Id),
                    },
                    cancellationToken);
                _logger.LogInformation("Created order {OrderNumber} ({OrderId}) from cart {CartId}", order.OrderNumber, order.Id, cart.Id);
                return order;
            }
            catch (CommerceToolsException ex) when (attempt < MaxAttempts)
            {
                // A concurrent delivery may have ordered the cart or changed its version; re-read and decide again.
                _logger.LogInformation(ex, "Order creation for cart {CartId} conflicted on attempt {Attempt}; re-reading the cart", cart.Id, attempt);
                cart = await _commerceTools.GetCartAsync(cart.Id, cancellationToken)
                    ?? throw new InvalidOperationException($"Cart {cart.Id} disappeared while creating its order.");
                if (cart.CartState == CartStates.Ordered)
                {
                    return await FindOrderForCartAsync(cart.Id, cancellationToken)
                        ?? throw new InvalidOperationException($"Cart {cart.Id} is ordered but its order was not found.");
                }
            }
        }
    }

    private async Task EnsureHoldingPublishedAsync(Order order, Transaction authorised, string? correlationId, CancellationToken cancellationToken)
    {
        if (order.Custom?.GetString(DigitalSiteCustomFields.HoldingEventId) is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(order.AnonymousId))
        {
            _logger.LogWarning("Order {OrderNumber} has no CRM id, so no ProductHoldingChange is published", order.OrderNumber);
            return;
        }

        var eventId = await _publisher.PublishProductHoldingChangeAsync(
            CreateHoldingChange(order, authorised),
            _settings.EventSource,
            correlationId,
            cancellationToken);

        await UpdateWithRetryAsync(
            order,
            id => _commerceTools.GetOrderAsync(id, cancellationToken),
            current => _commerceTools.UpdateOrderAsync(
                current.Id,
                current.Version,
                [new SetCustomFieldAction(DigitalSiteCustomFields.HoldingEventId, eventId)],
                cancellationToken),
            current => current.Id);
    }

    private async Task RecordProcessingResultAsync(Payment payment, string result, CancellationToken cancellationToken)
    {
        if (payment.Custom?.GetString(DigitalSiteCustomFields.ProcessingResult) == result)
        {
            return;
        }

        await UpdateWithRetryAsync(
            payment,
            id => _commerceTools.GetPaymentAsync(id, cancellationToken),
            current => _commerceTools.UpdatePaymentAsync(
                current.Id,
                current.Version,
                [new SetCustomFieldAction(DigitalSiteCustomFields.ProcessingResult, result)],
                cancellationToken),
            current => current.Id);
    }

    private async Task<Order?> FindOrderForCartAsync(string cartId, CancellationToken cancellationToken)
    {
        var orders = await _commerceTools.QueryOrdersAsync(CommercePredicate.ReferenceIdEqualTo("cart", cartId), cancellationToken);
        return orders.FirstOrDefault();
    }

    private static async Task UpdateWithRetryAsync<T>(
        T resource,
        Func<string, Task<T?>> reload,
        Func<T, Task<T>> update,
        Func<T, string> idOf)
        where T : class
    {
        var current = resource;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await update(current);
                return;
            }
            catch (ConcurrentModificationException) when (attempt < MaxAttempts)
            {
                current = await reload(idOf(current))
                    ?? throw new InvalidOperationException($"{typeof(T).Name} {idOf(current)} disappeared during an update.");
            }
        }
    }

    private ProductHoldingChangeData CreateHoldingChange(Order order, Transaction authorised)
    {
        var lineItem = order.LineItems.FirstOrDefault();
        return new ProductHoldingChangeData
        {
            ContactId = order.AnonymousId,
            HoldingId = order.OrderNumber ?? order.Id,
            ProductType = ProductHoldingChangeData.ProductTypes.RoadsideAssistance,
            Action = ProductHoldingChangeData.Actions.Created,
            HoldingData = new Dictionary<string, object?>
            {
                ["channel"] = _settings.EventSource,
                ["orderId"] = order.Id,
                ["orderNumber"] = order.OrderNumber,
                ["coverId"] = lineItem?.Variant.Sku,
                ["coverName"] = lineItem?.Name.Values.FirstOrDefault(),
                ["paymentPlan"] = AnnualPaymentPlan,
                ["totalAnnualCost"] = order.TotalPrice.ToDecimal(),
                ["currency"] = order.TotalPrice.CurrencyCode,
                ["paymentReference"] = authorised.InteractionId,
                ["vehicleRego"] = order.Custom?.GetString(DigitalSiteCustomFields.VehicleRego),
            },
        };
    }

    /// <summary>Derives a stable order number so a retried creation cannot produce a second order.</summary>
    /// <param name="cartId">The cart id.</param>
    /// <returns>The order number.</returns>
    public static string OrderNumberFor(string cartId)
    {
        var compactId = cartId.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return $"RSA-{compactId[..Math.Min(10, compactId.Length)]}";
    }
}
