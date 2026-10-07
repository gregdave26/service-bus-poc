# Service Bus POC Dashboard (`ui/`)

Node.js/Express backend with a React/Vite + MUI frontend. Each tab is a feature folder under `server/` (see [ADR-012](../docs/decisions/012-feature-based-backend-structure.md)).

## Prerequisites: RAC GitHub Packages access

The Digital Site tab uses `@racwa/react-components`, which is published to GitHub Packages ([ADR-013](../docs/decisions/013-digital-site-racwa-component-library.md)). `ui/.npmrc` maps the `@racwa` scope to the registry. Your token goes in your **user** `~/.npmrc`, never in the repo:

```
//npm.pkg.github.com/:_authToken=<GitHub PAT with read:packages, SSO-authorised for racwa>
```

Then run `npm install`, `npm test`, and `npm start`.

## Digital Site tab

This is a cut-down RACWA Roadside Assistance purchase flow: Quick check → Vehicle details → Confirm cover → Payment plan → Confirmation.

- Demo regos: `1ANURAG`, `1ABC123`, `1RAC000`, `1UTE999` (from `data/rsa-catalog.json`). You can also skip the vehicle step.
- Checkout saves the order to SQLite and publishes `ProductHoldingChange` (`productType: roadside-assistance`) to `contact.events`. If publishing fails, the order is kept with status `publish_failed`.

| Variable | Default | Purpose |
| --- | --- | --- |
| `DIGITAL_SITE_DB_PATH` | `ui/data/digital-site.db` | SQLite order store |
| `DIGITAL_SITE_LOG_DIR` | shared log directory | Feature log output |

API: `GET /api/digital-site/catalog`, `GET /api/digital-site/vehicles/:rego`, `POST /api/digital-site/orders`, `GET /api/digital-site/orders`, `GET /api/digital-site/orders/:id`.
