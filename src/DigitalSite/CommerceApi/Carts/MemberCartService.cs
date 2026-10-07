using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.Catalog;
using ServiceBusPoc.DigitalSite.CommerceApi.Vehicles;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Carts;

/// <summary>
/// Manages a member's Roadside Assistance carts. The mock CRM id is the commercetools
/// <c>anonymousId</c>, which avoids commercetools customer accounts and their passwords.
/// </summary>
public sealed partial class MemberCartService
{
    /// <summary>The vehicle search choice for a registration lookup.</summary>
    public const string RegoLookup = "RegoLookup";

    /// <summary>The vehicle search choice when the shopper skips the lookup.</summary>
    public const string SkipLookup = "Skip";

    private readonly ICommerceToolsClient _commerceTools;
    private readonly CatalogService _catalog;
    private readonly VehicleRegister _vehicles;
    private readonly DigitalSiteSettings _settings;
    private readonly ILogger<MemberCartService> _logger;

    /// <summary>Initializes a new instance of the <see cref="MemberCartService"/> class.</summary>
    /// <param name="commerceTools">The commercetools client.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="vehicles">The vehicle register.</param>
    /// <param name="settings">The Digital Site settings.</param>
    /// <param name="logger">The logger.</param>
    public MemberCartService(
        ICommerceToolsClient commerceTools,
        CatalogService catalog,
        VehicleRegister vehicles,
        IOptions<DigitalSiteSettings> settings,
        ILogger<MemberCartService> logger)
    {
        _commerceTools = commerceTools;
        _catalog = catalog;
        _vehicles = vehicles;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>Rejects a malformed mock CRM id.</summary>
    /// <param name="crmId">The CRM id.</param>
    /// <exception cref="DigitalSiteRequestException">The id is not <c>CRM-</c> followed by 8 digits.</exception>
    public static void EnsureValidCrmId(string? crmId)
    {
        if (crmId is null || !CrmIdPattern().IsMatch(crmId))
        {
            throw DigitalSiteRequestException.BadRequest("crmId must be CRM- followed by 8 digits");
        }
    }

    /// <summary>Gets the member's active carts, newest first.</summary>
    /// <param name="crmId">The CRM id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The carts.</returns>
    public async Task<IReadOnlyList<CartView>> GetActiveCartsAsync(string? crmId, CancellationToken cancellationToken)
    {
        EnsureValidCrmId(crmId);
        var carts = await _commerceTools.QueryCartsAsync(ActiveCartsOf(crmId!), cancellationToken);
        return carts.Select(ToView).ToArray();
    }

    /// <summary>Gets the member's active carts and orders.</summary>
    /// <param name="crmId">The CRM id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The history.</returns>
    public async Task<MemberHistory> GetHistoryAsync(string? crmId, CancellationToken cancellationToken)
    {
        EnsureValidCrmId(crmId);
        var cartsTask = _commerceTools.QueryCartsAsync(ActiveCartsOf(crmId!), cancellationToken);
        var ordersTask = _commerceTools.QueryOrdersAsync(CommercePredicate.EqualTo("anonymousId", crmId!), cancellationToken);
        await Task.WhenAll(cartsTask, ordersTask);
        return new MemberHistory(
            (await cartsTask).Select(ToView).ToArray(),
            (await ordersTask).Select(ToOrderView).ToArray());
    }

    /// <summary>Creates a cart holding the chosen cover.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cart.</returns>
    public async Task<CartView> CreateCartAsync(CreateCartRequest request, CancellationToken cancellationToken)
    {
        EnsureValidCrmId(request.CrmId);
        if (request.IsBrokenDown != "No")
        {
            throw DigitalSiteRequestException.BadRequest("Customers who are broken down must call RAC to join");
        }

        var vehicle = ResolveVehicle(request.Vehicle);
        var cover = await ResolveCoverAsync(request.CoverId, cancellationToken);
        var correlationId = Guid.NewGuid().ToString();

        var cart = await _commerceTools.CreateCartAsync(
            new CartDraft
            {
                Currency = _settings.Currency,
                AnonymousId = request.CrmId,
                Country = _settings.Country,
                ShippingMethod = ResourceReference.ByKey(ResourceTypes.ShippingMethod, _settings.ShippingMethodKey),
                ShippingAddress = new Address { Country = _settings.Country },
                LineItems = [new LineItemDraft { Sku = cover.Id, Quantity = 1 }],
                Custom = new CustomFieldsDraft
                {
                    Type = ResourceReference.ByKey(ResourceTypes.Type, DigitalSiteCustomFields.CartTypeKey),
                    Fields = new Dictionary<string, object?>
                    {
                        [DigitalSiteCustomFields.CorrelationId] = correlationId,
                        [DigitalSiteCustomFields.CoverSku] = cover.Id,
                        [DigitalSiteCustomFields.VehicleSearchChoice] = request.Vehicle!.SearchChoice,
                        [DigitalSiteCustomFields.VehicleRego] = vehicle?.Rego,
                    },
                },
            },
            cancellationToken);

        _logger.LogInformation(
            "Cart {CartId} created for {CrmId} with cover {CoverSku} (correlation {CorrelationId})",
            cart.Id,
            request.CrmId,
            cover.Id,
            correlationId);
        return ToView(cart);
    }

    /// <summary>Changes the cover or vehicle on an active cart.</summary>
    /// <param name="cartId">The cart id.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cart.</returns>
    /// <exception cref="ConcurrentModificationException">The cart changed since the shopper saw it.</exception>
    public async Task<CartView> UpdateCartAsync(string cartId, UpdateCartRequest request, CancellationToken cancellationToken)
    {
        EnsureValidCrmId(request.CrmId);
        var cart = await GetOwnedActiveCartAsync(cartId, request.CrmId!, cancellationToken);
        var vehicle = ResolveVehicle(request.Vehicle);
        var cover = await ResolveCoverAsync(request.CoverId, cancellationToken);

        var actions = new List<AbstractUpdateAction>();
        if (cart.LineItems.Count != 1 || cart.LineItems[0].Variant.Sku != cover.Id)
        {
            actions.AddRange(cart.LineItems.Select(lineItem => new RemoveLineItemAction(lineItem.Id)));
            actions.Add(new AddLineItemAction(cover.Id));
        }

        actions.Add(new SetCustomFieldAction(DigitalSiteCustomFields.CoverSku, cover.Id));
        actions.Add(new SetCustomFieldAction(DigitalSiteCustomFields.VehicleSearchChoice, request.Vehicle!.SearchChoice));
        actions.Add(new SetCustomFieldAction(DigitalSiteCustomFields.VehicleRego, vehicle?.Rego));

        var updated = await _commerceTools.UpdateCartAsync(cart.Id, request.Version, actions, cancellationToken);
        _logger.LogInformation("Cart {CartId} updated to cover {CoverSku}", cart.Id, cover.Id);
        return ToView(updated);
    }

    /// <summary>Deletes an active cart so the member can start again.</summary>
    /// <param name="cartId">The cart id.</param>
    /// <param name="crmId">The CRM id that owns the cart.</param>
    /// <param name="version">The cart version the shopper last saw.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the cart is deleted.</returns>
    public async Task DeleteCartAsync(string cartId, string? crmId, long version, CancellationToken cancellationToken)
    {
        EnsureValidCrmId(crmId);
        var cart = await GetOwnedActiveCartAsync(cartId, crmId!, cancellationToken);
        await _commerceTools.DeleteCartAsync(cart.Id, version, cancellationToken);
        _logger.LogInformation("Cart {CartId} deleted for {CrmId}", cart.Id, crmId);
    }

    /// <summary>Maps a commercetools cart to its Digital Site view.</summary>
    /// <param name="cart">The cart.</param>
    /// <returns>The view.</returns>
    public CartView ToView(Cart cart)
    {
        var lineItem = cart.LineItems.FirstOrDefault();
        return new CartView(
            cart.Id,
            cart.Version,
            cart.AnonymousId,
            cart.CartState,
            cart.Custom?.GetString(DigitalSiteCustomFields.CoverSku) ?? lineItem?.Variant.Sku,
            lineItem is null ? null : CatalogService.LevelLabel(lineItem.Variant),
            cart.TotalPrice.ToDecimal(),
            cart.TotalPrice.CurrencyCode,
            cart.Custom?.GetString(DigitalSiteCustomFields.VehicleSearchChoice),
            _vehicles.Find(cart.Custom?.GetString(DigitalSiteCustomFields.VehicleRego)),
            cart.Custom?.GetString(DigitalSiteCustomFields.CorrelationId),
            cart.CreatedAt,
            cart.LastModifiedAt);
    }

    private static OrderView ToOrderView(Order order)
    {
        var lineItem = order.LineItems.FirstOrDefault();
        return new OrderView(
            order.Id,
            order.OrderNumber,
            order.OrderState,
            lineItem is null ? null : CatalogService.LevelLabel(lineItem.Variant),
            order.TotalPrice.ToDecimal(),
            order.TotalPrice.CurrencyCode,
            order.CreatedAt);
    }

    private static string ActiveCartsOf(string crmId) =>
        CommercePredicate.And(
            CommercePredicate.EqualTo("anonymousId", crmId),
            CommercePredicate.EqualTo("cartState", CartStates.Active));

    private async Task<Cart> GetOwnedActiveCartAsync(string cartId, string crmId, CancellationToken cancellationToken)
    {
        var cart = await _commerceTools.GetCartAsync(cartId, cancellationToken);
        if (cart is null || cart.AnonymousId != crmId)
        {
            throw DigitalSiteRequestException.NotFound("Cart not found");
        }

        return cart.CartState == CartStates.Active
            ? cart
            : throw DigitalSiteRequestException.Conflict("This cart has already been ordered");
    }

    private Vehicle? ResolveVehicle(VehicleSelection? selection)
    {
        switch (selection?.SearchChoice)
        {
            case SkipLookup:
                return null;
            case RegoLookup when !VehicleRegister.IsValid(selection.Rego):
                throw DigitalSiteRequestException.BadRequest("vehicle.rego is not a valid registration");
            case RegoLookup:
                return _vehicles.Find(selection.Rego) ?? throw DigitalSiteRequestException.BadRequest("vehicle.rego was not found");
            default:
                throw DigitalSiteRequestException.BadRequest("vehicle.searchChoice must be RegoLookup or Skip");
        }
    }

    private async Task<CoverOffer> ResolveCoverAsync(string? coverId, CancellationToken cancellationToken) =>
        await _catalog.FindCoverAsync(coverId, cancellationToken)
        ?? throw DigitalSiteRequestException.BadRequest("coverId is not a known cover");

    [GeneratedRegex(@"^CRM-\d{8}$")]
    private static partial Regex CrmIdPattern();
}
