using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceBusPoc.Core.Utilities;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools;

/// <summary>
/// HTTP implementation of <see cref="ICommerceToolsClient"/>. The <see cref="HttpClient"/> base address
/// must end with <c>/{projectKey}/</c>.
/// </summary>
public sealed class CommerceToolsClient : ICommerceToolsClient
{
    private const int QueryLimit = 100;
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptionsHelper.DefaultOptions;

    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="CommerceToolsClient"/> class.</summary>
    /// <param name="httpClient">The configured HTTP client.</param>
    public CommerceToolsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public Task<ProductType?> GetProductTypeByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<ProductType>($"product-types/key={Uri.EscapeDataString(key)}", cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ProductProjection>> QueryProductProjectionsAsync(string where, CancellationToken cancellationToken = default) =>
        QueryAsync<ProductProjection>("product-projections", where, sort: null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Cart>> QueryCartsAsync(string where, CancellationToken cancellationToken = default) =>
        QueryAsync<Cart>("carts", where, "lastModifiedAt desc", cancellationToken);

    /// <inheritdoc />
    public Task<Cart?> GetCartAsync(string id, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<Cart>($"carts/{Uri.EscapeDataString(id)}", cancellationToken);

    /// <inheritdoc />
    public Task<Cart> CreateCartAsync(CartDraft draft, CancellationToken cancellationToken = default) =>
        PostAsync<Cart>("carts", draft, cancellationToken);

    /// <inheritdoc />
    public Task<Cart> UpdateCartAsync(string id, long version, IReadOnlyList<AbstractUpdateAction> actions, CancellationToken cancellationToken = default) =>
        PostAsync<Cart>($"carts/{Uri.EscapeDataString(id)}", new UpdateRequest(version, actions), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteCartAsync(string id, long version, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"carts/{Uri.EscapeDataString(id)}?version={version}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Payment> CreatePaymentAsync(PaymentDraft draft, CancellationToken cancellationToken = default) =>
        PostAsync<Payment>("payments", draft, cancellationToken);

    /// <inheritdoc />
    public Task<Payment?> GetPaymentAsync(string id, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<Payment>($"payments/{Uri.EscapeDataString(id)}", cancellationToken);

    /// <inheritdoc />
    public Task<Payment?> GetPaymentByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<Payment>($"payments/key={Uri.EscapeDataString(key)}", cancellationToken);

    /// <inheritdoc />
    public Task<Payment> UpdatePaymentAsync(string id, long version, IReadOnlyList<AbstractUpdateAction> actions, CancellationToken cancellationToken = default) =>
        PostAsync<Payment>($"payments/{Uri.EscapeDataString(id)}", new UpdateRequest(version, actions), cancellationToken);

    /// <inheritdoc />
    public Task<Order> CreateOrderFromCartAsync(OrderFromCartDraft draft, CancellationToken cancellationToken = default) =>
        PostAsync<Order>("orders", draft, cancellationToken);

    /// <inheritdoc />
    public Task<Order?> GetOrderAsync(string id, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<Order>($"orders/{Uri.EscapeDataString(id)}", cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Order>> QueryOrdersAsync(string where, CancellationToken cancellationToken = default) =>
        QueryAsync<Order>("orders", where, "createdAt desc", cancellationToken);

    /// <inheritdoc />
    public Task<Order> UpdateOrderAsync(string id, long version, IReadOnlyList<AbstractUpdateAction> actions, CancellationToken cancellationToken = default) =>
        PostAsync<Order>($"orders/{Uri.EscapeDataString(id)}", new UpdateRequest(version, actions), cancellationToken);

    private async Task<T?> GetOrDefaultAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string path, string where, string? sort, CancellationToken cancellationToken)
    {
        var query = $"{path}?where={Uri.EscapeDataString(where)}&limit={QueryLimit}";
        if (sort is not null)
        {
            query += $"&sort={Uri.EscapeDataString(sort)}";
        }

        using var response = await _httpClient.GetAsync(query, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var page = await ReadAsync<PagedQueryResponse<T>>(response, cancellationToken);
        return page.Results;
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
        ?? throw new CommerceToolsException(response.StatusCode, "commercetools returned an empty body.", []);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await TryReadErrorAsync(response, cancellationToken);
        var message = string.IsNullOrWhiteSpace(error?.Message)
            ? $"commercetools request failed with status {(int)response.StatusCode}."
            : error.Message;
        var codes = error?.Errors.Select(item => item.Code).ToArray() ?? [];

        if (response.StatusCode == HttpStatusCode.Conflict
            && codes.Contains(CommerceToolsErrorCodes.ConcurrentModification))
        {
            throw new ConcurrentModificationException(message, codes);
        }

        throw new CommerceToolsException(response.StatusCode, message, codes);
    }

    private static async Task<CommerceToolsErrorResponse?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<CommerceToolsErrorResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
