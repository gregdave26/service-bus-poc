# Service Bus POC Dashboard (`ui/`)

Node.js/Express backend with a React/Vite + MUI frontend. Each tab is a feature folder under `server/` (see [ADR-012](../docs/decisions/012-feature-based-backend-structure.md)).

## Prerequisites: RAC GitHub Packages access

The Digital Site tab uses `@racwa/react-components`, which is published to GitHub Packages ([ADR-013](../docs/decisions/013-digital-site-racwa-component-library.md)). `ui/.npmrc` maps the `@racwa` scope to the registry. Your token goes in your **user** `~/.npmrc`, never in the repo:

```
//npm.pkg.github.com/:_authToken=<GitHub PAT with read:packages, SSO-authorised for racwa>
```

Then run `npm install`, `npm test`, and `npm start`.


## Digital Site tab

This is a cut-down RACWA Roadside Assistance purchase flow, backed by the .NET commerce apps in `src/DigitalSite` ([ADR-014](../docs/decisions/014-digital-site-commerce-flow.md)):

Quick check → Vehicle details → Confirm cover → Payment plan → Payment → Confirmation.

The browser calls the **CommerceApi** directly (CORS). The Node server only tells it where that API is, through `GET /api/config` (`digitalSiteApiBaseUrl`). `scripts/run-dashboard.ps1` starts everything:

- the commercetools stub (`:5201`)
- the CommerceApi (`:5200`)
- the CartProcessor and the FulfilmentStub

How the flow works:

1. **Quick check** generates a mock CRM id (`CRM-XXXXXXXX`), which is used as the commercetools cart `anonymousId`. **New member** re-rolls it. If the member has an active cart, you can **Resume** it or **Start fresh**, which deletes the cart.
2. **Vehicle details**: the demo regos are `1ANURAG`, `1ABC123`, `1RAC000` and `1UTE999`. You can also skip this step.
3. **Confirm cover** creates the commercetools cart. Coming back and changing the cover or vehicle updates the cart, using its version.
4. **Payment plan** (annual only) reviews the cart total and the terms, then creates a checkout session. This creates a commercetools payment, and an Adyen session when Adyen is enabled.
5. **Payment** shows one of two forms:
   - **Stub gateway (default):** Approve, Decline and Pending buttons. Each one sends a signed Adyen-format notification through the real webhook module.
   - **Adyen:** the `@adyen/adyen-web` Drop-in. Card data and 3D Secure stay inside Adyen.
6. **Confirmation** polls `GET /checkout/status/{paymentId}`. It shows the order number only after the verified webhook has updated the payment and the CartProcessor has created the order. The order then moves from Open to Complete when the FulfilmentStub provisions it, and a `ProductHoldingChange` appears on `contact.events`.

The **Member history** panel lists the commercetools active carts and orders for the current CRM id. Flow state is kept in `sessionStorage`, so switching tabs or returning from a payment redirect resumes the flow.

| Variable | Where | Default | Purpose |
| --- | --- | --- | --- |
| `DIGITAL_SITE_API_BASE_URL` | Node dashboard | `http://localhost:5200` | CommerceApi URL given to the browser |
| `DigitalSite__PaymentGateway` | CommerceApi | `Stub` | `Stub` or `Adyen` (feature toggle) |
| `DigitalSite__AllowedOrigins` | CommerceApi | `http://localhost:5100` | CORS origins (comma-separated) |
| `CommerceTools__ApiUrl` / `__ProjectKey` | CommerceApi, processors | — / `rac-rsa-poc` | commercetools (stub) API |
| `CommerceTools__AuthUrl`, `__ClientId`, `__ClientSecret`, `__Scope` | CommerceApi, processors | unset | OAuth client credentials for a real commercetools project |
| `Adyen__MerchantAccount`, `__ApiKey`, `__ClientKey`, `__HmacKey`, `__Environment` | CommerceApi | `Environment=test` | Adyen credentials; set them in your user environment only |
| `Adyen__CheckoutApiUrl`, `__ApimSubscriptionKey` | CommerceApi | unset | Route Adyen calls through RAC APIM instead of directly |

### Trying Adyen (test environment)

1. In the Adyen Customer Area (test), create an API credential. Note its API key and client key, and add `http://localhost:5100` to the client key's **allowed origins**.
2. Expose the CommerceApi publicly, for example with `devtunnel host -p 5200 --allow-anonymous`. Then add a **Standard webhook** pointing at `https://<tunnel>/api/digital-site/notifications/adyen`, with an HMAC key.
3. Set `DigitalSite__PaymentGateway=Adyen` and the `Adyen__*` variables in your user environment, then run `scripts/run-dashboard.ps1`.
4. Pay with an [Adyen test card](https://docs.adyen.com/development-resources/testing/test-card-numbers), for example `4111 1111 4555 1142`, expiry `03/30`, CVC `737`. The test card page also lists 3D Secure 2 and refusal cards.
