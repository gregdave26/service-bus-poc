using ServiceBusPoc.Core.Configuration;
using ServiceBusPoc.DigitalSite.CommerceApi.Carts;
using ServiceBusPoc.DigitalSite.CommerceApi.Catalog;
using ServiceBusPoc.DigitalSite.CommerceApi.Checkout;
using ServiceBusPoc.DigitalSite.CommerceApi.Notifications;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.CommerceApi.Vehicles;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Endpoints;

/// <summary>The Digital Site backend-for-frontend routes.</summary>
public static class DigitalSiteEndpoints
{
    /// <summary>The route prefix.</summary>
    public const string RoutePrefix = "/api/digital-site";

    /// <summary>Maps the routes.</summary>
    /// <param name="app">The route builder.</param>
    /// <param name="paymentGateway">The configured gateway; stub-only routes are mapped only for the stub.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapDigitalSiteEndpoints(this IEndpointRouteBuilder app, PaymentGatewayMode paymentGateway)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        var api = app.MapGroup(RoutePrefix).AddEndpointFilter(TranslateErrorsAsync);

        api.MapGet("/config", (IPaymentGatewayAdapter gateway) => Results.Ok(new { gateway = gateway.Mode.ToString() }));

        api.MapGet("/catalog", async (CatalogService catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.GetCatalogAsync(cancellationToken)));

        api.MapGet("/vehicles/{rego}", (string rego, VehicleRegister vehicles) =>
        {
            if (!VehicleRegister.IsValid(rego))
            {
                throw DigitalSiteRequestException.BadRequest("Enter a valid registration");
            }

            return vehicles.Find(rego) is { } vehicle
                ? Results.Ok(vehicle)
                : throw DigitalSiteRequestException.NotFound("We couldn't find that vehicle");
        });

        api.MapGet("/members/{crmId}/carts", async (string crmId, MemberCartService carts, CancellationToken cancellationToken) =>
            Results.Ok(await carts.GetActiveCartsAsync(crmId, cancellationToken)));

        api.MapGet("/members/{crmId}/history", async (string crmId, MemberCartService carts, CancellationToken cancellationToken) =>
            Results.Ok(await carts.GetHistoryAsync(crmId, cancellationToken)));

        api.MapPost("/carts", async (CreateCartRequest request, MemberCartService carts, CancellationToken cancellationToken) =>
        {
            var cart = await carts.CreateCartAsync(request, cancellationToken);
            return Results.Created($"{RoutePrefix}/carts/{cart.Id}", cart);
        });

        api.MapPut("/carts/{cartId}", async (string cartId, UpdateCartRequest request, MemberCartService carts, CancellationToken cancellationToken) =>
            Results.Ok(await carts.UpdateCartAsync(cartId, request, cancellationToken)));

        api.MapDelete("/carts/{cartId}", async (string cartId, string? crmId, long version, MemberCartService carts, CancellationToken cancellationToken) =>
        {
            await carts.DeleteCartAsync(cartId, crmId, version, cancellationToken);
            return Results.NoContent();
        });

        api.MapPost("/checkout/session", async (StartCheckoutRequest request, CheckoutService checkout, CancellationToken cancellationToken) =>
            Results.Ok(await checkout.StartCheckoutAsync(request, cancellationToken)));

        api.MapPost("/checkout/details", async (SubmitPaymentDetailsRequest request, CheckoutService checkout, CancellationToken cancellationToken) =>
            Results.Ok(await checkout.SubmitDetailsAsync(request, cancellationToken)));

        api.MapGet("/checkout/status/{paymentId}", async (string paymentId, CheckoutService checkout, CancellationToken cancellationToken) =>
            Results.Ok(await checkout.GetStatusAsync(paymentId, cancellationToken)));

        if (paymentGateway == PaymentGatewayMode.Stub)
        {
            api.MapPost("/checkout/stub/{paymentId}/notify", async (string paymentId, SimulatePaymentRequest request, StubPaymentSimulator simulator, CancellationToken cancellationToken) =>
                Results.Ok(new { outcome = (await simulator.SimulateAsync(paymentId, request.Outcome, cancellationToken)).ToString() }));
        }

        api.MapPost("/notifications/adyen", async (HttpRequest request, AdyenNotificationModule notifications, CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            await notifications.HandleAsync(payload, cancellationToken);
            return Results.Text(AdyenNotificationModule.AcceptedResponse);
        });

        return app;
    }

    private static async ValueTask<object?> TranslateErrorsAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DigitalSiteEndpoints));
        try
        {
            return await next(context);
        }
        catch (DigitalSiteRequestException exception)
        {
            logger.LogInformation("Request {Path} rejected with {StatusCode}: {Reason}", context.HttpContext.Request.Path, exception.StatusCode, exception.Message);
            return Error(exception.StatusCode, exception.Message);
        }
        catch (FormatException exception)
        {
            logger.LogWarning("Request {Path} had an unreadable body: {Reason}", context.HttpContext.Request.Path, exception.Message);
            return Error(StatusCodes.Status400BadRequest, "The request body is not valid");
        }
        catch (ConcurrentModificationException)
        {
            return Error(StatusCodes.Status409Conflict, "This cart changed since you last saw it. Refresh and try again.");
        }
        catch (Exception exception) when (exception is CommerceToolsException or HttpRequestException)
        {
            logger.LogError(exception, "commercetools request failed for {Path}", context.HttpContext.Request.Path);
            return Error(StatusCodes.Status502BadGateway, "The commerce platform is unavailable");
        }
        catch (PaymentGatewayException exception)
        {
            logger.LogError("Payment gateway request failed for {Path}: {Reason}", context.HttpContext.Request.Path, exception.Message);
            return Error(StatusCodes.Status502BadGateway, "The payment provider is unavailable");
        }
    }

    private static IResult Error(int statusCode, string message) => Results.Json(new { error = message }, statusCode: statusCode);
}
