# ADR 011: Carwash MemberCentral Verification and Event Publishing

**Status:** APPROVED (2026-09-23)  
**Date:** 2026-09-23  
**Decided By:** Product Owner

## Context

Carwash does not own the authoritative customer or membership data. MemberCentral is the partner system that can verify whether a membership is valid and eligible for Carwash.

The earlier design made Carwash a consumer of contact events filtered by `hasCarwashProduct=true`. That would imply that Carwash needs to maintain or process a local customer or entitlement representation. The intended business flow does not require that:

1. Pulse calls Carwash to verify a membership.
2. Carwash queries the MemberCentral partner API.
3. Pulse performs the wash.
4. Pulse informs CarWash of success/failure (and any additional details)
4. Carwash publishes a business event when the wash completes.

## Problem Statement

Should Carwash consume contact events from Azure Service Bus, or should it query MemberCentral directly and publish completed-wash events?

The decision must avoid:

- Treating contact events as the authoritative membership store.
- Duplicating MemberCentral membership data in Carwash.
- Adding a consumer that has no business responsibility.
- Coupling membership verification to asynchronous contact synchronization.

## Options Considered

### A. Carwash consumes contact events

Carwash subscribes to `contact.events` and maintains a local customer or product representation.

**Advantages:**

- Local data could support fast lookups.
- Carwash could operate when MemberCentral is unavailable.

**Disadvantages:**

- Creates synchronization and reconciliation responsibilities.
- Risks stale membership decisions.
- Implies Carwash owns or replicates membership data.
- Requires handling replay, ordering, deletion, and correction events.
- Does not match the current business flow.
- MemberCentral should be sole source of truth for member data.

### B. Carwash queries MemberCentral and publishes Carwash events (selected)

Carwash calls MemberCentral synchronously when Pulse requests verification. After a wash completes, Carwash publishes a domain event to Azure Service Bus.

**Advantages:**

- MemberCentral remains the authoritative membership source.
- No local membership database is required.
- Verification reflects current membership state.
- Carwash publishes events about business activity it owns.
- Other systems can subscribe without coupling to Carwash internals.

**Disadvantages:**

- Membership verification depends on MemberCentral availability and latency. What happens if MemberCentral can't be reached?
- Requires authentication, timeout, retry, and circuit-breaker handling.
- Carwash must publish reliably after a wash completes.

### C. Carwash consumes commands from Service Bus

Another system publishes commands such as `carwash.wash.requested`, which Carwash consumes.

This is not required for the current scope. It may be introduced later if an external system needs to initiate wash operations asynchronously.

## Decision

Carwash will not consume the generic contact-event subscription in the MVP.

Carwash will:

1. Expose `POST /carwash/v1/verify` for Pulse or mock Pulse.
2. Delegate membership verification to an injected `IMembershipVerifier`.
3. Use MemberCentral as the future authoritative provider behind that abstraction.
4. Use a deterministic mock verifier for local MVP tests.
5. Publish a Carwash domain event when a wash completes.

The proposed event type is:

```text
carwash.wash.completed
```

The event should contain only data required by downstream consumers, potentially including:

- `washId`
- `membershipNumber` or an approved customer reference
- `completedAt` in UTC
- `locationId`
- Required vehicle information
- A correlation or trace identifier

Membership validity must not be inferred from a contact event or from the presence of `hasCarwashProduct`. The verification result comes from MemberCentral at request time.

## Topic and Routing

For the POC, the event may use the existing Service Bus backbone if demonstrating a shared topic is an explicit objective. A production design should assess a dedicated domain topic such as:

```text
carwash.events
```

The event must not be represented as `contact.updated`; it describes a completed Carwash business activity.

## Consequences

### Positive

- Removes unnecessary Carwash consumer behavior.
- Keeps membership ownership with MemberCentral.
- Separates synchronous verification from asynchronous business-event publication.
- Gives downstream systems a reliable integration point for completed washes.
- Avoids a stale local membership copy in Carwash.

### Negative

- MemberCentral availability affects verification latency and availability.
- Carwash needs a reliable outbound publishing pattern, including retry and recovery handling.
- A future offline-verification requirement would require a separate read-model decision.

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| MemberCentral is unavailable | Explicit timeout, bounded retry, circuit breaker, and documented unavailable response |
| Membership number or licence plate is sensitive | Minimize fields, mask logs, restrict subscriptions, and define retention |
| Wash completion is published more than once | Include a stable `washId` and support idempotent downstream processing |
| Wash completion is lost after the operational transaction | Use an outbox or equivalent durable publication/reconciliation mechanism |
| Future requirements need local verification | Create a separate ADR for membership-domain events and a replicated read model |

## Out of Scope

- Carwash-owned membership persistence.
- Bulk membership synchronization.
- MemberCentral webhook consumption.
- Asynchronous wash commands.
- Production MemberCentral credentials or endpoint configuration.

## Related Decisions

- [ADR 006: Configuration and Secrets](006-configuration-env-vars.md)
- [ADR 007: Separate Applications](007-separate-apps.md)
- [ADR 009: Carwash Integration Boundary](009-carwash-integration-boundary.md)
- [ADR 005: Schema Validation](005-schema-validation.md)

## Supersedes

This ADR supersedes the Carwash consumer portion of [ADR 009](009-carwash-integration-boundary.md). ADR 009 remains valid for the direction of the Pulse-to-Carwash HTTP API and the separation of HTTP and messaging concerns.

## Implementation Notes

The implementation plan should be updated to:

- Remove Carwash from the required `contact.events` consumer list.
- Remove the `carwash` contact-event subscription unless another requirement needs it.
- Define and validate the `carwash.wash.completed` event contract.
- Add a Carwash Service Bus publisher.
- Add a MemberCentral adapter implementing `IMembershipVerifier`.
- Add tests for MemberCentral success, invalid membership, timeout, unavailable-provider, and event-publication behavior.

