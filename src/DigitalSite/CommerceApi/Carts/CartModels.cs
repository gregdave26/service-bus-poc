using ServiceBusPoc.DigitalSite.CommerceApi.Catalog;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Carts;

/// <summary>How the shopper identified their vehicle.</summary>
/// <param name="SearchChoice"><c>RegoLookup</c> or <c>Skip</c>.</param>
/// <param name="Rego">The registration, for a lookup.</param>
public sealed record VehicleSelection(string? SearchChoice, string? Rego);

/// <summary>Request to start a Roadside Assistance cart.</summary>
/// <param name="CrmId">The member's mock CRM id.</param>
/// <param name="IsBrokenDown">The shopper's answer to "Are you broken down?".</param>
/// <param name="Vehicle">The vehicle selection.</param>
/// <param name="CoverId">The cover SKU.</param>
public sealed record CreateCartRequest(string? CrmId, string? IsBrokenDown, VehicleSelection? Vehicle, string? CoverId);

/// <summary>Request to change the cover or vehicle on a cart.</summary>
/// <param name="CrmId">The member's mock CRM id.</param>
/// <param name="Version">The cart version the shopper last saw.</param>
/// <param name="Vehicle">The vehicle selection.</param>
/// <param name="CoverId">The cover SKU.</param>
public sealed record UpdateCartRequest(string? CrmId, long Version, VehicleSelection? Vehicle, string? CoverId);

/// <summary>A cart as the Digital Site shows it.</summary>
/// <param name="Id">The cart id.</param>
/// <param name="Version">The cart version.</param>
/// <param name="CrmId">The member's mock CRM id.</param>
/// <param name="CartState">The cart state.</param>
/// <param name="CoverId">The cover SKU.</param>
/// <param name="CoverName">The cover name.</param>
/// <param name="TotalPrice">The total in dollars.</param>
/// <param name="Currency">The currency.</param>
/// <param name="VehicleSearchChoice">How the vehicle was chosen.</param>
/// <param name="Vehicle">The vehicle, when looked up.</param>
/// <param name="CorrelationId">The POC correlation id.</param>
/// <param name="CreatedAt">When the cart was created.</param>
/// <param name="LastModifiedAt">When the cart last changed.</param>
public sealed record CartView(
    string Id,
    long Version,
    string? CrmId,
    string CartState,
    string? CoverId,
    string? CoverName,
    decimal TotalPrice,
    string Currency,
    string? VehicleSearchChoice,
    Vehicle? Vehicle,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt);

/// <summary>An order as the member history shows it.</summary>
/// <param name="Id">The order id.</param>
/// <param name="OrderNumber">The order number.</param>
/// <param name="OrderState">The order state.</param>
/// <param name="CoverName">The cover name.</param>
/// <param name="TotalPrice">The total in dollars.</param>
/// <param name="Currency">The currency.</param>
/// <param name="CreatedAt">When the order was created.</param>
public sealed record OrderView(
    string Id,
    string? OrderNumber,
    string OrderState,
    string? CoverName,
    decimal TotalPrice,
    string Currency,
    DateTimeOffset CreatedAt);

/// <summary>A member's active carts and orders.</summary>
/// <param name="ActiveCarts">The active carts, newest first.</param>
/// <param name="Orders">The orders, newest first.</param>
public sealed record MemberHistory(IReadOnlyList<CartView> ActiveCarts, IReadOnlyList<OrderView> Orders);
