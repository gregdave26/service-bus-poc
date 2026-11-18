# Digital Site Payment & Subscription Increments

**Status:** In Progress  
**Branch:** `agents/adyen-increments-continued`  
**Approach:** End-to-end slices, one payment/subscription option combination per increment

---

## Completed Increments ✅

### **Increment 2A: DirectDebit + Annual + Card (Stub Mode)**
- ✅ UI checkout flow (Steps 1–5: cover, details, vehicle, confirm, payment)
- ✅ Stub payment gateway (no external calls; Approve/Decline/Pending buttons)
- ✅ Adyen Drop-in component integration (`@adyen/adyen-web`)
- ✅ Session state management (`sessionStorage`)
- ✅ Payment plan selection (Annual only; Monthly placeholder)
- ✅ Card payment method (for Annual DirectDebit)
- ✅ Terms acceptance gating (roadside assistance + payment authority terms)
- ✅ Mock commercetools stub (cart, payment, order storage)
- ✅ Stub notification dispatch (HMAC-signed Adyen-format messages)

**Status:** Full end-to-end checkout works in stub mode

---

### **Increment 2B: Stub Payment Notification Dispatch**
- ✅ Notification verification (HMAC signature validation)
- ✅ Payment transaction state machine (Pending → Authorised)
- ✅ Order creation trigger (on successful Authorization)
- ✅ Member history integration (active carts & orders displayed)
- ✅ Confirmation page (order number displayed after verified payment)
- ✅ Feature flag: `DigitalSite__EnableStubNotifications` (disabled by default)
- ✅ Unit tests (notification processor idempotency, state transitions)

**Status:** Full payment notification flow works with stub mode

---

## Remaining Increments 🚀

### **Increment 2C: DirectDebit + Monthly + Card (Stub Mode)**

**Current state:** Monthly frequency selector exists in UI but not fully wired to checkout flow

**2C.1: Monthly Payment Plan Selection**
- [ ] Enable Monthly option in payment frequency selector
- [ ] Calculate monthly amount (annual / 12) for display
- [ ] Pass selected frequency to checkout request
- [ ] Store paymentPlan (Annual vs Monthly) in cart/payment

**2C.2: Monthly Payment Amount in Stub Mode**
- [ ] Map monthly amount to Adyen session creation
- [ ] Stub notification returns correct monthly amount
- [ ] Order holding/subscription set for 1 month vs 12 months
- [ ] Member history shows monthly billing indicator

**2C.3: Unit Tests**
- [ ] Monthly payment plan selection
- [ ] Stub notification with monthly amount
- [ ] Monthly subscription state in commercetools

**Deliverable:** Full DirectDebit + Monthly + Card flow in stub mode

---

### **Increment 2D: DirectDebit + Annual + BankAccount (Stub Mode)**

**Current state:** Payment method selector only shows Card; BankAccount hidden

**2D.1: Bank Account Payment Method Option**
- [ ] Add BankAccount to payment method selector (visible for Annual DirectDebit)
- [ ] Store paymentMethod (Card vs BankAccount) in state
- [ ] Show "I've read and agree to payment authority terms" checkbox only for BankAccount
- [ ] Validate acceptedPaymentAuthTerms when BankAccount selected

**2D.2: Bank Account in Stub Mode**
- [ ] Stub gateway creates session for bank account (no card iframe)
- [ ] Display "Bank details will be captured" message (or mock bank UI)
- [ ] Stub notification returns Authorised for bank payment
- [ ] Mock bank account verification (UI shows account **** 1234)

**2D.3: Unit Tests**
- [ ] Payment method selector shows BankAccount for Annual DirectDebit
- [ ] Payment authority terms required only for BankAccount
- [ ] Stub notification with bank account
- [ ] Monthly payment vs bank account validation

**Deliverable:** Full DirectDebit + Annual + BankAccount flow in stub mode

---

### **Increment 2E: DirectDebit + Monthly + BankAccount (Stub Mode)**

**Current state:** Combination of 2C (Monthly) and 2D (BankAccount)

**2E.1: Monthly + BankAccount Combination**
- [ ] Enable BankAccount method for Monthly DirectDebit
- [ ] Calculate and display monthly amount for bank payment
- [ ] Payment authority terms checkbox shown for monthly bank payment
- [ ] Stub notification with monthly bank amount

**2E.2: Unit Tests**
- [ ] Monthly + BankAccount selection
- [ ] Monthly amount displayed for bank payment
- [ ] State transitions for monthly bank payment

**Deliverable:** Full DirectDebit + Monthly + BankAccount flow in stub mode

---

### **Increment 2F: Single Payment + Card (Stub Mode)**

**Current state:** Single Payment option exists in UI but flows directly to payment without subscription handling

**2F.1: Single Payment Flow**
- [ ] Single Payment radio button selects payment type
- [ ] Hide payment frequency and method selectors (not applicable)
- [ ] Show "Full amount due now: $X" message
- [ ] No terms acceptance (only roadside assistance, not payment authority)
- [ ] Payment goes directly to full cart amount

**2F.2: Order State for Single Payment**
- [ ] Order created with holding.term = 1 (no recurring subscription)
- [ ] Member history shows "Paid in full" vs recurring subscription indicator

**2F.3: Stub Mode Single Payment**
- [ ] Stub session creation for full amount
- [ ] Notification returns Authorised
- [ ] Order marked as complete (no renewal required)

**2F.4: Unit Tests**
- [ ] Single Payment selection hides frequency/method
- [ ] Payment amount equals cart total
- [ ] Order holding.term = 1
- [ ] No subscription renewal scheduled

**Deliverable:** Full Single Payment + Card flow in stub mode

---

### **Increment 2G: Real Adyen Integration (All Payment Options)**

**Prerequisites:** 
- Adyen test merchant credentials (MerchantAccount, ApiKey, ClientKey, HmacKey)
- All 2A–2F increments complete and working in stub mode

**2G.1: Adyen Configuration & Startup**
- [ ] Validate `Adyen__MerchantAccount`, `Adyen__ApiKey`, `Adyen__ClientKey` at startup
- [ ] Implement `GET /api/digital-site/health/adyen` endpoint
- [ ] Support `Adyen__CheckoutApiUrl` for APIM proxy routing
- [ ] Fallback to Stub mode if Adyen connectivity fails (when allowed)
- [ ] Unit tests for configuration validation, health check

**2G.2: Real Adyen Checkout Sessions**
- [ ] Replace stub session creation with real `POST /sessions` API call
- [ ] Map cart (annual/monthly, card/bank, amount) to Adyen SessionRequest
- [ ] Implement retry logic (exponential backoff, 3 attempts)
- [ ] Error handling for Adyen HTTP 4xx/5xx responses
- [ ] Structured logging for API requests/responses
- [ ] Integration tests for session creation (all 2A–2F scenarios)

**2G.3: Real Payment Details Submission**
- [ ] Implement `POST /sessions/details` call to Adyen
- [ ] Handle 3DS redirect flows (`/sessions?redirectResult=...`)
- [ ] Map browser component state to payment details request
- [ ] Correlation tracking (paymentId ↔ Adyen pspReference)
- [ ] Integration tests for details submission, 3DS redirects

**2G.4: Real Webhook Verification & Idempotency**
- [ ] Persist received webhook signatures (prevent double-processing)
- [ ] Implement idempotency key tracking
- [ ] Deduplicate webhooks (same eventCode + originalReference = once)
- [ ] Handle out-of-order webhook events
- [ ] Tests for HMAC validation, duplicate prevention, event ordering

**2G.5: End-to-End Testing (All 2A–2F Variants)**
- [ ] Smoke tests for each payment option combination
- [ ] Bruno API collection for manual Adyen testing
- [ ] Test card numbers and bank account scenarios
- [ ] Performance baseline tests

**Deliverable:** Production-ready Adyen integration with comprehensive testing

---

## Implementation Strategy

### Testing Approach
Each increment includes:
1. **Unit tests** for business logic (plan selection, amount calculation, state transitions)
2. **Integration tests** against commercetools stub (cart creation, payment updates, order creation)
3. **Manual smoke tests** in the UI (click through checkout, verify order created)

### Stub-to-Real Transition
- **Stub mode:** All 2A–2F increments work without Adyen credentials
- **Real mode:** Configure `DigitalSite__PaymentGateway=Adyen` and provide credentials
- **Toggle between modes:** Configuration change only (no code changes)

### Feature Flags
- `DigitalSite__EnableStubNotifications`: Enable automatic notification dispatch in stub mode (default: false)
- `DigitalSite__PaymentGateway`: Stub (default) or Adyen
- `FeatureManagement__UseRealAdyen`: Force Adyen or retain Stub (optional startup override)

---

## Success Criteria

### Per Increment
- [ ] All UI controls functional and wired to backend
- [ ] Cart created with correct cover, amount, frequency, method
- [ ] Payment created in commercetools with correct state
- [ ] Stub notification triggers order creation
- [ ] Order visible in member history
- [ ] ≥80% code coverage for new logic

### End-to-End (2A–2F in Stub Mode)
- [ ] All 6 payment option combinations produce working checkouts
- [ ] All checkouts result in order creation
- [ ] All orders appear in member history
- [ ] Confirmation page shows order number

### Production Ready (2G)
- [ ] Real Adyen integration passes all smoke tests
- [ ] Retry logic and error handling tested
- [ ] Webhook verification works for all payment options
- [ ] Fallback to Stub mode works when Adyen unavailable

---

## Timeline Estimate

| Increment | Effort | Blocker |
|-----------|--------|---------|
| 2C | 2–3 hrs | None |
| 2D | 3–4 hrs | None |
| 2E | 1–2 hrs | 2C + 2D complete |
| 2F | 2–3 hrs | None |
| 2G | 8–12 hrs | Adyen test credentials |

**Total for 2A–2F (stub):** ~16–20 hrs  
**Total for 2G (real Adyen):** ~12 hrs + credential setup

---

## Current Progress

✅ **Complete:** 2A, 2B  
⏳ **Next:** Start 2C (DirectDebit + Monthly + Card)  
🔄 **Recommended sequence:** 2C → 2D → 2E → 2F → 2G
