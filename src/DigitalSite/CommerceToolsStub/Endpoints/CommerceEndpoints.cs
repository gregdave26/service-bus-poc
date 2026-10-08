using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Auth;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Endpoints;

/// <summary>Maps the subset of the commercetools HTTP API the Digital Site uses.</summary>
public static class CommerceEndpoints
{
    private const string KeyPrefix = "key=";

    /// <summary>Maps the token endpoint and the project-scoped API.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapCommerceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapPost("/oauth/token", (HttpRequest request, StubTokenService tokens) =>
            tokens.TryIssue(request.Headers.Authorization) is { } token
                ? Results.Json(new Dictionary<string, object>
                {
                    ["access_token"] = token,
                    ["token_type"] = "Bearer",
                    ["expires_in"] = StubTokenService.ExpiresInSeconds
                })
                : Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized));

        var project = app.MapGroup("/{projectKey}").AddEndpointFilter(ProjectFilterAsync);

        project.MapGet("/product-types/{keyExpression}", (string keyExpression, CatalogService catalog, CancellationToken ct) =>
            catalog.GetProductTypeByKeyAsync(ParseKey(keyExpression), ct));
        project.MapGet("/product-projections", (string? where, string? sort, int? limit, int? offset, CatalogService catalog, CancellationToken ct) =>
            catalog.QueryProductProjectionsAsync(where, sort, limit, offset, ct));

        project.MapGet("/carts", (string? where, string? sort, int? limit, int? offset, CartService carts, CancellationToken ct) =>
            carts.QueryAsync(where, sort, limit, offset, ct));
        project.MapGet("/carts/{id}", (string id, CartService carts, CancellationToken ct) => carts.GetAsync(id, ct));
        project.MapPost("/carts", async (CartDraft draft, CartService carts, CancellationToken ct) =>
            Results.Created((string?)null, await carts.CreateAsync(draft, ct)));
        project.MapPost("/carts/{id}", (string id, UpdateRequest request, CartService carts, CancellationToken ct) =>
            carts.UpdateAsync(id, request, ct));
        project.MapDelete("/carts/{id}", (string id, long version, CartService carts, CancellationToken ct) =>
            carts.DeleteAsync(id, version, ct));

        project.MapPost("/payments", async (PaymentDraft draft, PaymentService payments, CancellationToken ct) =>
            Results.Created((string?)null, await payments.CreateAsync(draft, ct)));
        project.MapGet("/payments/{idOrKey}", (string idOrKey, PaymentService payments, CancellationToken ct) =>
            idOrKey.StartsWith(KeyPrefix, StringComparison.Ordinal)
                ? payments.GetByKeyAsync(ParseKey(idOrKey), ct)
                : payments.GetAsync(idOrKey, ct));
        project.MapPost("/payments/{id}", (string id, UpdateRequest request, PaymentService payments, CancellationToken ct) =>
            payments.UpdateAsync(id, request, ct));

        project.MapPost("/orders", async (OrderFromCartDraft draft, OrderService orders, CancellationToken ct) =>
            Results.Created((string?)null, await orders.CreateFromCartAsync(draft, ct)));
        project.MapGet("/orders", (string? where, string? sort, int? limit, int? offset, OrderService orders, CancellationToken ct) =>
            orders.QueryAsync(where, sort, limit, offset, ct));
        project.MapGet("/orders/{id}", (string id, OrderService orders, CancellationToken ct) => orders.GetAsync(id, ct));
        project.MapPost("/orders/{id}", (string id, UpdateRequest request, OrderService orders, CancellationToken ct) =>
            orders.UpdateAsync(id, request, ct));

        return app;
    }

    private static string ParseKey(string expression) =>
        expression.StartsWith(KeyPrefix, StringComparison.Ordinal)
            ? Uri.UnescapeDataString(expression[KeyPrefix.Length..])
            : throw CommerceStubException.InvalidInput("Only key=<key> lookups are supported.");

    private static async ValueTask<object?> ProjectFilterAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<CommerceToolsStubSettings>>().Value;
        var tokens = httpContext.RequestServices.GetRequiredService<StubTokenService>();

        try
        {
            if (!tokens.IsAuthorized(httpContext.Request.Headers.Authorization))
            {
                throw CommerceStubException.Unauthorized();
            }

            if (!string.Equals(httpContext.Request.RouteValues["projectKey"] as string, settings.ProjectKey, StringComparison.Ordinal))
            {
                throw CommerceStubException.NotFound("project", httpContext.Request.RouteValues["projectKey"] as string ?? string.Empty);
            }

            return await next(context);
        }
        catch (CommerceStubException ex)
        {
            return Results.Json(
                CommerceToolsErrorResponse.Single(ex.StatusCode, ex.Code, ex.Message),
                StubJson.Options,
                statusCode: ex.StatusCode);
        }
    }
}
