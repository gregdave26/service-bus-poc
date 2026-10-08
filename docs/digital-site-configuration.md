# Digital Site configuration and Adyen Drop-in guide

This guide explains how the Digital Site purchase flow is configured and how
the Adyen Web Drop-in fits into the payment page. The default local
configuration is deliberately **stub-only**: no Adyen account, credentials or
card data are required to run the local flow.

The implementation is described in [ADR-014](decisions/014-digital-site-commerce-flow.md).

## 1. Local stub flow

The recommended local setup is started by:

```powershell
.\scripts\run-dashboard.ps1
```

The script starts:

| Service | URL | Purpose |
| --- | --- | --- |
| Dashboard | `http://localhost:5100` | React UI and Node dashboard APIs |
| CommerceTools stub | `http://localhost:5201` | Local subset of the commercetools HTTP API |
| CommerceApi | `http://localhost:5200` | Digital Site backend-for-frontend |
| CartProcessor | Service Bus consumer | Creates an order after an authorised payment |
| FulfilmentStub | Service Bus consumer | Simulates D365 provisioning |

The local payment configuration is:

```powershell
$env:DigitalSite__PaymentGateway = "Stub"
```

`Stub` is also the default when the variable is absent. The payment page then
shows **Approve payment**, **Decline payment** and **Leave pending** buttons.
Those buttons create signed, Adyen-shaped notifications and send them through
the same notification and CommerceTools update path used by the real gateway.

The message saying that “Adyen is not configured” on this page means that the
stub mode is active; it is not a payment failure.

## 2. Configuration ownership

The systems have different responsibilities:

| Configuration | Owner | Purpose |
| --- | --- | --- |
| `CommerceTools__ApiUrl`, `CommerceTools__ProjectKey` | CommerceApi and processors | CommerceTools API endpoint and project |
| `DigitalSite__PaymentGateway` | CommerceApi | Selects `Stub` or `Adyen` |
| `DigitalSite__AllowedOrigins` | CommerceApi | CORS origins allowed to call CommerceApi |
| `DIGITAL_SITE_API_BASE_URL` | Dashboard Node server | URL exposed to the browser as `digitalSiteApiBaseUrl` |
| `Adyen__MerchantAccount` | CommerceApi | Adyen merchant account used by server-side Checkout API calls |
| `Adyen__ApiKey` | CommerceApi only | Server-side Adyen Checkout API credential |
| `Adyen__ClientKey` | CommerceApi response to browser | Public client key used to initialise Adyen Web |
| `Adyen__HmacKey` | CommerceApi only | Server-side webhook signature verification |
| `Adyen__Environment` | CommerceApi | `test` or `live` |
| `Adyen__CheckoutApiUrl` | CommerceApi | Optional direct or APIM Checkout API base URL |
| `Adyen__ApimSubscriptionKey` | CommerceApi only | Optional APIM subscription credential |

Never put `Adyen__ApiKey`, `Adyen__HmacKey` or
`Adyen__ApimSubscriptionKey` in the frontend bundle, browser storage or
source control. Only the client key is intended for browser use.

## 3. What CommerceTools does and does not do

CommerceTools is the cart, payment-record and order system in this flow. The
CommerceApi creates a CommerceTools payment with:

- the cart total and currency;
- a payment key used as the Adyen merchant reference;
- `paymentMethodInfo.paymentInterface = "adyen"`;
- custom fields for the cart and correlation identifiers.

CommerceTools does **not** collect card details or authorise a card. The
payment interface identifies the external provider; it does not configure or
replace the provider. Adyen remains responsible for the payment session, card
entry, 3D Secure actions and payment authorisation.

The verified Adyen webhook updates the CommerceTools payment transaction. The
CartProcessor creates the order only after it observes a successful
authorisation. A browser callback is only an interim UI signal.

## 4. Enabling the Adyen path

The current code supports an Adyen mode, but enabling it requires Adyen test
credentials and a reachable webhook endpoint. Do this only in a test
environment.

Set these variables in the CommerceApi process environment, not in source
control:

```powershell
$env:DigitalSite__PaymentGateway = "Adyen"
$env:Adyen__Environment = "test"
$env:Adyen__MerchantAccount = "<merchant-account>"
$env:Adyen__ApiKey = "<server-side-checkout-api-key>"
$env:Adyen__ClientKey = "<web-client-key>"
$env:Adyen__HmacKey = "<standard-webhook-hmac-key>"
```

The CommerceApi validates these values at startup. In direct mode,
`Adyen__ApiKey` is required. If RAC APIM injects the Adyen API key, configure
the APIM URL and subscription key instead:

```powershell
$env:Adyen__CheckoutApiUrl = "https://<apim-host>/<checkout-api-path>"
$env:Adyen__ApimSubscriptionKey = "<apim-subscription-key>"
Remove-Item Env:Adyen__ApiKey -ErrorAction SilentlyContinue
```

The client key's allowed origin must include the dashboard origin, normally
`http://localhost:5100`. The CommerceApi CORS configuration must also include
that origin:

```powershell
$env:DigitalSite__AllowedOrigins = "http://localhost:5100"
```

Configure an Adyen Standard webhook to call:

```text
https://<public-commerce-api-host>/api/digital-site/notifications/adyen
```

The endpoint must be reachable by Adyen. For local testing, use an approved
development tunnel rather than exposing a development machine directly.

## 5. Payment-page integration sequence

The payment page is not supposed to call Adyen's server API from React. The
sequence is:

1. The shopper accepts the payment terms.
2. The browser calls `POST /api/digital-site/checkout/session`.
3. CommerceApi validates the cart and creates the CommerceTools payment.
4. CommerceApi calls Adyen Checkout `/sessions` through the configured
   `IPaymentGatewayAdapter`.
5. CommerceApi returns the opaque session data and public client key:

   ```json
   {
     "paymentId": "ct-payment-id",
     "sessionId": "CS...",
     "sessionData": "...",
     "clientKey": "test_...",
     "environment": "test",
     "gateway": "Adyen",
     "amount": 310.00,
     "correlationId": "..."
   }
   ```

6. React initialises `AdyenCheckout` with the session and mounts `Dropin`.
7. Adyen hosts the sensitive card fields in its secure component/iframe and
   handles any 3D Secure action.
8. `onPaymentCompleted` and `onPaymentFailed` update the UI only. They do not
   create an order.
9. For a redirect result, the browser returns with `redirectResult`. The UI
   restores its session state and calls `submitDetails`.
10. Adyen sends the Standard webhook. CommerceApi verifies its HMAC and updates
    the CommerceTools payment.
11. CartProcessor creates the order, then FulfilmentStub completes it.
12. The confirmation page polls
    `GET /api/digital-site/checkout/status/{paymentId}` and displays the order
    only after backend confirmation.

## 6. Existing Drop-in component

The repository already contains the integration in
[`ui/src/AdyenDropIn.jsx`](../ui/src/AdyenDropIn.jsx). The essential pattern is:

```jsx
import { AdyenCheckout, Card, Dropin } from "@adyen/adyen-web";
import "@adyen/adyen-web/styles/adyen.css";

const configuration = {
  session: {
    id: checkout.sessionId,
    sessionData: checkout.sessionData,
  },
  clientKey: checkout.clientKey,
  environment: checkout.environment,
  amount: {
    value: checkout.price.centAmount,
    currency: checkout.price.currencyCode,
  },
  countryCode: "AU",
  locale: "en-AU",
  onPaymentCompleted: (result) => {
    // Interim browser result only. Poll CommerceApi for the authoritative result.
    reportInterimResult(result.resultCode);
  },
  onPaymentFailed: (result) => {
    reportInterimResult(result?.resultCode ?? "Refused");
  },
  onError: (error) => {
    showPaymentError(error.message);
  },
};

const checkoutInstance = await AdyenCheckout(configuration);
const dropin = new Dropin(checkoutInstance, {
  paymentMethodComponents: [Card],
}).mount(containerElement);
```

Important implementation details:

- Mount only after the session response is available.
- Keep the Drop-in in a `useRef` container.
- Unmount it in the React effect cleanup to avoid duplicate instances when
  the payment page is revisited.
- Import Adyen's stylesheet.
- Do not log or store card data, CVC, encrypted card fields or session secrets.
- Use the `clientKey` returned by CommerceApi; do not hard-code it in React.
- Treat the browser result as pending until the status endpoint reports the
  verified backend state.

For redirect payment methods, the current component restores the checkout
session and invokes:

```js
adyenCheckout.submitDetails({
  details: { redirectResult },
});
```

The payment page should remain usable if Adyen reports an action or an
intermediate result. The backend webhook remains the source of truth.

## 7. Troubleshooting

### The page says “Adyen is not configured”

Check the gateway value from `GET /api/digital-site/config`. If it is `Stub`,
the stub panel is working as designed. If it is `Adyen`, check the CommerceApi
startup logs for missing `MerchantAccount`, `ClientKey`, `HmacKey` or
`ApiKey`/APIM configuration.

### The Drop-in does not initialise

Check all of the following:

- CommerceApi is running and `/checkout/session` returns `sessionId`,
  `sessionData`, `clientKey` and `environment`.
- The client key's allowed origin contains the dashboard URL.
- The browser can call CommerceApi and the CORS origin matches exactly.
- `@adyen/adyen-web` is installed and its CSS is loaded.
- The amount uses minor units (`centAmount`) and the currency is `AUD`.

### The browser says payment completed but no order appears

This is expected until the webhook is processed. Check:

1. the CommerceApi webhook endpoint received the notification;
2. HMAC verification succeeded;
3. the CommerceTools payment transaction changed to authorised;
4. the `cart-processor` subscription is running;
5. the `fulfilment-d365-stub` subscription is running.

