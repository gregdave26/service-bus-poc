namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>
/// commercetools <c>CentPrecisionMoney</c>: an amount in the minor unit of its currency.
/// </summary>
public sealed record Money
{
    /// <summary>The only money type this POC uses.</summary>
    public const string CentPrecisionType = "centPrecision";

    private const int CentsPerUnit = 100;

    /// <summary>Gets the money type.</summary>
    public string Type { get; init; } = CentPrecisionType;

    /// <summary>Gets the ISO 4217 currency code.</summary>
    public string CurrencyCode { get; init; } = string.Empty;

    /// <summary>Gets the amount in cents.</summary>
    public long CentAmount { get; init; }

    /// <summary>Gets the number of fraction digits of the currency.</summary>
    public int FractionDigits { get; init; } = 2;

    /// <summary>Creates money from a cent amount.</summary>
    /// <param name="currencyCode">The ISO 4217 currency code.</param>
    /// <param name="centAmount">The amount in cents.</param>
    /// <returns>The money value.</returns>
    public static Money FromCents(string currencyCode, long centAmount) =>
        new() { CurrencyCode = currencyCode, CentAmount = centAmount };

    /// <summary>Gets the amount in major currency units.</summary>
    /// <returns>The decimal amount, for example 310.00.</returns>
    public decimal ToDecimal() => (decimal)CentAmount / CentsPerUnit;

    /// <summary>Gets a value indicating whether both values have the same currency and amount.</summary>
    /// <param name="other">The money to compare.</param>
    /// <returns><see langword="true"/> when currency and amount match.</returns>
    public bool IsSameAmountAs(Money? other) =>
        other is not null
        && CentAmount == other.CentAmount
        && string.Equals(CurrencyCode, other.CurrencyCode, StringComparison.OrdinalIgnoreCase);
}
