# Adyen Implementation Roadmap — Remaining Increments

**Status:** Planning for Increments 2C–3 and beyond  
**Branch:** `agents/adyen-increments-continued`  
**Target:** Production-ready Adyen payment integration with comprehensive testing and observability

## Completed Work ✅

### Increment 2A: Basic Adyen Sessions Integration
- ✅ Adyen Drop-in component (`@adyen/adyen-web` 6.46.0)
- ✅ Checkout Sessions flow (Steps 1–5)
- ✅ Payment gateway abstraction (`IPaymentGatewayAdapter`)
- ✅ Stub payment mode (no external calls)
- ✅ Session state management (`sessionStorage`)
- ✅ Error handling and validation

### Increment 2B: Stub Payment Notification Dispatch
- ✅ HMAC signature verification (Adyen-format notifications)
- ✅ Payment transaction state machine
- ✅ `POST /api/digital-site/checkout/stub/{paymentId}/notify`
- ✅ Notification dispatch through message module
- ✅ Tests for stub payment flow
- ✅ Terms acceptance gating for Adyen button

### UI/UX Enhancements ✅
- ✅ Fixed $NaN pricing calculation
- ✅ Icon buttons in message editor dialog
- ✅ Material Design integration

---

## Remaining Work — Increments 2C, 3 & Beyond

### **Increment 2C: Real Adyen Integration (Production Path)**

#### 2C.1 Adyen Client Configuration
- [ ] Validate `Adyen__MerchantAccount`, `Adyen__ApiKey`, `Adyen__ClientKey` at startup
- [ ] Support `Adyen__CheckoutApiUrl` for APIM proxy routing
- [ ] Support `Adyen__ApimSubscriptionKey` for API Management authentication
- [ ] Add health check endpoint: `GET /api/digital-site/health/adyen`
- [ ] Document Adyen test account setup

**Tests:**
- [ ] Startup validation tests (valid & invalid credentials)
- [ ] Configuration fallback tests (Stub mode on auth failure)
- [ ] Health check endpoint tests

#### 2C.2 Adyen Checkout Sessions API Integration
- [ ] Replace stub session creation with real `POST /sessions` call to Adyen
- [ ] Map cart/order data to Adyen `SessionRequest` model
- [ ] Implement retry logic with exponential backoff
- [ ] Handle Adyen error responses (HTTP 4xx/5xx)
- [ ] Add request/response logging and structured telemetry
- [ ] Validate Adyen response schema

**Tests:**
- [ ] Happy path session creation
- [ ] Adyen API timeout handling
- [ ] Network error retry logic
- [ ] Invalid merchant account handling
- [ ] Session data validation

#### 2C.3 Real Payment Details Submission
- [ ] Implement `POST /sessions/details` call to Adyen
- [ ] Handle 3DS redirect flows
- [ ] Map browser result to payment details request
- [ ] Implement correlation tracking (paymentId ↔ Adyen reference)
- [ ] Add structured logging for payment state transitions

**Tests:**
- [ ] Details submission happy path
- [ ] Redirect payment methods (3D Secure, Apple Pay, etc.)
- [ ] Additional details required (3DS step-up)
- [ ] Timeout and network error handling

#### 2C.4 Webhook Verification & Idempotency
- [ ] Persist received webhook signatures to prevent double-processing
- [ ] Add idempotency key tracking
- [ ] Implement webhook deduplication (same `eventCode` + `originalReference` → once)
- [ ] Add structured logging for webhook events
- [ ] Document webhook IP allowlist configuration

**Tests:**
- [ ] Valid HMAC signatures pass
- [ ] Invalid HMAC signatures rejected (HTTP 403)
- [ ] Duplicate webhook events processed once
- [ ] Out-of-order webhook events handled correctly
- [ ] Event timestamp validation

---

### **Increment 3: Full Checkout-to-Fulfillment Flow**

#### 3.1 Order Creation & Fulfillment
- [ ] `CartProcessor` consumes `PaymentTransactionAdded` / `PaymentTransactionStateChanged`
- [ ] Create order from cart on successful Authorization
- [ ] Publish `product.holding.changed` event to Service Bus
- [ ] Implement order version conflict retry
- [ ] Add fulfillment order state tracking

**Tests:**
- [ ] Order creation on successful Authorization
- [ ] `product.holding.changed` published once
- [ ] Concurrent order creation (version conflict) handling
- [ ] Fulfillment stub sets order to `Complete`

#### 3.2 Member History & Cart Reconciliation
- [ ] Display active carts and orders in member history panel
- [ ] Refresh cart list after purchase
- [ ] Handle cart resumption from history
- [ ] Show order status timeline
- [ ] Add cart abandonment detection

**Tests:**
- [ ] Load member carts and orders
- [ ] Resume cart flow
- [ ] Order detail retrieval
- [ ] Empty history state

#### 3.3 Confirmation Page & Notification
- [ ] Poll `GET /checkout/status/{paymentId}` for confirmed order
- [ ] Display order number only after verified webhook
- [ ] Add email confirmation trigger (optional: requires notification service)
- [ ] Timeout and retry handling on confirmation page
- [ ] Add success metrics/events

**Tests:**
- [ ] Poll status transitions
- [ ] Timeout and fallback handling
- [ ] Display order number only after verified payment
- [ ] Cancellation page flow

---

### **Increment 4: Testing & Observability**

#### 4.1 Comprehensive Integration Tests
- [ ] End-to-end checkout flow (Stub mode)
- [ ] Real Adyen Sessions flow (requires test credentials)
- [ ] Webhook verification and idempotency
- [ ] Error scenarios (declined card, timeout, invalid CVV)
- [ ] 3DS/redirect payment method flow
- [ ] Session state persistence across browser refresh

#### 4.2 Smoke Tests & Test Tools
- [ ] Update `docs/scenarios/adyen-checkout-sessions-smoke-test.md`
- [ ] Add Bruno API collection for manual testing
- [ ] Document test card numbers for Adyen
- [ ] Create test data fixtures for carts, orders, payments
- [ ] Implement performance baseline tests

#### 4.3 Observability & Logging
- [ ] Add structured logging for all Adyen API calls
- [ ] Include correlation IDs in logs and Adyen requests
- [ ] Log webhook events with full payload (PII masked)
- [ ] Add Application Insights instrumentation
- [ ] Create Kusto queries for payment flow analysis

---

### **Increment 5: Error Handling & Edge Cases**

#### 5.1 Payment Error Recovery
- [ ] Handle declined cards with user-friendly messaging
- [ ] Implement retry logic for transient Adyen failures
- [ ] Display Adyen error details to user (PCI-compliant)
- [ ] Add payment timeout recovery (auto-refund eligibility)
- [ ] Implement rate limiting on payment attempts

#### 5.2 Session Expiration & Stale Data
- [ ] Implement session timeout (default: 1 hour)
- [ ] Handle cart price changes between session creation and payment
- [ ] Detect currency/amount mismatch
- [ ] Add session refresh logic
- [ ] Implement checkout state cleanup on expiration

#### 5.3 Concurrent Payment Attempts
- [ ] Prevent double-payment if user submits multiple times
- [ ] Lock cart during payment processing
- [ ] Implement optimistic locking with version checks
- [ ] Add duplicate detection via Adyen `merchantReference`

---

### **Increment 6: Documentation & Runbook**

#### 6.1 Adyen Configuration Guide
- [ ] Document Adyen test account creation
- [ ] Step-by-step API credential setup
- [ ] Client key configuration
- [ ] Webhook endpoint configuration
- [ ] IP allowlist setup

#### 6.2 Operations & Troubleshooting
- [ ] Common error codes and solutions
- [ ] Debug logging activation
- [ ] Payment webhook inspection tools
- [ ] Session state inspection queries
- [ ] Recovery procedures for failed payments

#### 6.3 Security & PCI Compliance
- [ ] Verify card data never touches RAC code
- [ ] Confirm HMAC verification is always enforced
- [ ] Document audit logging for compliance
- [ ] PII masking in logs and error messages

---

## Prioritization & Timeline

### Phase 1 (This Sprint): Foundation
**Increment 2C.1–2C.2** — Real Adyen integration basics
- **Effort:** 2–3 days
- **Deliverable:** Functional real Adyen checkout with test credentials
- **Success Criteria:**
  - Sessions created successfully via Adyen API
  - Payment submission to Adyen works
  - Stub mode fallback operational

### Phase 2 (Next Sprint): Robustness
**Increment 2C.3–2C.4, 3.1–3.2** — Webhook, order creation, member history
- **Effort:** 3–5 days
- **Deliverable:** Full purchase flow with order creation
- **Success Criteria:**
  - Orders created and fulfilled
  - Webhook verification working
  - `product.holding.changed` published correctly

### Phase 3 (Following Sprint): Testing & Observability
**Increment 4** — Comprehensive testing, logging, smoke tests
- **Effort:** 2–3 days
- **Deliverable:** Production-ready test coverage and observability
- **Success Criteria:**
  - End-to-end tests pass
  - Smoke test documentation complete
  - Structured logging implemented

### Phase 4 (Polish): Error Handling & Documentation
**Increment 5–6** — Edge cases, error recovery, runbooks
- **Effort:** 2–3 days
- **Deliverable:** Production hardening and runbooks
- **Success Criteria:**
  - All error scenarios tested
  - Troubleshooting guide complete
  - Operations team ready

---

## Branch Strategy

- **Main branch:** `agents/adyen-increments-continued`
- **Feature branches:** `agents/adyen-2c-real-integration`, `agents/adyen-3-fulfillment`, etc.
- **PRs:** Target `master` for each logical increment

---

## Dependencies & Blockers

- **Adyen Test Credentials Required:**
  - Merchant Account ID
  - API Key
  - Client Key
  - Action: Provide test credentials in `.env` (git-ignored)

- **Service Bus Configuration:**
  - Already operational ✅
  - No changes needed

- **commercetools Stub:**
  - Already operational ✅
  - No changes needed

---

## Success Metrics

- ✅ Real Adyen checkout flow completes successfully
- ✅ Webhook notifications verified and idempotent
- ✅ Orders created and linked to payments
- ✅ `product.holding.changed` events published
- ✅ Confirmation page displays order number
- ✅ All error scenarios handled gracefully
- ✅ Production-ready test coverage (≥80%)
- ✅ Structured logging for all critical paths
- ✅ Operations runbook complete

---

## Related Documentation

- [ADR-014: Digital Site Commerce Flow](014-digital-site-commerce-flow.md)
- [Adyen Configuration](../adyen-configuration.md)
- [Digital Site Configuration](../digital-site-configuration.md)
- [Adyen Checkout Sessions Smoke Test](../scenarios/adyen-checkout-sessions-smoke-test.md)
