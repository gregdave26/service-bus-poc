namespace ServiceBusPoc.DigitalSite.CommerceApi.Payments;

/// <summary>
/// Routes Adyen calls through RAC's API Management proxy, which holds the Adyen API key itself.
/// The proxy keeps any incoming <c>X-API-Key</c>, so the header must be removed rather than left blank.
/// </summary>
public sealed class ApimSubscriptionHandler : DelegatingHandler
{
    /// <summary>The APIM subscription key header.</summary>
    public const string SubscriptionKeyHeader = "Ocp-Apim-Subscription-Key";

    /// <summary>The Adyen API key header.</summary>
    public const string AdyenApiKeyHeader = "X-API-Key";

    private readonly string _subscriptionKey;

    /// <summary>Initializes a new instance of the <see cref="ApimSubscriptionHandler"/> class.</summary>
    /// <param name="subscriptionKey">The APIM subscription key.</param>
    public ApimSubscriptionHandler(string subscriptionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionKey);
        _subscriptionKey = subscriptionKey;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(AdyenApiKeyHeader);
        request.Headers.Remove(SubscriptionKeyHeader);
        request.Headers.Add(SubscriptionKeyHeader, _subscriptionKey);
        return base.SendAsync(request, cancellationToken);
    }
}
