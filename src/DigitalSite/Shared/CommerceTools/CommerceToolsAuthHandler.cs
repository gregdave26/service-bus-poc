using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools;

/// <summary>
/// Adds a commercetools bearer token obtained with the OAuth client-credentials grant,
/// caching it until shortly before it expires.
/// </summary>
public sealed class CommerceToolsAuthHandler : DelegatingHandler
{
    /// <summary>The named HTTP client used for token requests.</summary>
    public const string TokenClientName = "commercetools-auth";

    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CommerceToolsSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <summary>Initializes a new instance of the <see cref="CommerceToolsAuthHandler"/> class.</summary>
    /// <param name="httpClientFactory">Creates the token client.</param>
    /// <param name="settings">The commercetools settings.</param>
    /// <param name="timeProvider">The clock.</param>
    public CommerceToolsAuthHandler(
        IHttpClientFactory httpClientFactory,
        IOptions<CommerceToolsSettings> settings,
        TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tokenLock.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && _timeProvider.GetUtcNow() < _expiresAt)
            {
                return _accessToken;
            }

            var token = await RequestTokenAsync(cancellationToken);
            var accessToken = token.AccessToken!;
            _accessToken = accessToken;
            _expiresAt = _timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn) - ExpiryMargin;
            return accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string> { ["grant_type"] = "client_credentials" };
        if (!string.IsNullOrWhiteSpace(_settings.Scope))
        {
            form["scope"] = _settings.Scope;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.AuthUrl!.TrimEnd('/')}/oauth/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var client = _httpClientFactory.CreateClient(TokenClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new CommerceToolsException(response.StatusCode, "commercetools token request failed.", [CommerceToolsErrorCodes.InvalidToken]);
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            throw new CommerceToolsException(response.StatusCode, "commercetools token response had no access token.", [CommerceToolsErrorCodes.InvalidToken]);
        }

        return token;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
