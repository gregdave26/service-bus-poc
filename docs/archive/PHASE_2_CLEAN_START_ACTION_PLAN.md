# Phase 2 Clean-Start Action Plan

`IMPLEMENTATION_PLAN.md` is the roadmap authority. This document is a concise execution view of its Phase 2 workstreams.

## Current State

- `infra/servicebus/compose.yaml` and `infra/servicebus/config.json` exist.
- Producer and consumer messaging services remain stubs.
- The Carwash verification API is implemented; `CarwashApiServer*Tests` files exist.
- Avoid Git-state assumptions: this workspace is not a Git repository.

## Phase 2 Workstreams

### 2.1 Emulator topology

Use the existing Compose and `config.json` files to start the local Service Bus emulator and define `contact.events` with `digital-channels`, `insurance`, `parks-resorts`, and `carwash` subscriptions. Validate the topology through the local workflow.

### 2.2 Producer

Implement `ProducerService` to construct canonical `contact.events` envelopes, publish them to the topic, and log outcomes. Keep configuration externalized and use the existing contracts.

### 2.3 Consumers

Implement the Digital Channels, Insurance, Parks & Resorts, and Carwash consumers to receive their subscriptions, deserialize and validate messages, process them, and settle messages appropriately. Carwash consumes only messages filtered by `hasCarwashProduct=true`; this is independent of the HTTP verification API.

### 2.4 Carwash verification API — already complete

Carwash exposes `POST /carwash/v1/verify`. Pulse or mock Pulse calls this endpoint. Its request/response validation and mock verification behavior are already implemented. Do not add a Carwash-to-Pulse client or couple this API to the Carwash Service Bus consumer.

### 2.5 Scenario verifier

Implement `VerifierService` to publish deterministic scenarios and assert subscription delivery. Cover all **8** combinations of `hasInsurance`, `hasParksResorts`, and `hasCarwashProduct`, including the all-false and all-true cases, and report pass/fail.

## Phase 3 Tests — in parallel with Phase 2

Develop these tests alongside the implementation work:

- Schema tests for the canonical envelopes and invalid payload handling.
- Filter-routing tests for all 8 boolean combinations and no cross-subscription leakage.
- Settings tests for valid, missing, invalid, and environment-overridden configuration.
- Carwash API contract tests and independent Carwash consumer routing tests.

## Phase 5.1 Local Script

Complete `scripts/run-local-poc.ps1` after the relevant Phase 2 workstreams so one command starts the emulator, runs the verifier's 8 routing combinations, collects results, and cleans up. This completes the local MVP.

## Delivery Guardrails

- Use the canonical contracts in `contracts/`; do not duplicate or diverge them.
- Keep secrets in secure configuration, never source or fixtures.
- Use the emulator for local work; Azure deployment remains outside the local MVP.
- Keep work focused on the roadmap workstreams; no new architectural layers are required.

## Estimates

| Scope | Estimate |
|---|---:|
| Phase 2 | 3–5 days |
| Phase 3, alongside Phase 2 | 2–3 days |
| Phase 5.1 | 1 day |
| **MVP subtotal** | **7–11 days** |

