using ServiceBusPoc.DigitalSite.CommerceToolsStub.Messaging;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Storage;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools.Models;

namespace ServiceBusPoc.DigitalSite.CommerceToolsStub.Domain;

/// <summary>Payment endpoints of the stub; transaction changes emit subscription messages.</summary>
public sealed class PaymentService
{
    private readonly IResourceStore _store;
    private readonly StubWriteLock _writeLock;
    private readonly MessageFactory _messageFactory;
    private readonly IOutboxSignal _outboxSignal;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="PaymentService"/> class.</summary>
    public PaymentService(
        IResourceStore store,
        StubWriteLock writeLock,
        MessageFactory messageFactory,
        IOutboxSignal outboxSignal,
        TimeProvider timeProvider)
    {
        _store = store;
        _writeLock = writeLock;
        _messageFactory = messageFactory;
        _outboxSignal = outboxSignal;
        _timeProvider = timeProvider;
    }

    /// <summary>Gets a payment by id.</summary>
    public async Task<Payment> GetAsync(string id, CancellationToken cancellationToken) =>
        await _store.GetAsync<Payment>(ResourceTypes.Payment, id, cancellationToken)
        ?? throw CommerceStubException.NotFound(ResourceTypes.Payment, id);

    /// <summary>Gets a payment by key.</summary>
    public async Task<Payment> GetByKeyAsync(string key, CancellationToken cancellationToken) =>
        await _store.GetByKeyAsync<Payment>(ResourceTypes.Payment, key, cancellationToken)
        ?? throw CommerceStubException.NotFound(ResourceTypes.Payment, key);

    /// <summary>Creates a payment.</summary>
    public async Task<Payment> CreateAsync(PaymentDraft draft, CancellationToken cancellationToken)
    {
        if (draft.AmountPlanned.CentAmount <= 0 || string.IsNullOrWhiteSpace(draft.AmountPlanned.CurrencyCode))
        {
            throw CommerceStubException.InvalidInput("amountPlanned must be a positive amount with a currency.");
        }

        var now = _timeProvider.GetUtcNow();
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            Version = 1,
            Key = draft.Key,
            AmountPlanned = Money.FromCents(draft.AmountPlanned.CurrencyCode, draft.AmountPlanned.CentAmount),
            PaymentMethodInfo = draft.PaymentMethodInfo,
            Custom = StubResources.ToCustomFields(draft.Custom),
            CreatedAt = now,
            LastModifiedAt = now
        };

        using (await _writeLock.AcquireAsync(cancellationToken))
        {
            if (draft.Key is not null && await _store.GetByKeyAsync<Payment>(ResourceTypes.Payment, draft.Key, cancellationToken) is not null)
            {
                throw CommerceStubException.DuplicateField("key", draft.Key);
            }

            await _store.CommitAsync([new ResourceWrite(ResourceTypes.Payment, payment.Id, payment.Key, payment)], [], cancellationToken);
        }

        return payment;
    }

    /// <summary>Applies update actions to a payment.</summary>
    public async Task<Payment> UpdateAsync(string id, UpdateRequest request, CancellationToken cancellationToken)
    {
        using (await _writeLock.AcquireAsync(cancellationToken))
        {
            var payment = await GetAsync(id, cancellationToken);
            StubResources.EnsureVersion(request.Version, payment.Version);
            payment.Version++;
            payment.LastModifiedAt = _timeProvider.GetUtcNow();

            var messages = new List<PendingMessage>();
            foreach (var action in request.Actions)
            {
                Apply(payment, action, messages);
            }

            await _store.CommitAsync([new ResourceWrite(ResourceTypes.Payment, payment.Id, payment.Key, payment)], messages, cancellationToken);
            if (messages.Count > 0)
            {
                _outboxSignal.Notify();
            }

            return payment;
        }
    }

    private void Apply(Payment payment, AbstractUpdateAction action, List<PendingMessage> messages)
    {
        var reference = ResourceReference.ById(ResourceTypes.Payment, payment.Id);
        var correlationId = StubResources.CorrelationIdOf(payment.Custom);
        switch (action)
        {
            case AddTransactionAction addTransaction:
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    Timestamp = addTransaction.Transaction.Timestamp,
                    Type = addTransaction.Transaction.Type,
                    Amount = addTransaction.Transaction.Amount,
                    InteractionId = addTransaction.Transaction.InteractionId,
                    State = addTransaction.Transaction.State
                };
                payment.Transactions.Add(transaction);
                messages.Add(_messageFactory.Create(
                    CommerceMessageTypes.PaymentTransactionAdded,
                    reference,
                    payment.Version,
                    new Dictionary<string, object?> { ["transaction"] = transaction },
                    correlationId));
                break;
            case ChangeTransactionStateAction changeState:
                var existing = payment.Transactions.FirstOrDefault(item => item.Id == changeState.TransactionId)
                    ?? throw CommerceStubException.InvalidInput($"Transaction '{changeState.TransactionId}' not found.");
                existing.State = changeState.State;
                messages.Add(_messageFactory.Create(
                    CommerceMessageTypes.PaymentTransactionStateChanged,
                    reference,
                    payment.Version,
                    new Dictionary<string, object?> { ["transactionId"] = existing.Id, ["state"] = existing.State },
                    correlationId));
                break;
            case AddInterfaceInteractionAction addInteraction:
                payment.InterfaceInteractions.Add(new CustomFields
                {
                    Type = addInteraction.Type,
                    Fields = addInteraction.Fields
                        .Where(field => field.Value is not null)
                        .ToDictionary(field => field.Key, field => StubResources.ToElement(field.Value))
                });
                break;
            case SetCustomFieldAction setCustomField:
                payment.Custom = StubResources.SetCustomField(payment.Custom, setCustomField.Name, setCustomField.Value);
                break;
            default:
                throw CommerceStubException.InvalidInput($"Action '{action.GetType().Name}' is not supported on payments.");
        }
    }
}
