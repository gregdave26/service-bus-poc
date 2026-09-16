# ADR 009: Carwash Consumer and Verification API Boundary

**Status:** APPROVED (2026-09-16)  
**Date:** 2026-09-16  
**Decided By:** Product Owner

## Context

Carwash participates in the POC through two integration points:

1. A Service Bus consumer receives events from the `carwash` subscription when `hasCarwashProduct=true`.
2. An HTTP API exposes `POST /carwash/v1/verify` for Pulse to verify a RAC member ID.

Earlier plans incorrectly described Carwash as calling Pulse or made the consumer call the verification API.

## Problem Statement

The implementation needs an unambiguous dependency direction that matches the approved API contract and avoids coupling unrelated runtime paths.

## Options Considered

### A. Independent consumer and API paths

- Pulse or mock Pulse calls the Carwash verification API.
- Carwash independently consumes filtered Service Bus events.
- Neither path invokes the other.

### B. Consumer calls the Carwash API

- Couples message processing to an HTTP endpoint in the same service.
- Adds an unnecessary failure mode and does not match the approved flow.

### C. Carwash calls Pulse

- Reverses the approved API direction.
- Requires a Pulse client and contract that are outside the MVP.

## Decision

Use independent consumer and API paths.

- `POST /carwash/v1/verify` is called by Pulse or mock Pulse.
- The Carwash consumer processes `hasCarwashProduct=true` messages independently.
- The MVP does not add a Carwash-to-Pulse client.
- The MVP does not use Service Bus messages as the backing store for verification.
- The verifier may test both paths, but it must not imply a runtime dependency between them.

## Consequences

### Positive

- Matches the approved API contract and dependency direction.
- Keeps message processing and HTTP verification independently testable.
- Avoids unnecessary HTTP self-calls and storage coupling.

### Negative

- The POC does not demonstrate a combined workflow between a received event and a later verification request.
- Any future shared data requirement needs a separate design decision.

## Supersedes

- ADR 002 where it couples Service Bus contact storage to the API.
- ADR 003 where it proposes generic contact endpoints rather than `POST /carwash/v1/verify`.

## Validation

- Exercise all 8 Service Bus routing combinations.
- Test the Carwash consumer as part of routing validation.
- Test `POST /carwash/v1/verify` with a mock Pulse caller as a separate scenario.
