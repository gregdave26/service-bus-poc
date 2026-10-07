using ServiceBusPoc.DigitalSite.CommerceToolsStub.Messaging;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>Order endpoints of the stub; creation and state changes emit subscription messages.</summary>
public sealed class OrderService
{
    private readonly IResourceStore _store;
    private readonly StubWriteLock _writeLock;
    private readonly MessageFactory _messageFactory;
    private readonly IOutboxSignal _outboxSignal;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="OrderService"/> class.</summary>
    public OrderService(
        IResourceStore store,
        StubWriteLock writeLock,
        MessageFactory messageFactory,
        IOutboxSignal outboxSignal,
        TimeProvider timeProvider)
    {
        _store = store;
        _writeLock = writeLock;
        _messageFactory = messageFactory;
        _outboxSignal = outboxSignal;
        _timeProvider = timeProvider;
    }

    /// <summary>Queries orders.</summary>
    public async Task<PagedQueryResponse<Order>> QueryAsync(string? where, string? sort, int? limit, int? offset, CancellationToken cancellationToken) =>
        QueryResults.Page(await _store.ListAsync<Order>(ResourceTypes.Order, cancellationToken), where, sort, limit, offset);

    /// <summary>Gets an order.</summary>
    public async Task<Order> GetAsync(string id, CancellationToken cancellationToken) =>
        await _store.GetAsync<Order>(ResourceTypes.Order, id, cancellationToken)
        ?? throw CommerceStubException.NotFound(ResourceTypes.Order, id);

    /// <summary>Converts an active cart into an order.</summary>
    public async Task<Order> CreateFromCartAsync(OrderFromCartDraft draft, CancellationToken cancellationToken)
    {
        var cartId = draft.Cart.Id ?? throw CommerceStubException.InvalidInput("cart.id is required.");
        using (await _writeLock.AcquireAsync(cancellationToken))
        {
            var cart = await _store.GetAsync<Cart>(ResourceTypes.Cart, cartId, cancellationToken)
                ?? throw CommerceStubException.InvalidInput($"Cart '{cartId}' not found.");
            StubResources.EnsureVersion(draft.Version, cart.Version);
            if (cart.CartState != CartStates.Active)
            {
                throw CommerceStubException.InvalidOperation($"Cart '{cartId}' is {cart.CartState}; only active carts can be ordered.");
            }

            if (cart.LineItems.Count == 0)
            {
                throw CommerceStubException.InvalidOperation("Cannot create an order from an empty cart.");
            }

            if (draft.OrderNumber is not null)
            {
                var orders = await _store.ListAsync<Order>(ResourceTypes.Order, cancellationToken);
                if (orders.Any(order => order.OrderNumber == draft.OrderNumber))
                {
                    throw CommerceStubException.DuplicateField("orderNumber", draft.OrderNumber);
                }
            }

            var now = _timeProvider.GetUtcNow();
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                Version = 1,
                OrderNumber = draft.OrderNumber,
                AnonymousId = cart.AnonymousId,
                OrderState = OrderStates.Open,
                Cart = ResourceReference.ById(ResourceTypes.Cart, cart.Id),
                LineItems = cart.LineItems,
                TotalPrice = cart.TotalPrice,
                PaymentInfo = cart.PaymentInfo,
                Custom = cart.Custom,
                CreatedAt = now,
                LastModifiedAt = now
            };
            cart.CartState = CartStates.Ordered;
            cart.Version++;
            cart.LastModifiedAt = now;

            var message = _messageFactory.Create(
                CommerceMessageTypes.OrderCreated,
                ResourceReference.ById(ResourceTypes.Order, order.Id),
                order.Version,
                new Dictionary<string, object?> { ["order"] = order },
                StubResources.CorrelationIdOf(order.Custom));
            await _store.CommitAsync(
                [new ResourceWrite(ResourceTypes.Order, order.Id, null, order), new ResourceWrite(ResourceTypes.Cart, cart.Id, null, cart)],
                [message],
                cancellationToken);
            _outboxSignal.Notify();
            return order;
        }
    }

    /// <summary>Applies update actions to an order.</summary>
    public async Task<Order> UpdateAsync(string id, UpdateRequest request, CancellationToken cancellationToken)
    {
        using (await _writeLock.AcquireAsync(cancellationToken))
        {
            var order = await GetAsync(id, cancellationToken);
            StubResources.EnsureVersion(request.Version, order.Version);
            order.Version++;
            order.LastModifiedAt = _timeProvider.GetUtcNow();

            var messages = new List<PendingMessage>();
            foreach (var action in request.Actions)
            {
                switch (action)
                {
                    case ChangeOrderStateAction changeState:
                        var oldState = order.OrderState;
                        order.OrderState = changeState.OrderState;
                        messages.Add(_messageFactory.Create(
                            CommerceMessageTypes.OrderStateChanged,
                            ResourceReference.ById(ResourceTypes.Order, order.Id),
                            order.Version,
                            new Dictionary<string, object?> { ["orderState"] = order.OrderState, ["oldOrderState"] = oldState },
                            StubResources.CorrelationIdOf(order.Custom)));
                        break;
                    case SetCustomFieldAction setCustomField:
                        order.Custom = StubResources.SetCustomField(order.Custom, setCustomField.Name, setCustomField.Value);
                        break;
                    default:
                        throw CommerceStubException.InvalidInput($"Action '{action.GetType().Name}' is not supported on orders.");
                }
            }

            await _store.CommitAsync([new ResourceWrite(ResourceTypes.Order, order.Id, null, order)], messages, cancellationToken);
            if (messages.Count > 0)
            {
                _outboxSignal.Notify();
            }

            return order;
        }
    }
}
