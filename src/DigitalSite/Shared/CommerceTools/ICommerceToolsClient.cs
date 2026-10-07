using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools;

/// <summary>
/// The subset of the commercetools HTTP API used by the Digital Site. Works against the real
/// platform or the local stub; only configuration differs.
/// </summary>
public interface ICommerceToolsClient
{
    /// <summary>Gets a product type by key.</summary>
    Task<ProductType?> GetProductTypeByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Queries published product projections.</summary>
    Task<IReadOnlyList<ProductProjection>> QueryProductProjectionsAsync(string where, CancellationToken cancellationToken = default);

    /// <summary>Queries carts.</summary>
    Task<IReadOnlyList<Cart>> QueryCartsAsync(string where, CancellationToken cancellationToken = default);

    /// <summary>Gets a cart by id.</summary>
    Task<Cart?> GetCartAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Creates a cart.</summary>
    Task<Cart> CreateCartAsync(CartDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Updates a cart.</summary>
    /// <exception cref="ConcurrentModificationException">The cart version changed.</exception>
    Task<Cart> UpdateCartAsync(string id, long version, IReadOnlyList<AbstractUpdateAction> actions, CancellationToken cancellationToken = default);

    /// <summary>Deletes a cart.</summary>
    Task DeleteCartAsync(string id, long version, CancellationToken cancellationToken = default);

    /// <summary>Creates a payment.</summary>
    Task<Payment> CreatePaymentAsync(PaymentDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Gets a payment by id.</summary>
    Task<Payment?> GetPaymentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Gets a payment by key.</summary>
    Task<Payment?> GetPaymentByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Updates a payment.</summary>
    /// <exception cref="ConcurrentModificationException">The payment version changed.</exception>
    Task<Payment> UpdatePaymentAsync(string id, long version, IReadOnlyList<AbstractUpdateAction> actions, CancellationToken cancellationToken = default);

    /// <summary>Creates an order from a cart.</summary>
    /// <exception cref="ConcurrentModificationException">The cart version changed.</exception>
    Task<Order> CreateOrderFromCartAsync(OrderFromCartDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Gets an order by id.</summary>
    Task<Order?> GetOrderAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Queries orders, newest first.</summary>
    Task<IReadOnlyList<Order>> QueryOrdersAsync(string where, CancellationToken cancellationToken = default);

    /// <summary>Updates an order.</summary>
    /// <exception cref="ConcurrentModificationException">The order version changed.</exception>
    Task<Order> UpdateOrderAsync(string id, long version, IReadOnlyList<AbstractUpdateAction> actions, CancellationToken cancellationToken = default);
}
