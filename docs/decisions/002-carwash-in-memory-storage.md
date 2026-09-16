# ADR 002: Carwash Contact Storage - In-Memory for MVP

**Status:** SUPERSEDED by ADR 009 (2026-09-16)  
**Date:** 2026-09-15  
**Decided By:** Product Owner  

> ADR 009 removes the proposed runtime coupling between Service Bus contact storage and the Carwash verification API. This document remains as historical context only.

---

## Context

Carwash is a Service Bus consumer that receives contact events and exposes an HTTP API for Pulse to query. The POC must store contact data received from events so the API can serve it to Pulse.

## Problem Statement

How should Carwash persist contact events received from the Service Bus in MVP?

- MVP needs simplicity and minimal dependencies to move fast
- Post-MVP will require persistent storage for production
- Local emulator + console app suggests in-process storage for MVP
- Trade-off between dev speed and realistic persistence

## Options Considered

### A. In-Memory Storage (Selected)
- Store contacts in a `ConcurrentDictionary<string, Contact>` or similar
- Contacts reset on app restart
- Zero external dependencies
- **Pros:** Simple, fast, zero dependencies, deterministic tests
- **Cons:** Data lost on restart; not production-grade

### B. Local Database (SQLite)
- Use Entity Framework Core with SQLite for local development
- Use SQL Server for cloud
- More realistic persistence
- **Pros:** Realistic data durability; easier migration to post-MVP
- **Cons:** More setup; EF Core adds dependency complexity

### C. Cloud Database (Azure SQL/Cosmos)
- Use Azure SQL or Cosmos DB from day 1
- Production-grade but overkill for MVP
- **Pros:** Production-realistic
- **Cons:** Requires Azure setup; higher cost; friction for dev

## Decision

**Use in-memory storage (ConcurrentDictionary) for MVP.**

- Carwash stores received contacts in memory
- HTTP API serves from in-memory collection
- Data persists for duration of Carwash process; resets on restart
- Post-MVP: Add persistent storage layer (EF Core + database)

## Consequences

### Positive
- Minimal code and dependencies (just `System.Collections.Concurrent`)
- Fast to implement and test
- Deterministic test scenarios (no I/O, no database setup)
- Perfect for demonstrating Carwash integration with Pulse in MVP
- No additional infrastructure or credentials needed

### Negative
- Data lost on process restart (acceptable for MVP)
- Not suitable for production workloads
- Doesn't demonstrate persistence (realistic for post-MVP)
- Horizontal scaling (multiple Carwash instances) not supported

### Mitigation
- Document MVP limitation clearly in DEVELOPER.md
- Plan Phase 5+ migration: in-memory → EF Core + database
- Add feature flag to swap storage implementation (post-MVP)
- Test scenario verifier short-lived; restarts acceptable

## Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Product stakeholders expect persistent storage | Medium | Document MVP constraints; plan migration upfront |
| Tests flaky due to race conditions in memory | Low | Use `ConcurrentDictionary` + concurrent test scenarios |
| Scaling beyond single process impossible | Low | Out of scope for MVP; post-MVP concern |

## Trade-Offs

- **Simplicity vs. Realism:** MVP prioritizes speed; post-MVP adds realistic persistence
- **Dev Speed vs. Production-Grade:** In-memory trading for faster implementation

## Related Decisions

- **ADR 001:** Emulator-first (supports in-memory; no persistent store dependency in emulator)
- **ADR 003:** Mock Pulse API (in-memory Carwash feeds mock Pulse calls)

## Implementation Notes

```csharp
// Carwash contact repository (in-memory)
public interface ICarwashContactRepository
{
    Task<Contact> GetAsync(string contactId);
    Task<IEnumerable<Contact>> GetAllAsync();
    Task<Contact> UpsertAsync(Contact contact);
}

// In-memory implementation
public class InMemoryCarwashContactRepository : ICarwashContactRepository
{
    private readonly ConcurrentDictionary<string, Contact> _contacts = new();
    
    public Task<Contact> GetAsync(string contactId) =>
        Task.FromResult(_contacts.TryGetValue(contactId, out var c) ? c : null);
    
    public Task<IEnumerable<Contact>> GetAllAsync() =>
        Task.FromResult(_contacts.Values.AsEnumerable());
    
    public Task<Contact> UpsertAsync(Contact contact)
    {
        _contacts.AddOrUpdate(contact.ContactId, contact, (_, _) => contact);
        return Task.FromResult(contact);
    }
}
```

- Inject `ICarwashContactRepository` into Carwash consumer and API
- Post-MVP: Implement `EFCoreCarwashContactRepository` with database backend

## Sign-Off

- ✅ Product Owner (2026-09-15)
