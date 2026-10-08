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
const personalDetails = {
  title: "Mr",
  firstName: "Alex",
  lastName: "Rider",
  dateOfBirth: "1980-01-01",
  email: "alex@example.com",
  phone: "0400000000",
  addressLine1: "1 Example Street",
  suburb: "Perth",
  state: "WA",
  postcode: "6000",
};
const foundVehicle = { rego: "1ANURAG", make: "Toyota" };
const savedCart = {
  id: "cart-1",
  version: 2,
  coverId: "RSA-CLASSIC",
  coverName: "Classic",
  totalPrice: 310,
  vehicleSearchChoice: "RegoLookup",
  vehicle: foundVehicle,
  personalDetails,
};
const checkout = { paymentId: "payment-1", merchantReference: "ref-1", gateway: "Stub", amount: 310 };

function run(actions, state = createInitialFlowState(CRM_ID)) {
  return actions.reduce(flowReducer, state);
}

function detailsAndVehicleState() {
  return run([
    ...Object.entries(personalDetails).map(([field, value]) => ({ type: "changePersonalDetails", field, value })),
    { type: "chooseVehicleSearch", value: "RegoLookup" },
    { type: "changeRego", value: "1anurag " },
    { type: "vehicleFound", vehicle: foundVehicle },
    { type: "chooseCover", value: "RSA-CLASSIC" },
  ]);
}

test("generates mock CRM ids in the CommerceApi format", () => {
  assert.equal(generateCrmId(() => 0.000_012_34), CRM_ID);
  assert.equal(generateCrmId(() => 0.999_999_999), "CRM-99999999");
  assert.match(createInitialFlowState().crmId, /^CRM-\d{8}$/);
});

test("starts at Your details with the RAC-style three-stage stepper", () => {
  const state = createInitialFlowState(CRM_ID);
  assert.equal(state.page, "yourDetails");
  assert.deepEqual(stepperIndexFor("yourDetails"), 0);
  assert.deepEqual(stepperIndexFor("confirmPay"), 1);
  assert.deepEqual(stepperIndexFor("finished"), 2);
  assert.equal(stepperIndexFor("quickCheck"), -1);
});

test("requires personal details, vehicle choice, and cover before continuing", () => {
  const state = detailsAndVehicleState();
  assert.equal(canContinue(state), true);
  assert.equal(canContinue({ ...state, personalDetails: { ...personalDetails, email: "" } }), false);
  assert.equal(canContinue({ ...state, coverId: null }), false);
  assert.equal(canContinue({ ...state, vehicle: { searchChoice: "RegoLookup", rego: "1ANURAG", details: null } }), false);
  assert.equal(canContinue({ ...state, vehicle: { searchChoice: "Skip", rego: "", details: null } }), true);
});

test("creates a cart request without the removed breakdown gate", () => {
  assert.deepEqual(toCreateCartRequest(detailsAndVehicleState()), {
    crmId: CRM_ID,
    isBrokenDown: "No",
    personalDetails,
    vehicle: { searchChoice: "RegoLookup", rego: "1anurag" },
    coverId: "RSA-CLASSIC",
  });
});

test("moves from Your details to Confirm & pay", () => {
  const state = detailsAndVehicleState();
  assert.equal(flowReducer(state, { type: "next" }).page, "confirmPay");
  assert.equal(flowReducer(createInitialFlowState(CRM_ID), { type: "next" }).page, "yourDetails");
  assert.equal(flowReducer(state, { type: "cartSaved", cart: savedCart }).page, "confirmPay");
});

test("requires payment plan and terms on Confirm & pay", () => {
  const state = flowReducer(detailsAndVehicleState(), { type: "cartSaved", cart: savedCart });
  assert.equal(canContinue(state), false);
  const ready = run([
    { type: "cartSaved", cart: savedCart },
    { type: "choosePaymentPlan", value: "Annual" },
    { type: "acceptTerms", field: "acceptedPaymentAuthTerms", value: true },
    { type: "acceptTerms", field: "acceptedRoadsideAssistTerms", value: true },
  ], detailsAndVehicleState());
  assert.equal(canContinue(ready), true);
  assert.deepEqual(toCheckoutRequest(ready, "http://localhost:5100/?tab=digitalSite"), {
    cartId: "cart-1",
    crmId: CRM_ID,
    returnUrl: "http://localhost:5100/?tab=digitalSite",
    acceptedPaymentAuthTerms: true,
    acceptedRoadsideAssistTerms: true,
  });
});

test("payment submission moves to Finished and retry returns to Confirm & pay", () => {
  const ready = run([
    { type: "cartSaved", cart: savedCart },
    { type: "choosePaymentPlan", value: "Annual" },
    { type: "acceptTerms", field: "acceptedPaymentAuthTerms", value: true },
    { type: "acceptTerms", field: "acceptedRoadsideAssistTerms", value: true },
    { type: "checkoutStarted", checkout },
    { type: "paymentSubmitted", resultCode: "Authorised" },
  ], detailsAndVehicleState());
  assert.equal(ready.page, "finished");
  assert.equal(flowReducer(ready, { type: "retryPayment" }).page, "confirmPay");
});

test("back navigation returns to Your details and drops checkout", () => {
  const state = { ...detailsAndVehicleState(), page: "confirmPay", checkout };
  const previous = flowReducer(state, { type: "back" });
  assert.equal(previous.page, "yourDetails");
  assert.equal(previous.checkout, null);
  assert.equal(flowReducer({ ...state, page: "finished" }, { type: "back" }).page, "finished");
});

test("personal details and vehicle selections are restored from an active cart", () => {
  const resumed = flowReducer(createInitialFlowState(CRM_ID), { type: "resumeCart", cart: savedCart });
  assert.equal(resumed.page, "yourDetails");
  assert.deepEqual(resumed.personalDetails, personalDetails);
  assert.equal(resumed.coverId, "RSA-CLASSIC");
  assert.equal(cartMatchesSelection(resumed), true);
  assert.deepEqual(toUpdateCartRequest(resumed), {
    crmId: CRM_ID,
    version: 2,
    personalDetails,
    vehicle: { searchChoice: "RegoLookup", rego: "1ANURAG" },
    coverId: "RSA-CLASSIC",
  });
});

test("changing payment plan clears previously accepted terms", () => {
  const state = {
    ...detailsAndVehicleState(),
    page: "confirmPay",
    cart: savedCart,
    acceptedPaymentAuthTerms: true,
    acceptedRoadsideAssistTerms: true,
  };
  const changed = flowReducer(state, { type: "choosePaymentPlan", value: "Annual" });
  assert.equal(changed.acceptedPaymentAuthTerms, false);
  assert.equal(changed.acceptedRoadsideAssistTerms, false);
});

test("restores only recognised flow states", () => {
  const state = createInitialFlowState(CRM_ID);
  assert.equal(restoreFlowState(JSON.stringify(state)).page, "yourDetails");
  assert.equal(restoreFlowState(JSON.stringify({ ...state, page: "quickCheck" })), null);
  assert.equal(restoreFlowState("not json"), null);
});

test("formats prices and describes checkout status", () => {
  assert.equal(formatCurrency(310), "$310.00");
  assert.equal(describeCheckoutStatus(null).title, "Waiting for payment confirmation");
  assert.equal(describeCheckoutStatus({ paymentState: "Refused" }).done, true);
  assert.equal(describeCheckoutStatus({ paymentState: "Authorised", provisioningStatus: "Provisioned" }).title, "You're covered");
});
