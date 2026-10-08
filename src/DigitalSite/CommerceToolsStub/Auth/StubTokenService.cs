using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ServiceBusPoc.Core.Configuration;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Auth;

/// <summary>
/// Issues and validates opaque bearer tokens for the client-credentials grant, so the shared client's
/// authentication path is exercised locally.
/// </summary>
public sealed class StubTokenService
{
    /// <summary>The token lifetime reported to clients, matching commercetools (48 hours).</summary>
    public const int ExpiresInSeconds = 172800;

    private readonly CommerceToolsStubSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();

    /// <summary>Initializes a new instance of the <see cref="StubTokenService"/> class.</summary>
    public StubTokenService(IOptions<CommerceToolsStubSettings> settings, TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    /// <summary>Gets a value indicating whether API calls require a token.</summary>
    public bool IsEnabled => _settings.RequiresAuthentication;

    /// <summary>Issues a token when the Basic credentials match.</summary>
    /// <param name="authorizationHeader">The <c>Authorization</c> header value.</param>
    /// <returns>The token, or <see langword="null"/> when the credentials are wrong.</returns>
    public string? TryIssue(string? authorizationHeader)
    {
        if (!IsEnabled || authorizationHeader is null || !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader["Basic ".Length..].Trim()));
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = $"{_settings.ClientId}:{_settings.ClientSecret}";
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(decoded), Encoding.UTF8.GetBytes(expected)))
        {
            return null;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _tokens[token] = _timeProvider.GetUtcNow().AddSeconds(ExpiresInSeconds);
        return token;
    }

    /// <summary>Gets a value indicating whether the request is authorised.</summary>
    /// <param name="authorizationHeader">The <c>Authorization</c> header value.</param>
    /// <returns><see langword="true"/> when authentication is disabled or the bearer token is valid.</returns>
    public bool IsAuthorized(string? authorizationHeader)
    {
        if (!IsEnabled)
        {
            return true;
        }

        if (authorizationHeader is null || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        return _tokens.TryGetValue(token, out var expiresAt) && _timeProvider.GetUtcNow() < expiresAt;
    }
}
