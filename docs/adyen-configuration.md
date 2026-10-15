To configure the real Adyen Card Component, you need values from the **Adyen Customer Area test environment**.

## 1. Create or access an Adyen test account

Use the Adyen test Customer Area:

<https://ca-test.adyen.com/>

You will need an Adyen test account with access to:

- API credentials
- Client key configuration
- Webhook configuration
- Merchant account details

If you do not have access, an Adyen account administrator must grant it or create the credentials for you.

## 2. Obtain the merchant account name

In the Customer Area:

1. Open **Account** or **Developers**.
2. Locate your merchant account name.
3. Copy the exact merchant account identifier.

Configure:

```dotenv
Adyen__MerchantAccount=your-test-merchant-account
```

This is usually an account name such as:

```dotenv
Adyen__MerchantAccount=RACWAAccountECOM
```

Do not assume this value; copy it from Adyen.

## 3. Create an API credential

In the Customer Area:

1. Go to **Developers → API credentials**.
2. Create a new API credential for Checkout.
3. Give it a descriptive name, for example:
   - `service-bus-poc-local-test`
4. Copy the generated API key immediately.

Configure:

```dotenv
Adyen__ApiKey=your-test-api-key
```

The API key is a **private server-side secret**. It must not be placed in frontend code, committed to Git, or exposed through the browser.

For this project, the credential must be able to create Checkout sessions and process payment details through the Adyen Checkout API. Use the minimum permissions required by your Adyen account policy.

## 4. Obtain the client key

In the same API credential or client-key configuration area:

1. Open the credential’s **Client key** settings.
2. Add the allowed origin:

```text
http://localhost:5100
```

3. If you use another dashboard port or hostname, add that exact origin too. For example:

```text
http://localhost:5100
https://your-dev-host.example.com
```

4. Copy the client key.

Configure:

```dotenv
Adyen__ClientKey=your-test-client-key
```

The client key is designed to be used by browser components, but it should still be supplied through the backend’s checkout-session response rather than hard-coded into the frontend.

Important:

- Include the scheme, host, and port in allowed origins.
- Do not use a path such as `/digital-site`.
- Do not use a trailing slash unless Adyen specifically accepts it.
- `http://localhost:5100` and `http://localhost:5200` are different origins.

## 5. Create the Standard webhook and obtain the HMAC key

The current backend verifies Adyen notifications, so you need a Standard webhook.

In the Customer Area:

1. Go to **Developers → Webhooks**.
2. Create a **Standard notification** webhook.
3. Set the URL to an externally reachable HTTPS endpoint, for example:

```text
https://your-dev-tunnel.example.com/api/digital-site/notifications/adyen
```

4. Generate or copy the webhook HMAC key.
5. Configure:

```dotenv
Adyen__HmacKey=your-webhook-hmac-key
```

For local development, Adyen cannot call `localhost` directly. You need an HTTPS tunnel, such as a development tunnel or another approved tunnelling service, pointing to the CommerceApi port.

The webhook endpoint must ultimately reach:

```text
/api/digital-site/notifications/adyen
```

The HMAC key is private and must remain server-side.

## 6. Configure the local `.env`

Copy the committed template:

```powershell
Copy-Item .env.example .env
```

Then replace the placeholders:

```dotenv
DigitalSite__PaymentGateway=Adyen

Adyen__Environment=test
Adyen__MerchantAccount=your-real-test-merchant-account
Adyen__ApiKey=your-real-test-api-key
Adyen__ClientKey=your-real-test-client-key
Adyen__HmacKey=your-real-test-webhook-hmac-key
```

The file should be located at the repository root:

```text
.env
```

The committed placeholder file is:

`.env.example`

The local `.env` is ignored by Git.

## 7. Configure the webhook tunnel

Start the CommerceApi on its normal port, currently:

```text
http://localhost:5200
```

Start an HTTPS tunnel to port `5200`, then configure the resulting URL in Adyen:

```text
https://your-tunnel-host/api/digital-site/notifications/adyen
```

The browser origin remains the dashboard origin:

```text
http://localhost:5100
```

These are separate configuration values:

| Purpose | Value |
|---|---|
| Browser allowed origin | `http://localhost:5100` |
| Local CommerceApi | `http://localhost:5200` |
| Adyen webhook URL | Public HTTPS tunnel URL forwarding to port `5200` |

## 8. Start the application

Run:

```powershell
.\scripts\run-dashboard.ps1
```

The script loads the root `.env` before starting the services.

Confirm that the startup output reports:

```text
Payment gateway: Adyen
```

If it reports `Stub`, check:

- `.env` is at the repository root.
- The variable is exactly `DigitalSite__PaymentGateway=Adyen`.
- The script was started after the `.env` file was created or changed.

## 9. Test the Adyen component

Use an Adyen test card, not a real card. For example, Adyen commonly provides test cards such as:

```text
4111 1111 4555 1142
```

Use the expiry date and CVC specified by Adyen’s current test-card documentation.

Then verify:

1. Select **Direct Debit**.
2. Select **Annual**.
3. Select **Card**.
4. Accept the Roadside Assistance terms.
5. The Adyen Component should appear if session creation succeeds.
6. Enter the Adyen test card.
7. Complete any simulated 3D Secure step if prompted.
8. Confirm the browser returns to the journey.
9. Confirm the webhook reaches `/api/digital-site/notifications/adyen`.
10. Confirm the payment status progresses through the existing confirmation flow.

## Required values summary

You need these five values:

```dotenv
DigitalSite__PaymentGateway=Adyen
Adyen__Environment=test
Adyen__MerchantAccount=...
Adyen__ApiKey=...
Adyen__ClientKey=...
Adyen__HmacKey=...
```

The three sensitive values are:

- `Adyen__ApiKey`
- `Adyen__HmacKey`
- Any APIM subscription key, if used

Never paste those secrets into chat, source files, pull requests, screenshots, or committed configuration.