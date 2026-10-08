using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Core.Contracts;

/// <summary>
/// Payload of a <c>ProductHoldingChange</c> event; mirrors <c>contracts/product-holding-change-v1.schema.json</c>.
/// </summary>
public sealed class ProductHoldingChangeData
{
    /// <summary>Gets or sets the contact identifier in the source system.</summary>
    [Required(ErrorMessage = "ContactId is required")]
    public string? ContactId { get; set; }

    /// <summary>Gets or sets the holding or product identifier.</summary>
    [Required(ErrorMessage = "HoldingId is required")]
    public string? HoldingId { get; set; }

    /// <summary>Gets or sets the product classification, one of <see cref="ProductTypes"/>.</summary>
    [Required(ErrorMessage = "ProductType is required")]
    [AllowedValues("insurance", "parks-resorts", "carwash", "roadside-assistance", "other", ErrorMessage = "ProductType is not a known product type")]
    public string? ProductType { get; set; }

    /// <summary>Gets or sets the action performed on the holding.</summary>
    [Required(ErrorMessage = "Action is required")]
    [AllowedValues("created", "modified", "removed", ErrorMessage = "Action must be created, modified or removed")]
    public string? Action { get; set; }

    /// <summary>Gets or sets product-specific fields.</summary>
    public IDictionary<string, object?>? HoldingData { get; set; }

    /// <summary>Product type values allowed by the contract.</summary>
    public static class ProductTypes
    {
        /// <summary>Roadside Assistance cover.</summary>
        public const string RoadsideAssistance = "roadside-assistance";
    }

    /// <summary>Action values allowed by the contract.</summary>
    public static class Actions
    {
        /// <summary>A new holding was created.</summary>
        public const string Created = "created";
    }
}
