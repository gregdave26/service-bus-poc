# Adyen Checkout Sessions smoke tests

This Bruno collection exercises the Digital Site API's existing Adyen Checkout
Sessions boundary. It does not call Adyen directly and does not contain API
keys, HMAC keys, APIM subscription keys, or captured PSP references.

## Setup

1. Start the Digital Site Commerce API.
2. Open `tools/adyen/bruno` as a Bruno collection.
3. Select the `local` environment.
4. Set `crmId` and `cartId` in `environments/local.bru` to an existing payable
   cart, or set them through Bruno's environment UI.
5. Run the requests in this order:

   - `health`
   - `config`
   - `start-session`
   - `status`
   - `submit-details` only when Adyen returns an action requiring details

The `start-session` request creates an Adyen session through the application.
It does not authorize or capture a payment. The browser still needs to mount
the Adyen Component and submit the shopper's payment details.

`submit-details` is intentionally a manual request. Populate `paymentId` and
either `redirectResult` or `threeDSResult` only from the shopper action. Do not
commit those values.

## Direct Adyen and APIM modes

The application can be configured to call Adyen directly with
`Adyen__ApiKey`, or through an APIM endpoint with
`Adyen__CheckoutApiUrl` and `Adyen__ApimSubscriptionKey`. This collection only
calls the application API, so the credential boundary remains on the server.

## Payment finality

The session and details responses are interim results. The authoritative
payment state is established by the signed Adyen webhook received at
`POST /api/digital-site/notifications/adyen`. A successful HTTP response from
Adyen is not by itself proof that funds were captured.

Use the existing `scripts/test-adyen-connectivity.ps1` for a credential and
Checkout API connectivity check without creating a shopper session through the
browser flow.
