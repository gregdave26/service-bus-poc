using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Adyen.Util;
using ServiceBusPoc.DigitalSite.CommerceApi.Payments;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;
using AdyenAmount = Adyen.Webhooks.Models.Amount;
using AdyenNotificationItem = Adyen.Webhooks.Models.NotificationRequestItem;

namespace ServiceBusPoc.DigitalSite.CommerceApi.Notifications;

/// <summary>
/// Reads Adyen standard webhooks and checks the HMAC signature Adyen adds to every item.
/// The stub gateway signs its simulated notifications the same way, so both modes share this path.
/// </summary>
public sealed class AdyenNotificationReader
{
    /// <summary>The additional-data key that carries the item signature.</summary>
    public const string HmacSignatureKey = "hmacSignature";

    /// <summary>The additional-data key that says whether a <c>CANCEL_OR_REFUND</c> was a cancel or a refund.</summary>
    public const string ModificationActionKey = "modification.action";

    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _hmacKey;
    private readonly HmacValidator _hmacValidator = new();

    /// <summary>Initializes a new instance of the <see cref="AdyenNotificationReader"/> class.</summary>
    /// <param name="hmacKey">The hex-encoded HMAC key configured for the webhook.</param>
    public AdyenNotificationReader(string hmacKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hmacKey);
        _hmacKey = hmacKey;
    }

    /// <summary>Parses a payload and keeps only the items with a valid signature.</summary>
    /// <param name="payload">The raw request body.</param>
    /// <returns>The authentic notifications and the number rejected.</returns>
    /// <exception cref="FormatException">The payload is not an Adyen notification.</exception>
    public WebhookVerification Verify(string payload)
    {
        var request = Parse(payload);
        var authentic = new List<PaymentNotification>();
        var rejected = 0;
        foreach (var item in request.NotificationItems.Select(container => container.NotificationRequestItem))
        {
            if (item is not null && IsAuthentic(item))
            {
                authentic.Add(ToNotification(item));
            }
            else
            {
                rejected++;
            }
        }

        return new WebhookVerification(authentic, rejected);
    }

    /// <summary>Signs a notification item in place; used by the stub gateway.</summary>
    /// <param name="item">The item to sign.</param>
    public void Sign(AdyenNotificationItemPayload item)
    {
        item.AdditionalData ??= [];
        item.AdditionalData[HmacSignatureKey] = _hmacValidator.CalculateHmac(ToAdyenItem(item), _hmacKey);
    }

    private static AdyenNotificationRequestPayload Parse(string payload)
    {
        try
        {
            var request = JsonSerializer.Deserialize<AdyenNotificationRequestPayload>(payload, SerializerOptions);
            return request?.NotificationItems is { Count: > 0 }
                ? request
                : throw new FormatException("The notification has no items");
        }
        catch (JsonException exception)
        {
            throw new FormatException("The notification is not valid JSON", exception);
        }
    }

    private bool IsAuthentic(AdyenNotificationItemPayload item)
    {
        if (string.IsNullOrWhiteSpace(item.PspReference)
            || string.IsNullOrWhiteSpace(item.EventCode)
            || item.AdditionalData?.ContainsKey(HmacSignatureKey) != true)
        {
            return false;
        }

        try
        {
            return _hmacValidator.IsValidHmac(ToAdyenItem(item), _hmacKey);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return false;
        }
    }

    private static AdyenNotificationItem ToAdyenItem(AdyenNotificationItemPayload item) =>
        new()
        {
            PspReference = item.PspReference,
            OriginalReference = item.OriginalReference,
            MerchantAccountCode = item.MerchantAccountCode,
            MerchantReference = item.MerchantReference,
            Amount = new AdyenAmount { Currency = item.Amount?.Currency, Value = item.Amount?.Value },
            EventCode = item.EventCode,
            Success = item.IsSuccess,
            AdditionalData = item.AdditionalData,
        };

    private static PaymentNotification ToNotification(AdyenNotificationItemPayload item) =>
        new(
            item.EventCode!,
            item.IsSuccess,
            item.PspReference!,
            NullIfEmpty(item.OriginalReference),
            item.MerchantReference ?? string.Empty,
            item.MerchantAccountCode,
            Money.FromCents(item.Amount?.Currency ?? string.Empty, item.Amount?.Value ?? 0),
            DateTimeOffset.TryParse(item.EventDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var eventDate) ? eventDate : null,
            NullIfEmpty(item.Reason),
            NullIfEmpty(item.PaymentMethod),
            item.AdditionalData?.GetValueOrDefault(ModificationActionKey));

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>The Adyen standard webhook body.</summary>
public sealed class AdyenNotificationRequestPayload
{
    /// <summary>Gets or sets <c>"true"</c> for live notifications.</summary>
    public string? Live { get; set; }

    /// <summary>Gets or sets the notification items.</summary>
    public List<AdyenNotificationContainerPayload> NotificationItems { get; set; } = [];
}

/// <summary>Wraps one notification item.</summary>
public sealed class AdyenNotificationContainerPayload
{
    /// <summary>Gets or sets the item.</summary>
    [JsonPropertyName("NotificationRequestItem")]
    public AdyenNotificationItemPayload? NotificationRequestItem { get; set; }
}

/// <summary>One Adyen notification item, as sent on the wire.</summary>
public sealed class AdyenNotificationItemPayload
{
    /// <summary>Gets or sets additional data, including the HMAC signature.</summary>
    public Dictionary<string, string>? AdditionalData { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public AdyenAmountPayload? Amount { get; set; }

    /// <summary>Gets or sets the event code.</summary>
    public string? EventCode { get; set; }

    /// <summary>Gets or sets the event date.</summary>
    public string? EventDate { get; set; }

    /// <summary>Gets or sets the merchant account.</summary>
    public string? MerchantAccountCode { get; set; }

    /// <summary>Gets or sets our reference.</summary>
    public string? MerchantReference { get; set; }

    /// <summary>Gets or sets the original PSP reference, for modifications.</summary>
    public string? OriginalReference { get; set; }

    /// <summary>Gets or sets the PSP reference.</summary>
    public string? PspReference { get; set; }

    /// <summary>Gets or sets the reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets <c>"true"</c> or <c>"false"</c>.</summary>
    public string? Success { get; set; }

    /// <summary>Gets or sets the payment method.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Gets a value indicating whether <see cref="Success"/> is true.</summary>
    [JsonIgnore]
    public bool IsSuccess => string.Equals(Success, "true", StringComparison.OrdinalIgnoreCase);
}

/// <summary>An Adyen amount in minor units.</summary>
public sealed class AdyenAmountPayload
{
    /// <summary>Gets or sets the currency.</summary>
    public string? Currency { get; set; }

    /// <summary>Gets or sets the value in minor units.</summary>
    public long? Value { get; set; }
}
