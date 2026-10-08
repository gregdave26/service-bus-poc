# ADR 014: Digital Site Commerce Flow — commercetools API Stub, Adyen Sessions Drop-in, Event-Driven Order Creation

**Status:** APPROVED (2026-10-07)

**Supersedes:** the `ui/server/digitalSite` backend described in [ADR-013](013-digital-site-racwa-component-library.md). The RACWA component decisions in ADR-013 still apply.

## Context

The Digital Site tab (ADR-013) completed a purchase by writing an order to SQLite in the Node dashboard and publishing `ProductHoldingChange` straight away. No payment was taken and there was no cart.

The target RAC website architecture (Confluence: *RAC Website – CommerceTools – Adyen Flow POC*) uses:

- **commercetools (CT)** as a headless cart and order store, with the member's CRM id as the cart `anonymousId`
- **Adyen** for payment, using the Sessions flow and a Drop-in web component
- the verified Adyen webhook, not the browser, as the authority that a payment succeeded

We have no CT project or Adyen credentials yet, but the POC must show the end-to-end shape and be easy to point at the real services later.

## Problem Statement

How do we model the cart → payment → order → fulfilment journey so that:

- it runs locally with no external accounts
- it follows the CT HTTP API and the official CT–Adyen connector conventions closely enough that swapping in the real services is a configuration change
- card data never touches RAC code?

## Options Considered

1. **Keep the Node backend and fake payment.** Least effort, but it shows none of the CT/Adyen integration risks.
2. **Use the official `commercetools-adyen-integration` connector (Node) against a real CT project.** Highest fidelity, but it needs CT and Adyen accounts now, and it does not fit the repo's .NET services.
3. **Write .NET services behind a gateway-neutral adapter, against a CT API stub that copies the documented HTTP API subset (selected).** Runs offline and keeps the connector's conventions. Real CT and Adyen are each one configuration change away.

## Decision

### Apps (`src/DigitalSite/`)

| Project | Role |
|---|---|
| `Shared` | `CommerceToolsClient` (typed `HttpClient`, optional OAuth client credentials), the CT models and update actions, and `CommerceSubscriptionRunner` for `commerce.events` consumers |
| `CommerceToolsStub` | A subset of the CT HTTP API: `/{projectKey}/product-projections`, `carts`, `payments`, `orders`, `/oauth/token`. CT JSON shapes, version checks, `409 ConcurrentModification`, and `where` equality/`and` predicates. SQLite store. It publishes CT-style `MessageDeliveryPayload` messages to `commerce.events` through an outbox, imitating a CT Subscription with an Azure Service Bus destination. |
| `CommerceApi` | The backend-for-frontend (`/api/digital-site/*`) that the browser calls directly (CORS). It also hosts the Adyen notification module (`POST /api/digital-site/notifications/adyen`). |
| `CartProcessor` | Consumes `PaymentTransactionAdded` / `PaymentTransactionStateChanged`. On a successful Authorization whose amount equals the cart total, it calls `createOrderFromCart`, then publishes `ProductHoldingChange` (`roadside-assistance`) to `contact.events` once. |
| `FulfilmentStub` | Consumes `OrderCreated`, logs a "D365 F&O provisioning (stub)" entry, then sets the order state to `Complete`. |

### Payment gateway abstraction (feature toggle)

`IPaymentGatewayAdapter` exposes `CreateCheckoutSessionAsync`, `HandlePaymentDetailsAsync`, `VerifyWebhook` and `MapWebhookToPaymentUpdate`. It is selected by `DigitalSite__PaymentGateway`:

- `Stub` (default): no external calls. The UI shows Approve / Decline / Pending buttons. Each button makes `POST /checkout/stub/{paymentId}/notify` build an HMAC-signed Adyen-format notification and send it through the **same** notification module that Adyen would call.
- `Adyen`: the official Adyen .NET library (`Adyen` 36.0.0, Checkout `/sessions` and `/payments/details`). It can go direct, or through RAC's APIM Adyen proxy via `Adyen__CheckoutApiUrl` and `Adyen__ApimSubscriptionKey`. Startup validation requires `MerchantAccount`, `ClientKey` and `HmacKey`, plus either `ApiKey` or an APIM URL.

Rollback is a configuration change: set the toggle back to `Stub`. Both states are covered by tests.

### Conventions borrowed from the official CT–Adyen connector

- `payment.key` is the Adyen `merchantReference`.
- `transaction.interactionId` is the `pspReference`.
- Adyen `eventCode`/`success` are mapped onto CT transaction types and states (a port of `adyen-events.json`).
- State changes only move forward, except that an Authorization can be corrected from Failure to Success.
- Unmapped events are recorded only as an `interfaceInteraction` (type `ctp-adyen-integration-interaction-notification`).
- `metadata.ctProjectKey` and the cart, payment and correlation ids are sent to Adyen in `metadata`.

### The verified webhook is the only order authority

The browser's `onPaymentCompleted` / `onPaymentFailed` result is shown only as an interim status. The confirmation page polls `GET /checkout/status/{paymentId}`. It shows an order number only after the CartProcessor has created the order from a CT payment that the notification module updated after verifying the HMAC.

Idempotency comes from three things:

- the transaction state rules
- CT version checks with conflict retry
- the order `holdingEventId` custom field, which ensures `ProductHoldingChange` is published once

### Frontend (`ui/`)

- The flow is: Quick check → Vehicle → Cover → Payment plan → Payment → Confirmation.
- Quick check generates a mock CRM id (`CRM-XXXXXXXX`, with a "New member" re-roll). If the member has an active cart, it offers Resume or Start fresh.
- Leaving the Cover step creates the cart, or updates it with its version if the cover or vehicle changed.
- Payment mounts `@adyen/adyen-web` 6.46.0 `Dropin` (with `Card`) in the lazy Digital Site chunk, or the stub panel.
- Flow state is kept in `sessionStorage`. A redirect payment method returns to `?tab=digitalSite&redirectResult=…`, and Adyen completes it with `submitDetails`.
- A member history panel lists the CT active carts and orders for the CRM id.
- The Node server only supplies `digitalSiteApiBaseUrl` (`DIGITAL_SITE_API_BASE_URL`) through `/api/config`.

### Correlation and logging

A correlation id is created with the cart and carried through the following, and it appears in log scopes:

- the cart and payment custom fields
- Adyen `metadata`
- the Service Bus `CorrelationId`
- `ProductHoldingChange`

The ids we log are crmId (a mock, non-PII value), cartId, paymentId/key, pspReference, eventCode, orderId/orderNumber and the CT message type/sequenceNumber. We never log card data, the HMAC key, API keys or session data.

## Consequences

- PCI scope stays with Adyen: card entry and 3D Secure happen inside the Adyen Drop-in, and RAC code only sees references.
- Four new runnable apps plus a library. `scripts/run-dashboard.ps1` starts them:
  - commercetools stub on `:5201`
  - CommerceApi on `:5200`
  - CartProcessor and FulfilmentStub on the `commerce.events` subscriptions `cart-processor` and `fulfilment-d365-stub`
- New dependencies:
  - `Adyen` (the official library)
  - `Microsoft.Data.Sqlite` (the stub store)
  - `@adyen/adyen-web` (the Drop-in)
  - `Microsoft.AspNetCore.Mvc.Testing` (tests only)
  
  We chose not to use the commercetools .NET SDK. A thin client over the documented HTTP API keeps the stub and real CT interchangeable.
- The Node `ui/server/digitalSite` backend, its SQLite store and `ui/data/rsa-catalog.json` were removed.

### Pointing at real services

**commercetools.** Set:

- `CommerceTools__ApiUrl` and `CommerceTools__ProjectKey`
- `CommerceTools__AuthUrl`, `ClientId`, `ClientSecret` and `Scope`

The project must also define:

- the ProductType `roadside-assistance` with a `Level` enum attribute, and the four products and SKUs
- a tax category and a `digital` shipping method
- the custom types `rsa-cart`, `rsa-payment`, `rsa-checkout-session-interaction` and `ctp-adyen-integration-interaction-notification`
- a Subscription that sends `PaymentTransactionAdded`, `PaymentTransactionStateChanged` and `OrderCreated` messages to the `commerce.events` Azure Service Bus topic

**Adyen.** Set:

- `DigitalSite__PaymentGateway=Adyen`
- `Adyen__MerchantAccount`, `ApiKey`, `ClientKey`, `HmacKey` and `Environment=test`

Add the dashboard origin to the client key's allowed origins. Then expose CommerceApi through a dev tunnel and register `https://<tunnel>/api/digital-site/notifications/adyen` as a Standard webhook with HMAC.

## Risks

- **Stub fidelity:** only the documented subset is copied (no GraphQL, a limited `where` grammar, and no tax calculation: prices are treated as tax-inclusive). The `CommerceToolsClient` tests pin request shapes to the CT docs, but behaviour against a real project is unverified until credentials exist.
- **Adyen integration is unverified end to end** until test credentials and a webhook tunnel are available. The adapter is covered by tests against mocked Adyen services.
- **Asynchronous order creation:** the shopper can see "Payment authorised, creating your order" for a few seconds. If the processors are down, the payment is authorised but no order exists. The CT payment keeps the state, so the order is created when the processor resumes (messages wait on the subscription).
- **Mock identity:** the CRM id is random and unauthenticated. This is acceptable for a POC, but it is not a member-matching design.

## Trade-offs

- More moving parts (five projects, a second topic) in exchange for a journey that matches the target architecture and can be swapped onto real CT and Adyen by configuration.
- Polling the status endpoint is simpler than pushing updates to the browser, at the cost of a short delay.
