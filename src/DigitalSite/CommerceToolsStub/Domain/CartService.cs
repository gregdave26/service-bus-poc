using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>Cart endpoints of the stub.</summary>
public sealed class CartService
{
    private readonly IResourceStore _store;
    private readonly CatalogService _catalog;
    private readonly StubWriteLock _writeLock;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="CartService"/> class.</summary>
    public CartService(IResourceStore store, CatalogService catalog, StubWriteLock writeLock, TimeProvider timeProvider)
    {
        _store = store;
        _catalog = catalog;
        _writeLock = writeLock;
        _timeProvider = timeProvider;
    }

    /// <summary>Queries carts.</summary>
    public async Task<PagedQueryResponse<Cart>> QueryAsync(string? where, string? sort, int? limit, int? offset, CancellationToken cancellationToken) =>
        QueryResults.Page(await _store.ListAsync<Cart>(ResourceTypes.Cart, cancellationToken), where, sort, limit, offset);

    /// <summary>Gets a cart.</summary>
    public async Task<Cart> GetAsync(string id, CancellationToken cancellationToken) =>
        await _store.GetAsync<Cart>(ResourceTypes.Cart, id, cancellationToken)
        ?? throw CommerceStubException.NotFound(ResourceTypes.Cart, id);

    /// <summary>Creates a cart.</summary>
    public async Task<Cart> CreateAsync(CartDraft draft, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft.Currency) || draft.Currency.Length != 3)
        {
            throw CommerceStubException.InvalidInput("currency must be a 3-letter ISO 4217 code.");
        }

        if (draft.ShippingMethod is not null)
        {
            await _catalog.FindShippingMethodAsync(draft.ShippingMethod, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow();
        var cart = new Cart
        {
            Id = Guid.NewGuid().ToString(),
            Version = 1,
            AnonymousId = draft.AnonymousId,
            CartState = CartStates.Active,
            ShippingAddress = draft.ShippingAddress,
            Custom = StubResources.ToCustomFields(draft.Custom),
            CreatedAt = now,
            LastModifiedAt = now
        };

        foreach (var lineItem in draft.LineItems)
        {
            await AddLineItemAsync(cart, draft.Currency, lineItem, cancellationToken);
        }

        Recalculate(cart, draft.Currency);
        using (await _writeLock.AcquireAsync(cancellationToken))
        {
            await _store.CommitAsync([new ResourceWrite(ResourceTypes.Cart, cart.Id, null, cart)], [], cancellationToken);
        }

        return cart;
    }

    /// <summary>Applies update actions to a cart.</summary>
    public async Task<Cart> UpdateAsync(string id, UpdateRequest request, CancellationToken cancellationToken)
    {
        using (await _writeLock.AcquireAsync(cancellationToken))
        {
            var cart = await GetAsync(id, cancellationToken);
            StubResources.EnsureVersion(request.Version, cart.Version);
            if (cart.CartState != CartStates.Active)
            {
                throw CommerceStubException.InvalidOperation($"Cart '{id}' is {cart.CartState} and cannot be modified.");
            }

            var currency = cart.TotalPrice.CurrencyCode;
            foreach (var action in request.Actions)
            {
                await ApplyAsync(cart, currency, action, cancellationToken);
            }

            Recalculate(cart, currency);
            cart.Version++;
            cart.LastModifiedAt = _timeProvider.GetUtcNow();
            await _store.CommitAsync([new ResourceWrite(ResourceTypes.Cart, cart.Id, null, cart)], [], cancellationToken);
            return cart;
        }
    }

    /// <summary>Deletes a cart.</summary>
    public async Task<Cart> DeleteAsync(string id, long version, CancellationToken cancellationToken)
    {
        using (await _writeLock.AcquireAsync(cancellationToken))
        {
            var cart = await GetAsync(id, cancellationToken);
            StubResources.EnsureVersion(version, cart.Version);
            await _store.DeleteAsync(ResourceTypes.Cart, id, cancellationToken);
            return cart;
        }
    }

    private async Task ApplyAsync(Cart cart, string currency, AbstractUpdateAction action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case AddLineItemAction addLineItem:
                await AddLineItemAsync(cart, currency, new LineItemDraft { Sku = addLineItem.Sku, Quantity = addLineItem.Quantity }, cancellationToken);
                break;
            case RemoveLineItemAction removeLineItem:
                if (cart.LineItems.RemoveAll(item => item.Id == removeLineItem.LineItemId) == 0)
                {
                    throw CommerceStubException.InvalidInput($"Line item '{removeLineItem.LineItemId}' not found.");
                }

                break;
            case AddPaymentAction addPayment:
                var paymentId = addPayment.Payment.Id
                    ?? throw CommerceStubException.InvalidInput("addPayment requires a payment id.");
                if (await _store.GetAsync<Payment>(ResourceTypes.Payment, paymentId, cancellationToken) is null)
                {
                    throw CommerceStubException.InvalidInput($"Payment '{paymentId}' not found.");
                }

                cart.PaymentInfo ??= new PaymentInfo();
                if (cart.PaymentInfo.Payments.All(payment => payment.Id != paymentId))
                {
                    cart.PaymentInfo.Payments.Add(ResourceReference.ById(ResourceTypes.Payment, paymentId));
                }

                break;
            case SetCustomFieldAction setCustomField:
                cart.Custom = StubResources.SetCustomField(cart.Custom, setCustomField.Name, setCustomField.Value);
                break;
            default:
                throw CommerceStubException.InvalidInput($"Action '{action.GetType().Name}' is not supported on carts.");
        }
    }

    private async Task AddLineItemAsync(Cart cart, string currency, LineItemDraft draft, CancellationToken cancellationToken)
    {
        var quantity = draft.Quantity ?? 1;
        if (quantity < 1)
        {
            throw CommerceStubException.InvalidInput("quantity must be at least 1.");
        }

        var product = await _catalog.FindProductAsync(draft, cancellationToken);
        var existing = cart.LineItems.FirstOrDefault(item => item.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            return;
        }

        var price = product.MasterVariant.Prices.FirstOrDefault(candidate =>
                string.Equals(candidate.Value.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
            ?? throw CommerceStubException.InvalidOperation($"Product '{product.Id}' has no price in {currency}.");

        cart.LineItems.Add(new LineItem
        {
            Id = Guid.NewGuid().ToString(),
            ProductId = product.Id,
            ProductKey = product.Key,
            Name = product.Name,
            Variant = product.MasterVariant,
            Quantity = quantity,
            Price = price
        });
    }

    private static void Recalculate(Cart cart, string currency)
    {
        foreach (var item in cart.LineItems)
        {
            item.TotalPrice = Money.FromCents(currency, item.Price.Value.CentAmount * item.Quantity);
        }

        cart.TotalPrice = Money.FromCents(currency, cart.LineItems.Sum(item => item.TotalPrice.CentAmount));
    }
}
