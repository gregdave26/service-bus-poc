import assert from "node:assert/strict";
import test from "node:test";
import {
  canContinue,
  cartMatchesSelection,
  createInitialFlowState,
  describeCheckoutStatus,
  flowReducer,
  formatCurrency,
  generateCrmId,
  restoreFlowState,
  stepperIndexFor,
  toCheckoutRequest,
  toCreateCartRequest,
  toUpdateCartRequest,
} from "../src/digitalSiteFlowState.js";

const CRM_ID = "CRM-00001234";
const foundVehicle = { rego: "1ANURAG", make: "Toyota" };
const savedCart = { id: "cart-1", version: 2, coverId: "RSA-CLASSIC", coverName: "Classic", totalPrice: 310, vehicleSearchChoice: "RegoLookup", vehicle: foundVehicle };
const checkout = { paymentId: "payment-1", merchantReference: "ref-1", gateway: "Stub", amount: 310 };

function run(actions, state = createInitialFlowState(CRM_ID)) {
  return actions.reduce(flowReducer, state);
}

const onCoverPage = run([
  { type: "answerQuickCheck", value: "No" }, { type: "next" },
  { type: "chooseVehicleSearch", value: "RegoLookup" }, { type: "changeRego", value: "1anurag " }, { type: "vehicleFound", vehicle: foundVehicle }, { type: "next" },
  { type: "chooseCover", value: "RSA-CLASSIC" },
]);

const readyToPay = run([
  { type: "cartSaved", cart: savedCart },
  { type: "choosePaymentPlan", value: "Annual" },
  { type: "acceptTerms", field: "acceptedPaymentAuthTerms", value: true },
  { type: "acceptTerms", field: "acceptedRoadsideAssistTerms", value: true },
], onCoverPage);

test("generates mock CRM ids in the CommerceApi format", () => {
  assert.equal(generateCrmId(() => 0.000_012_34), CRM_ID);
  assert.equal(generateCrmId(() => 0.999_999_999), "CRM-99999999");
  assert.match(generateCrmId(), /^CRM-\d{8}$/);
  assert.match(createInitialFlowState().crmId, /^CRM-\d{8}$/);
});

test("walks the happy path from quick check to checkout", () => {
  assert.equal(onCoverPage.page, "confirmCover");
  assert.equal(canContinue(onCoverPage), true);
  assert.equal(flowReducer(onCoverPage, { type: "next" }).page, "confirmCover", "the cart must be saved before moving on");
  assert.deepEqual(toCreateCartRequest(onCoverPage), {
    crmId: CRM_ID,
    isBrokenDown: "No",
    vehicle: { searchChoice: "RegoLookup", rego: "1anurag" },
    coverId: "RSA-CLASSIC",
  });

  assert.equal(readyToPay.page, "paymentPlan");
  assert.equal(canContinue(readyToPay), true);
  assert.equal(flowReducer(readyToPay, { type: "next" }).page, "paymentPlan", "checkout must start before moving on");
  assert.deepEqual(toCheckoutRequest(readyToPay, "http://localhost:5100/?tab=digitalSite"), {
    cartId: "cart-1",
    crmId: CRM_ID,
    returnUrl: "http://localhost:5100/?tab=digitalSite",
    acceptedPaymentAuthTerms: true,
    acceptedRoadsideAssistTerms: true,
  });

  const paying = flowReducer(readyToPay, { type: "checkoutStarted", checkout });
  assert.equal(paying.page, "pay");
  assert.equal(canContinue(paying), false);
  const submitted = flowReducer(paying, { type: "paymentSubmitted", resultCode: "Authorised" });
  assert.equal(submitted.page, "confirmation");
  assert.equal(submitted.paymentResult, "Authorised");
});

test("blocks customers who are broken down", () => {
  const state = run([{ type: "answerQuickCheck", value: "Yes" }, { type: "next" }]);
  assert.equal(state.page, "quickCheck");
  assert.equal(canContinue(state), false);
});

test("requires a found vehicle unless the lookup is skipped", () => {
  const onVehiclePage = run([{ type: "answerQuickCheck", value: "No" }, { type: "next" }]);
  assert.equal(canContinue(onVehiclePage), false);
  const typedRego = run([{ type: "chooseVehicleSearch", value: "RegoLookup" }, { type: "changeRego", value: "1ABC" }], onVehiclePage);
  assert.equal(canContinue(typedRego), false);
  const changedAfterFound = run([{ type: "vehicleFound", vehicle: foundVehicle }, { type: "changeRego", value: "1ABC1" }], typedRego);
  assert.equal(changedAfterFound.vehicle.details, null);
  const skipped = run([{ type: "chooseVehicleSearch", value: "Skip" }, { type: "chooseCover", value: "RSA-STANDARD" }], typedRego);
  assert.equal(canContinue(skipped), true);
  assert.deepEqual(toCreateCartRequest(skipped).vehicle, { searchChoice: "Skip", rego: null });
});

test("requires a cover and a saved cart before payment", () => {
  const state = { ...createInitialFlowState(CRM_ID), page: "confirmCover" };
  assert.equal(canContinue(state), false);
  const onPlan = { ...readyToPay, cart: null };
  assert.equal(canContinue(onPlan), false);
});

test("detects when the saved cart no longer matches the selection", () => {
  assert.equal(cartMatchesSelection(onCoverPage), false, "no cart yet");
  assert.equal(cartMatchesSelection(readyToPay), true, "rego is compared normalised");
  assert.equal(cartMatchesSelection({ ...readyToPay, coverId: "RSA-ULTIMATE" }), false);
  assert.equal(cartMatchesSelection({ ...readyToPay, vehicle: { searchChoice: "Skip", rego: "", details: null } }), false);
  const skippedCart = { ...savedCart, vehicleSearchChoice: "Skip", vehicle: null };
  assert.equal(cartMatchesSelection({ ...readyToPay, cart: skippedCart, vehicle: { searchChoice: "Skip", rego: "", details: null } }), true);
  assert.deepEqual(toUpdateCartRequest({ ...readyToPay, coverId: "RSA-ULTIMATE" }), {
    crmId: CRM_ID,
    version: 2,
    vehicle: { searchChoice: "RegoLookup", rego: "1anurag" },
    coverId: "RSA-ULTIMATE",
  });
});

test("changing payment plan clears previously accepted terms", () => {
  const state = flowReducer(readyToPay, { type: "choosePaymentPlan", value: "Annual" });
  assert.equal(state.acceptedPaymentAuthTerms, false);
  assert.equal(state.acceptedRoadsideAssistTerms, false);
  assert.equal(canContinue(state), false);
});

test("resumes an active cart at the cover step with its selections", () => {
  const resumed = flowReducer(createInitialFlowState(CRM_ID), { type: "resumeCart", cart: savedCart });
  assert.equal(resumed.page, "confirmCover");
  assert.equal(resumed.isBrokenDown, "No");
  assert.equal(resumed.coverId, "RSA-CLASSIC");
  assert.deepEqual(resumed.vehicle, { searchChoice: "RegoLookup", rego: "1ANURAG", details: foundVehicle });
  assert.equal(cartMatchesSelection(resumed), true);

  const skippedCart = { ...savedCart, vehicleSearchChoice: "Skip", vehicle: null };
  assert.deepEqual(flowReducer(createInitialFlowState(CRM_ID), { type: "resumeCart", cart: skippedCart }).vehicle, { searchChoice: "Skip", rego: "", details: null });
});

test("discarding only clears the matching cart", () => {
  assert.equal(flowReducer(readyToPay, { type: "cartDiscarded", cartId: "other" }), readyToPay);
  assert.equal(flowReducer(readyToPay, { type: "cartDiscarded", cartId: "cart-1" }).cart, null);
});

test("a new member starts a fresh flow with a new CRM id", () => {
  const state = flowReducer(readyToPay, { type: "newMember", crmId: "CRM-99999999" });
  assert.deepEqual(state, createInitialFlowState("CRM-99999999"));
});

test("navigates back and only jumps to earlier stepper steps, dropping any checkout", () => {
  const paying = flowReducer(readyToPay, { type: "checkoutStarted", checkout });
  const backFromPay = flowReducer(paying, { type: "back" });
  assert.equal(backFromPay.page, "paymentPlan");
  assert.equal(backFromPay.checkout, null);
  assert.equal(flowReducer(createInitialFlowState(CRM_ID), { type: "back" }).page, "quickCheck");
  const jumped = flowReducer(paying, { type: "goToStep", index: 0 });
  assert.equal(jumped.page, "vehicleDetails");
  assert.equal(jumped.checkout, null);
  assert.equal(flowReducer(onCoverPage, { type: "goToStep", index: 2 }).page, "confirmCover");
  assert.equal(flowReducer(onCoverPage, { type: "goToStep", index: 9 }).page, "confirmCover");
});

test("payment results are ignored without a checkout", () => {
  assert.equal(flowReducer(readyToPay, { type: "paymentSubmitted", resultCode: "Authorised" }), readyToPay);
});

test("confirmation is terminal until restarted or retried", () => {
  const confirmed = run([{ type: "checkoutStarted", checkout }, { type: "paymentSubmitted" }], readyToPay);
  assert.equal(confirmed.page, "confirmation");
  assert.equal(confirmed.paymentResult, null);
  assert.equal(canContinue(confirmed), false);
  assert.equal(flowReducer(confirmed, { type: "back" }), confirmed);
  assert.equal(flowReducer(confirmed, { type: "goToStep", index: 0 }), confirmed);
  assert.equal(flowReducer(confirmed, { type: "next" }), confirmed);
  assert.equal(flowReducer(confirmed, { type: "unknown" }), confirmed);
  assert.deepEqual(flowReducer(confirmed, { type: "restart" }), createInitialFlowState(CRM_ID));

  const retry = flowReducer(confirmed, { type: "retryPayment" });
  assert.equal(retry.page, "paymentPlan");
  assert.equal(retry.checkout, null);
  assert.equal(retry.cart, savedCart);
  assert.equal(flowReducer(readyToPay, { type: "retryPayment" }), readyToPay);
});

test("restores a persisted flow and rejects anything unrecognised", () => {
  const paying = flowReducer(readyToPay, { type: "checkoutStarted", checkout });
  assert.deepEqual(restoreFlowState(JSON.stringify(paying)), paying);
  assert.deepEqual(restoreFlowState(JSON.stringify({ page: "quickCheck", crmId: CRM_ID })), createInitialFlowState(CRM_ID));
  assert.equal(restoreFlowState(null), null);
  assert.equal(restoreFlowState("not json"), null);
  assert.equal(restoreFlowState(JSON.stringify({ page: "unknown", crmId: CRM_ID })), null);
  assert.equal(restoreFlowState(JSON.stringify({ page: "quickCheck", crmId: "bad" })), null);
});

test("maps pages to sidebar stepper positions", () => {
  assert.equal(stepperIndexFor("quickCheck"), -1);
  assert.equal(stepperIndexFor("vehicleDetails"), 0);
  assert.equal(stepperIndexFor("pay"), 3);
  assert.equal(stepperIndexFor("confirmation"), 4);
});

test("describes checkout progress from the backend status only", () => {
  assert.equal(describeCheckoutStatus(null).done, false);
  assert.equal(describeCheckoutStatus({ paymentState: "Pending" }).title, "Waiting for payment confirmation");
  assert.deepEqual(describeCheckoutStatus({ paymentState: "Refused" }), { severity: "error", title: "Payment declined", done: true });
  assert.equal(describeCheckoutStatus({ paymentState: "Authorised", provisioningStatus: "NotStarted" }).title, "Payment authorised, creating your order");
  assert.equal(describeCheckoutStatus({ paymentState: "Authorised", provisioningStatus: "NotStarted", processingResult: "OrderCreated" }).done, false);
  assert.equal(describeCheckoutStatus({ paymentState: "Authorised", provisioningStatus: "NotStarted", processingResult: "AmountMismatch" }).severity, "warning");
  assert.equal(describeCheckoutStatus({ paymentState: "Authorised", orderId: "o-1", provisioningStatus: "Pending" }).done, false);
  assert.deepEqual(describeCheckoutStatus({ paymentState: "Authorised", orderId: "o-1", provisioningStatus: "Provisioned" }), { severity: "success", title: "You're covered", done: true });
});

test("formats prices in Australian dollars", () => {
  assert.equal(formatCurrency(310), "$310.00");
});
