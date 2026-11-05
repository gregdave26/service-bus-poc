# ADR 015: Startup Payment Feature Management

**Status:** Accepted
**Date:** 2026-10-21

## Context

Payment integration already has three modes: Stub, Adyen and AdyenComponent.
Previously, a failed startup connectivity check automatically selected simulated
payments. This checkpoint deliberately replaces that behavior with explicit,
Development-only permission. Environment variables remain the configuration
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
Fallback requires `DigitalSite__AllowDevelopmentAdyenFallback=true` and the
Development hosting environment. Only transient startup connectivity failures
qualify: transport failures, HTTP timeouts, and gateway responses HTTP 408, 429
or 5xx. Authentication and authorisation rejections (HTTP 401 and 403), other
rejections (for example HTTP 400 or 404), missing or invalid configuration,
cancellation, unexpected failures, and payment request failures all fail startup
or the request and never select Stub. Status and a persistent UI warning
distinguish fallback from deliberate Stub selection. Selecting real Adyen does
not select live versus sandbox.

The startup check runs as a hosted service that completes before the host
begins serving requests, so no request reaches Adyen before the gateway is
verified or a permitted fallback is selected. Runtime verification showed
requests held until the check finished, then answered through the selected
gateway.

## Consequences

- Existing environment setups continue working without adding a flag.
- Startup failure behavior intentionally changes even when the flag is absent:
  operators must explicitly opt in to Development fallback or repair connectivity.
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
- Removing the flag restores legacy mode selection, not automatic fallback.
  Revert the payment checkpoint if the stricter fallback policy must be undone.

## Trade-offs

The standalone Boolean configuration is intentionally limited. Azure migration
will require adding its configuration provider and identity/RBAC before startup
evaluation. Modern flag definitions, filters or live switching require revisiting
the evaluator and workflow consistency; this is not a zero-code migration.

## Related Decisions

- [ADR-006: Environment variables](006-configuration-env-vars.md)
- [ADR-014: Digital Site commerce flow](014-digital-site-commerce-flow.md)
- [Configuration and fallback guide](../digital-site-configuration.md)
