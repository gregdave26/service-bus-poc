import assert from "node:assert/strict";
import test from "node:test";
import {
  canContinue,
  flowReducer,
  formatCurrency,
  initialFlowState,
  paymentPlanDescription,
  paymentPlanPrice,
  stepperIndexFor,
  toOrderRequest,
} from "../src/digitalSiteFlowState.js";

const classic = { id: "CLAS", annualPrice: 310, monthlyPrice: 28.55, monthlyTotal: 345.6 };
const foundVehicle = { rego: "1ANURAG", make: "Toyota" };

function run(actions, state = initialFlowState) {
  return actions.reduce(flowReducer, state);
}

const completedState = run([
  { type: "answerQuickCheck", value: "No" }, { type: "next" },
  { type: "chooseVehicleSearch", value: "RegoLookup" }, { type: "changeRego", value: "1anurag" }, { type: "vehicleFound", vehicle: foundVehicle }, { type: "next" },
  { type: "chooseCover", value: "CLAS" }, { type: "next" },
  { type: "choosePaymentPlan", value: "Monthly" },
  { type: "acceptTerms", field: "acceptedPaymentAuthTerms", value: true },
  { type: "acceptTerms", field: "acceptedRoadsideAssistTerms", value: true },
]);

test("walks the happy path to the payment plan page", () => {
  assert.equal(completedState.page, "paymentPlan");
  assert.equal(canContinue(completedState), true);
  assert.deepEqual(toOrderRequest(completedState), {
    isBrokenDown: "No",
    vehicle: { searchChoice: "RegoLookup", rego: "1anurag" },
    coverId: "CLAS",
    paymentPlan: "Monthly",
    acceptedPaymentAuthTerms: true,
    acceptedRoadsideAssistTerms: true,
  });
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
  const skipped = run([{ type: "chooseVehicleSearch", value: "Skip" }], typedRego);
  assert.equal(canContinue(skipped), true);
  assert.deepEqual(toOrderRequest(skipped).vehicle, { searchChoice: "Skip" });
});

test("requires a cover before moving to payment", () => {
  const state = { ...initialFlowState, page: "confirmCover" };
  assert.equal(flowReducer(state, { type: "next" }).page, "confirmCover");
  assert.equal(run([{ type: "chooseCover", value: "STD" }, { type: "next" }], state).page, "paymentPlan");
});

test("changing payment plan clears previously accepted terms", () => {
  const state = flowReducer(completedState, { type: "choosePaymentPlan", value: "Annual" });
  assert.equal(state.acceptedPaymentAuthTerms, false);
  assert.equal(state.acceptedRoadsideAssistTerms, false);
  assert.equal(canContinue(state), false);
});

test("navigates back and only jumps to earlier stepper steps", () => {
  assert.equal(flowReducer(completedState, { type: "back" }).page, "confirmCover");
  assert.equal(flowReducer(initialFlowState, { type: "back" }).page, "quickCheck");
  assert.equal(flowReducer(completedState, { type: "goToStep", index: 0 }).page, "vehicleDetails");
  const onCover = { ...completedState, page: "confirmCover" };
  assert.equal(flowReducer(onCover, { type: "goToStep", index: 2 }).page, "confirmCover");
  assert.equal(flowReducer(onCover, { type: "goToStep", index: 9 }).page, "confirmCover");
});

test("confirmation is terminal until restarted", () => {
  const order = { orderId: "RSA-000001" };
  const confirmed = flowReducer(completedState, { type: "orderPlaced", order });
  assert.equal(confirmed.page, "confirmation");
  assert.equal(confirmed.order, order);
  assert.equal(canContinue(confirmed), false);
  assert.equal(flowReducer(confirmed, { type: "back" }).page, "confirmation");
  assert.equal(flowReducer(confirmed, { type: "goToStep", index: 0 }).page, "confirmation");
  assert.equal(flowReducer(confirmed, { type: "next" }), confirmed);
  assert.equal(flowReducer(confirmed, { type: "restart" }), initialFlowState);
  assert.equal(flowReducer(confirmed, { type: "unknown" }), confirmed);
});

test("maps pages to sidebar stepper positions", () => {
  assert.equal(stepperIndexFor("quickCheck"), -1);
  assert.equal(stepperIndexFor("vehicleDetails"), 0);
  assert.equal(stepperIndexFor("paymentPlan"), 2);
  assert.equal(stepperIndexFor("confirmation"), 3);
});

test("formats payment plan prices and descriptions", () => {
  assert.equal(formatCurrency(28.55), "$28.55");
  assert.equal(paymentPlanPrice(classic, "Monthly"), 28.55);
  assert.equal(paymentPlanPrice(classic, "Annual"), 310);
  assert.equal(paymentPlanDescription(classic, "Monthly"), "Total $345.60 12-month term via direct debit.");
  assert.equal(paymentPlanDescription(classic, "Annual"), "Save $35.60 compared to monthly.");
});
