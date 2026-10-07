export const PAGES = ["quickCheck", "vehicleDetails", "confirmCover", "paymentPlan", "confirmation"];

export const STEPPER_STEPS = [
  { name: "Vehicle details", page: "vehicleDetails" },
  { name: "Confirm your cover", page: "confirmCover" },
  { name: "Payment plan", page: "paymentPlan" },
];

export const initialFlowState = Object.freeze({
  page: "quickCheck",
  isBrokenDown: null,
  vehicle: Object.freeze({ searchChoice: null, rego: "", details: null }),
  coverId: null,
  paymentPlan: null,
  acceptedPaymentAuthTerms: false,
  acceptedRoadsideAssistTerms: false,
  order: null,
});

export function stepperIndexFor(page) {
  if (page === "confirmation") return STEPPER_STEPS.length;
  return STEPPER_STEPS.findIndex((step) => step.page === page);
}

export function canContinue(state) {
  switch (state.page) {
    case "quickCheck":
      return state.isBrokenDown === "No";
    case "vehicleDetails":
      return state.vehicle.searchChoice === "Skip"
        || (state.vehicle.searchChoice === "RegoLookup" && state.vehicle.details !== null);
    case "confirmCover":
      return state.coverId !== null;
    case "paymentPlan":
      return state.paymentPlan !== null && state.acceptedPaymentAuthTerms && state.acceptedRoadsideAssistTerms;
    default:
      return false;
  }
}

function movePage(state, offset) {
  const nextPage = PAGES[PAGES.indexOf(state.page) + offset];
  return nextPage ? { ...state, page: nextPage } : state;
}

export function flowReducer(state, action) {
  switch (action.type) {
    case "answerQuickCheck":
      return { ...state, isBrokenDown: action.value };
    case "chooseVehicleSearch":
      return { ...state, vehicle: { ...state.vehicle, searchChoice: action.value } };
    case "changeRego":
      return { ...state, vehicle: { ...state.vehicle, rego: action.value, details: null } };
    case "vehicleFound":
      return { ...state, vehicle: { ...state.vehicle, details: action.vehicle } };
    case "chooseCover":
      return { ...state, coverId: action.value };
    // Live flow behaviour: the authorisation wording depends on the plan, so consent must be given again.
    case "choosePaymentPlan":
      return { ...state, paymentPlan: action.value, acceptedPaymentAuthTerms: false, acceptedRoadsideAssistTerms: false };
    case "acceptTerms":
      return { ...state, [action.field]: action.value };
    case "next":
      return canContinue(state) ? movePage(state, 1) : state;
    case "back":
      return state.page === "confirmation" ? state : movePage(state, -1);
    case "goToStep": {
      const target = STEPPER_STEPS[action.index]?.page;
      const isEarlierStep = target && action.index < stepperIndexFor(state.page);
      return isEarlierStep && state.page !== "confirmation" ? { ...state, page: target } : state;
    }
    case "orderPlaced":
      return { ...state, page: "confirmation", order: action.order };
    case "restart":
      return initialFlowState;
    default:
      return state;
  }
}

export function toOrderRequest(state) {
  return {
    isBrokenDown: state.isBrokenDown,
    vehicle: state.vehicle.searchChoice === "RegoLookup"
      ? { searchChoice: "RegoLookup", rego: state.vehicle.rego }
      : { searchChoice: "Skip" },
    coverId: state.coverId,
    paymentPlan: state.paymentPlan,
    acceptedPaymentAuthTerms: state.acceptedPaymentAuthTerms,
    acceptedRoadsideAssistTerms: state.acceptedRoadsideAssistTerms,
  };
}

const currencyFormatter = new Intl.NumberFormat("en-AU", { style: "currency", currency: "AUD" });

export function formatCurrency(amount) {
  return currencyFormatter.format(amount);
}

export function paymentPlanPrice(cover, planId) {
  return planId === "Monthly" ? cover.monthlyPrice : cover.annualPrice;
}

export function paymentPlanDescription(cover, planId) {
  return planId === "Monthly"
    ? `Total ${formatCurrency(cover.monthlyTotal)} 12-month term via direct debit.`
    : `Save ${formatCurrency(cover.monthlyTotal - cover.annualPrice)} compared to monthly.`;
}
