# ADR 015: Startup Payment Feature Management

**Status:** Accepted
**Date:** 2026-10-21

## Context

Payment integration already has three modes: Stub, Adyen and AdyenComponent.
Development can optionally fall back to simulated payments after a failed
startup connectivity check. Environment variables remain the configuration
source under ADR-006.

## Problem Statement

Adopt a standard feature-management library without losing existing modes,
requiring Azure for local development, or introducing unsafe mid-payment switches.

## Options Considered

- Continue with typed mode settings only: simplest, but no standard flag API.
- Microsoft.FeatureManagement with environment-backed configuration: selected.
- Add Azure App Configuration now: unnecessary infrastructure for this phase.
- OpenFeature with a separate provider: useful for vendor neutrality but not
  currently required.

## Decision

Add Microsoft.FeatureManagement to CommerceApi. Evaluate the Boolean
`FeatureManagement__UseRealAdyen` once, asynchronously, before gateway service
registration. Use the library's standalone configuration provider and feature
manager at the composition root, avoiding a temporary DI container and avoiding
registering or validating an unselected real gateway.

- Absent flag: preserve `DigitalSite__PaymentGateway` and its Stub default.
- False: select Stub regardless of the valid legacy mode.
- True: retain AdyenComponent when configured; otherwise select Adyen.
- Invalid modes/flags fail startup. Conditional flags are not supported.

Capture the selected mode in options registration and runtime state. All
request-time services use that snapshot; configuration changes require restart.
The existing Development-only fallback permission and visible reporting remain
independent. Selecting real Adyen does not select live versus sandbox.

## Consequences

- Existing environment setups continue working without adding a flag.
- Explicit flags override valid legacy modes according to a documented table.
- Payment adapter, SDK setup, validation and status use the same selected mode.
- No live refresh, targeting, rollout percentages or Azure resources are added.
- Other integrations are separate implementation slices.

## Risks

- An operator might expect live switching: documentation explicitly requires
  restart and tests verify snapshot behaviour.
- False can intentionally override a real mode: status and startup logs expose
  the resolved gateway; this is not an automatic fallback.
- Credentials remain external configuration and must never be stored in flags.

## Trade-offs

The standalone Boolean configuration is intentionally limited. Azure migration
will require adding its configuration provider and identity/RBAC before startup
evaluation. Modern flag definitions, filters or live switching require revisiting
the evaluator and workflow consistency; this is not a zero-code migration.

## Related Decisions

- [ADR-006: Environment variables](006-configuration-env-vars.md)
- [ADR-014: Digital Site commerce flow](014-digital-site-commerce-flow.md)
- [Configuration and fallback guide](../digital-site-configuration.md)
