# ADR 003: Carwash API Testing - Mock Pulse Client

**Status:** APPROVED (2026-09-15)  
**Date:** 2026-09-15  
**Decided By:** Product Owner  

---

## Context

Carwash receives contact events from Service Bus and exposes HTTP API endpoint(s) for the Pulse system to call. The POC must validate this integration without requiring real Pulse infrastructure.

Architecture:
1. Carwash is a Service Bus consumer (receives contact events)
2. Carwash stores contacts in-memory
3. Carwash exposes HTTP API (e.g., `POST /contacts`, `GET /contacts/{id}`)
4. Pulse calls Carwash API with contact requests

## Problem Statement

How should we test Carwash API integration without requiring real Pulse infrastructure?

- MVP cannot depend on external Pulse system being available
- Must validate Carwash API contract and endpoint behavior
- Must be reproducible and deterministic in `run-local-poc.ps1`
- Pulse integration is out of scope for MVP (Pulse is external system)

## Options Considered

### A. Mock Pulse Client (Selected)
- Carwash exposes actual HTTP endpoints (e.g., `POST /contacts`)
- Create a test mock Pulse client in scenario verifier that calls Carwash API
- Validate request/response shape and HTTP status codes
- **Pros:** Realistic API testing; self-contained; no external dependencies
- **Cons:** Requires implementing mock HTTP client in verifier

### B. Manual Testing Only
- Carwash exposes endpoints
- Manual testing with curl or Postman during development
- No automated validation
- **Pros:** Minimal code in MVP
- **Cons:** Not reproducible; can't validate in `run-local-poc.ps1`; easy to break

### C. Real Pulse API Calls
- Implement actual Pulse HTTP client immediately
- Call real Pulse endpoints during MVP
- **Pros:** Production-realistic
- **Cons:** Requires Pulse credentials; external dependency; Pulse availability blocks dev

## Decision

**Use mock Pulse client for MVP.**

- Carwash exposes actual HTTP API endpoints
- Scenario verifier includes a mock Pulse client that makes requests to Carwash API
- Mock client validates:
  - Correct HTTP methods and status codes
  - Request/response JSON shape
  - Contact data transformation
  - Error handling (e.g., missing contact ID)
- No real Pulse calls; fully self-contained

## Consequences

### Positive
- Carwash API is real and testable (valid endpoints)
- Full integration testing without external dependencies
- Mock client can run in scenario verifier; validates end-to-end flow
- Pulse integration requirement captured in API contract
- Zero friction; no Pulse credentials or infrastructure needed
- Post-MVP: Replace mock client with real Pulse HTTP client

### Negative
- Mock client doesn't exercise real Pulse API behavior
- Doesn't discover Pulse API incompatibilities during MVP
- Requires writing mock client code in verifier
- Pulse integration deferred to post-MVP

### Mitigation
- Document Pulse API contract upfront (request/response schemas)
- Write mock client to match expected Pulse API shape
- Post-MVP: Implement real Pulse client and integration tests
- Flag discrepancies between mock and real Pulse in Phase 5+ ADR

## Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Real Pulse API has different contract than mock | Medium | Document expected API contract; Phase 5 integration testing validates |
| Mock client is too permissive; masks bugs | Medium | Mock validates schema strictly; use data annotations + validation |
| Pulse unavailable blocks post-MVP integration | Low | Post-MVP integration is separate workstream; real client tested in Phase 5 |

## Trade-Offs

- **Testing Completeness vs. MVP Speed:** MVP uses mock; real Pulse integration tested post-MVP
- **Self-Contained vs. Realistic:** Mock eliminates external dependency; trade for slightly less realistic testing

## Related Decisions

- **ADR 002:** In-memory Carwash storage (feeds mock Pulse client with contact data)
- **ADR 001:** Emulator-first (mock Pulse client runs in scenario verifier against local emulator)

## Implementation Notes

```csharp
// Mock Pulse client (in scenario verifier)
public class MockPulseClient
{
    private readonly HttpClient _httpClient;
    
    public MockPulseClient(string carwashApiBaseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(carwashApiBaseUrl) };
    }
    
    // Call Carwash API to create/update contact
    public async Task<HttpResponseMessage> UpsertContactAsync(Contact contact)
    {
        var json = JsonSerializer.Serialize(contact);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.PostAsync("/contacts", content);
    }
    
    // Call Carwash API to retrieve contact
    public async Task<Contact> GetContactAsync(string contactId)
    {
        var response = await _httpClient.GetAsync($"/contacts/{contactId}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Contact>(json);
    }
}
```

Carwash API (ASP.NET Minimal APIs or console-based HTTP listener):
```csharp
// POST /contacts — Create/update contact
app.MapPost("/contacts", async (Contact contact, ICarwashContactRepository repo) =>
{
    var saved = await repo.UpsertAsync(contact);
    return Results.Ok(saved);
});

// GET /contacts/{id} — Retrieve contact
app.MapGet("/contacts/{id}", async (string id, ICarwashContactRepository repo) =>
{
    var contact = await repo.GetAsync(id);
    return contact is not null ? Results.Ok(contact) : Results.NotFound();
});
```

## Post-MVP Integration Notes

Phase 5 will replace mock with real Pulse HTTP client:
- Use `HttpClientFactory` for lifecycle management
- Implement retry logic and circuit breaker (Polly)
- Document Pulse API authentication (if required)
- Integration tests against staging Pulse endpoints

## Sign-Off

- ✅ Product Owner (2026-09-15)
