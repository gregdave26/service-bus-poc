# ADR 013: Digital Site Tab and the RACWA Component Library

> **Update (ADR-014):** the `ui/server/digitalSite` backend described below was replaced by the .NET commerce flow (commercetools API stub, CommerceApi, Adyen Drop-in). The component library decisions still apply.

**Status:** APPROVED (2026-10-07)

## Context

The dashboard needs a "Digital Site" tab that mimics a cut-down version of the RACWA Roadside Assistance (RSA) purchase flow. A completed purchase should produce a real `contact.events` message, so the tab demonstrates a digital channel as an event producer.

The live flow lives in `racwa/rac-digital` (`frontend/apps/sales/src/app/(flows)/roadside-assistance`). It uses `@racwa/react-library`, which is unpublished, built on MUI 9 and coupled to Next.js. RAC also publishes `@racwa/react-components` (repo `racwa/design-system`) to GitHub Packages. That package is built on MUI 7, which the dashboard already uses.

## Problem Statement

How do we reuse RAC's internal MUI components in the dashboard without taking on the full Next.js monorepo, and how does the tab fit the feature-per-tab structure from ADR-012?

## Options Considered

1. **Copy markup from `rac-digital` into plain MUI.** No new dependencies, but the look drifts from the real site and we would be maintaining copied code.
2. **Use `@racwa/react-library`.** This matches production most closely, but it is unpublished, needs MUI 9 (the dashboard is on MUI 7) and depends on Next.js.
3. **Use the published `@racwa/react-components` v12 (selected).** It is on the same MUI major as the dashboard, is versioned, and provides `RacwaStepperTemplate`, radio/checkbox lists, inputs and notifications.

## Decision

- Install `@racwa/react-components` 12.0.3 and its peer dependencies from GitHub Packages. `ui/.npmrc` maps only the `@racwa` scope; each developer's token stays in their user `~/.npmrc`.
- The library pins exact peer versions (React 19.2.3, MUI 7.3.10), so the dashboard pins them too (`--save-exact`). Some peers still declare `react ^18` (framer-motion, react-number-format, react-snap-carousel, react-dropzone). npm `overrides` (`$react` / `$react-dom`) resolve these instead of using `legacy-peer-deps`.
- The RACWA theme is applied with a nested MUI `ThemeProvider` around the tab only. `RacwaThemeProvider` would inject a global `CssBaseline` and restyle the other tabs.
- The tab is loaded with `React.lazy`, so the library ships in its own chunk and the other tabs don't pay for it.
- The backend follows ADR-012 in `ui/server/digitalSite/`:
  - Catalog, vehicle lookup, order service, SQLite repository (`DIGITAL_SITE_DB_PATH`) and router.
  - Prices are recalculated on the server.
  - Orders are persisted before publishing a `ProductHoldingChange` (`productType: roadside-assistance`), and the publish status is recorded.
- `contracts/product-holding-change-v1.schema.json` adds `roadside-assistance` to the `productType` enum.

## Consequences

- The UI matches RAC components without copying source code.
- `npm install` in `ui/` now needs a GitHub PAT with `read:packages` and RACWA org SSO authorisation (see `ui/README.md`). CI is not affected, because it only runs .NET tests.
- Upgrading React or MUI must follow the library's pinned peer versions.
- If Service Bus is unavailable, the order is still saved with status `publish_failed`, and the UI shows a warning.

## Risks

- `@racwa/react-components` may diverge from the production `react-library`. Migrating later means swapping imports per step component, and the flow state and backend are unaffected.
- The `overrides` run React-18-declared packages on React 19. This works in this flow, but those packages are not officially tested against React 19.
- Vehicle lookup, payment and the member identity (a generated `contactId`) are simulated.

## Trade-offs

- A heavier dependency footprint and registry authentication, in exchange for components that look and behave like RACWA.
- No feature toggle: the tab is a self-contained demo with no effect on existing behaviour.
