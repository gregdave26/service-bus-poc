using System.Text.Json.Serialization;

namespace ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

/// <summary>
/// A commercetools update action. Only the actions the POC uses are modelled; the
/// <c>action</c> discriminator matches the commercetools HTTP API.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "action", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(AddLineItemAction), "addLineItem")]
[JsonDerivedType(typeof(RemoveLineItemAction), "removeLineItem")]
[JsonDerivedType(typeof(AddPaymentAction), "addPayment")]
[JsonDerivedType(typeof(SetCustomFieldAction), "setCustomField")]
[JsonDerivedType(typeof(AddTransactionAction), "addTransaction")]
[JsonDerivedType(typeof(ChangeTransactionStateAction), "changeTransactionState")]
[JsonDerivedType(typeof(AddInterfaceInteractionAction), "addInterfaceInteraction")]
[JsonDerivedType(typeof(ChangeOrderStateAction), "changeOrderState")]
public abstract record AbstractUpdateAction;

/// <summary>Adds a line item to a cart.</summary>
/// <param name="Sku">The variant SKU.</param>
/// <param name="Quantity">The quantity.</param>
public sealed record AddLineItemAction(string Sku, long Quantity = 1) : AbstractUpdateAction;

/// <summary>Removes a line item from a cart.</summary>
/// <param name="LineItemId">The line item id.</param>
public sealed record RemoveLineItemAction(string LineItemId) : AbstractUpdateAction;

/// <summary>Associates a payment with a cart.</summary>
/// <param name="Payment">The payment reference.</param>
public sealed record AddPaymentAction(ResourceReference Payment) : AbstractUpdateAction;

/// <summary>Sets or clears one custom field.</summary>
/// <param name="Name">The field name.</param>
/// <param name="Value">The value; <see langword="null"/> removes the field.</param>
public sealed record SetCustomFieldAction(string Name, object? Value) : AbstractUpdateAction;

/// <summary>Adds a transaction to a payment.</summary>
/// <param name="Transaction">The transaction.</param>
public sealed record AddTransactionAction(TransactionDraft Transaction) : AbstractUpdateAction;

/// <summary>Changes the state of a payment transaction.</summary>
/// <param name="TransactionId">The transaction id.</param>
/// <param name="State">The new state.</param>
public sealed record ChangeTransactionStateAction(string TransactionId, string State) : AbstractUpdateAction;

/// <summary>Records a raw provider interaction on a payment.</summary>
/// <param name="Type">The interaction custom type.</param>
/// <param name="Fields">The interaction fields.</param>
public sealed record AddInterfaceInteractionAction(ResourceReference Type, Dictionary<string, object?> Fields) : AbstractUpdateAction;

/// <summary>Changes the state of an order.</summary>
/// <param name="OrderState">The new state.</param>
public sealed record ChangeOrderStateAction(string OrderState) : AbstractUpdateAction;

/// <summary>Request body for updating a resource.</summary>
/// <param name="Version">The expected current version.</param>
/// <param name="Actions">The actions to apply in order.</param>
public sealed record UpdateRequest(long Version, IReadOnlyList<AbstractUpdateAction> Actions);
