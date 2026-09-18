# ADR-010: Browser-Based Live Dashboard for Service Status and Event Publishing

## Status
APPROVED

## Context

The Service Bus POC was initially scoped (ADR-007, PROJECT_BRIEF §2) to exclude UI components, keeping each messaging role as a separate console application. However, observability of the distributed system — particularly real-time visibility into which services are running, whether consumers are filtering correctly, and the ability to test the system by publishing events — requires a lightweight, non-coupled mechanism to expose this information.

Users need to:
1. See which services (producer + 4 consumers) are currently connected and healthy
2. Publish test events with different capability flags and observe consumer filtering in real-time
3. Understand which events were accepted vs. filtered by each subscription

## Problem Statement

Without visibility into live system state and a test publishing interface, operators must infer messaging behavior from logs alone or manually construct Service Bus messages via CLI tools. The existing console-app architecture (by design) prevents adding a shared HTTP endpoint directly to Producer or Consumers; each runs independently. A standalone dashboard solves this without violating the separation-of-concerns principle.

## Options Considered

### Option A: Console-Based Status Report (Rejected)
A script that polls heartbeat logs and prints a refreshing table to the console.
- **Pros:** No new architectural layer, minimal dependencies
- **Pros:** Consistent with "separate console apps" design
- **Cons:** No real-time feedback, no ability to publish events interactively

### Option B: Unified Web Host (ADR-007 Precedent, Rejected)
A monolithic ASP.NET Core host serving all producer/consumer logic plus UI.
- **Cons:** Violates ADR-007 separation principle
- **Cons:** Adds complexity to every role's initialization
- **Cons:** Couples unrelated concerns

### **Option C: Separate Lightweight Dashboard Process (Accepted)**
A dedicated `ServiceBusPoc.Dashboard` application that:
- Runs on its own HTTP listener (HttpListener, not ASP.NET Core)
- Receives heartbeats from Producer/Consumers via best-effort HTTP POST
- Exposes a simple vanilla HTML+JS UI for status viewing and event publishing
- Remains fully optional; Producer/Consumers work perfectly without it

## Decision

**Approved:** Implement Option C — a separate, lightweight dashboard process.

This approach:
- Preserves the separation-of-concerns principle from ADR-007 (each role is still a standalone console app)
- Does not reverse the UI exclusion in PROJECT_BRIEF §2 — it scopes the exclusion to "consumer-facing" UI (webhooks, APIs for external integration), not internal observability tools
- Uses HttpListener (like CarwashApiServer) to avoid adding ASP.NET MVC/SPA overhead
- Makes dashboard availability optional via best-effort heartbeat reporting (if dashboard stops, messaging continues unaffected)
- Enables interactive test publishing without CLI tooling

**Artifacts:**
- New project: `src/ServiceBusPoc.Dashboard` with HTTP server, status registry, and minimal HTML/JS UI
- Core extensions: heartbeat DTOs, `IDashboardReporter` interface, status aging logic
- Producer + Consumer implementations now send real messages and report heartbeats
- ADR-006 compliance: all dashboard config via environment variables (Enabled, Url, Port, HeartbeatIntervalSeconds, etc.)

## Consequences

**Positive:**
- Real-time operational visibility into the distributed system
- Interactive event publishing for testing filter routing
- No architectural burden on existing roles; remains opt-in
- Messaging is fully functional whether dashboard runs or not (best-effort reporting)
- No new external dependencies (uses HttpListener + System.Net)

**Negative:**
- Adds a 9th project/executable to the POC (increases surface area for operations)
- HTTP server in production code (though Dashboard is dev/test only, per PROJECT_BRIEF)
- Heartbeat timeout introduces a small latency in detecting service shutdown (currently 15 seconds)

## Risks

- **Risk:** Dashboard becomes a bottleneck if heartbeat reporting is synchronous
  - **Mitigation:** Heartbeat requests have a 3-second timeout and fail silently; messaging continues unaffected
  
- **Risk:** Status display lags behind actual service state due to heartbeat interval (5 seconds by default)
  - **Mitigation:** Expected and acceptable; this is a monitoring/observability tool, not a control plane

- **Risk:** Operators rely on dashboard for critical operational decisions
  - **Mitigation:** Dashboard is explicitly scoped as observability only; critical decisions must rely on persistent logs and Application Insights

## Trade-Offs

- **Simplicity vs. Visibility:** Adds a process but preserves role separation; trade is favorable because observability is a legitimate need for any distributed system POC
- **Process Count vs. Coupling:** 9 processes instead of 5, but zero coupling compared to a monolithic host
- **Vanilla HTML vs. Framework:** No frontend framework means raw HTML/CSS/JS; acceptable for this internal POC tool

## Related Decisions

- **ADR-007:** Separate apps pattern — this decision does NOT override ADR-007; dashboard is an additional optional sidecar, not a unified host
- **ADR-006:** Config via environment variables — Dashboard inherits this pattern for all settings (Enabled, Url, Port, HeartbeatIntervalSeconds, RequestTimeoutSeconds, OfflineAfterSeconds)
- **ADR-004:** Async-first — Dashboard uses async HTTP listener and async heartbeat reporting to match the overall threading model

## Validation

- Acceptance criteria:
  1. Producer publishes real messages; dashboard shows "Running" + message counts
  2. Each consumer receives only messages matching its broker filter; console logs reflect accepted vs. filtered
  3. Dashboard unavailability does not interrupt Producer or Consumer messaging
  4. Status ages to "Offline" after 15 seconds without a heartbeat
  5. Dashboard publish endpoint successfully sends `EventEnvelope<ContactData>` with user-selected capability flags

- Test coverage: New tests for status aging, heartbeat timeout, consumer filtering, and publish validation
- Manual verification: run emulator + Producer + all 4 consumers + Dashboard; publish events with different flag combinations and confirm filtering correctness in consumer logs

---

**Approved by:** Development Team (Nova, Sage, Milo) + Producer  
**Date:** 2026-09-16  
**Supersedes:** None  
**Superseded by:** (None yet)
