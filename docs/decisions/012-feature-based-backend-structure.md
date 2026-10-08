# ADR 012: Feature-Based Backend Structure

**Status:** APPROVED (2026-10-06)

## Context

The UI has three tabs: Contact Events, Local (POS) Processing and Rostering (a fourth, Digital Site, was added later — see ADR-013). The backend was organised by technical role: four consumer projects sat beside Core and Producer in `src/`, and `ui/server.js` (~850 lines) mixed dashboard, contact-event, POS and rostering code. A legacy C# `ServiceBusPoc.Dashboard` duplicated the Node.js dashboard (see `DASHBOARD_NODEJS_MIGRATION.md`).

## Problem Statement

How do we structure the backend so each tab owns its code and a new tab follows the same layout?

## Options Considered

1. **Keep the flat layout** – no churn, but new tabs keep growing `server.js` and `src/`.
2. **Group by feature (selected)** – one folder/namespace per tab; consumers stay separate executables beneath the Contact Events feature.
3. **Merge consumers into one project** – fewer projects, but supersedes ADR-007 and loses independent deployment.

## Decision

### .NET (`src/`)

- `src/ContactEvents/Producer` → `ServiceBusPoc.ContactEvents.Producer`
- `src/ContactEvents/Verifier` → `ServiceBusPoc.ContactEvents.Verifier`
- `src/ContactEvents/Consumers/{Carwash,DigitalChannels,Insurance,ParksResorts}` → `ServiceBusPoc.ContactEvents.Consumers.<Name>`
- `src/DigitalSite/{Shared,CommerceToolsStub,CommerceApi,CartProcessor,FulfilmentStub}` → `ServiceBusPoc.DigitalSite.<Name>` (added by ADR-014)
- `src/ServiceBusPoc.Core` remains shared infrastructure.
- Folder names are short on purpose: the full project name repeated in the folder pushed `bin` output past the Windows 260-character path limit in deep worktrees.
- The C# `ServiceBusPoc.Dashboard` project and its tests are deleted. `Core/Dashboard` (heartbeat reporting) stays; consumers report to the Node.js dashboard.

### Node.js (`ui/server/`)

`ui/server.js` is a thin entry point. Each feature has the same shape:

```
ui/server/
  app.js              composes middleware, /api/config and feature routers
  shared/             paths, per-feature file logger
  contactEvents/      router.js, publisher.js, messageStore.js, messageTypes.js, log.js, index.js
  pos/                router.js, database.js, receipt*.js, catalog.js, log.js, index.js
  rostering/          router.js, batchProcessor.js, inputDefinitions.js, lineup.js, log.js, index.js
  digitalSite/        index.js (client config only: the tab calls the .NET CommerceApi in src/DigitalSite, see ADR-014)
```

### Adding a new tab

1. Create `ui/server/<feature>/` with `log.js`, `router.js` (exports an Express router and optional client config) and `index.js`.
2. Mount the router (and merge client config) in `ui/server/app.js`.
3. For .NET work, add `src/<Feature>/` with `ServiceBusPoc.<Feature>.*` projects.

## Consequences

- Public API routes, environment variables and runtime behaviour are unchanged; this is a structural refactor, so no feature toggle is needed.
- Scripts locate projects by `.csproj` name rather than hard-coded folder.

## Risks

- Long absolute paths can still break builds in very deeply nested checkouts.
- Contact-specific types still live in `ServiceBusPoc.Core`; moving them is deferred.

## Trade-offs

- More folders and longer project names in exchange for clear ownership per tab.
