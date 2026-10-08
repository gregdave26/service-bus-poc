export const PAGES = ["quickCheck", "vehicleDetails", "confirmCover", "paymentPlan", "pay", "confirmation"];

export const STEPPER_STEPS = [
  { name: "Vehicle details", page: "vehicleDetails" },
  { name: "Confirm your cover", page: "confirmCover" },
  { name: "Payment plan", page: "paymentPlan" },
  { name: "Payment", page: "pay" },
];

export const FLOW_STORAGE_KEY = "digitalSite.flow";

export function generateCrmId(random = Math.random) {
  return `CRM-${String(Math.floor(random() * 100_000_000)).padStart(8, "0")}`;
}

export function createInitialFlowState(crmId = generateCrmId()) {
  return {
    page: "quickCheck",
    crmId,
    isBrokenDown: null,
    vehicle: { searchChoice: null, rego: "", details: null },
    coverId: null,
    paymentPlan: null,
    acceptedPaymentAuthTerms: false,
    acceptedRoadsideAssistTerms: false,
    cart: null,
    checkout: null,
    paymentResult: null,
  };
}

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
      return state.cart !== null && state.paymentPlan !== null && state.acceptedPaymentAuthTerms && state.acceptedRoadsideAssistTerms;
    default:
      return false;
  }
}

function toVehicleSelection(vehicle) {
  return vehicle.searchChoice === "RegoLookup"
    ? { searchChoice: "RegoLookup", rego: vehicle.rego.trim() }
    : { searchChoice: "Skip", rego: null };
}

export function cartMatchesSelection(state) {
  if (!state.cart) return false;
  const selected = toVehicleSelection(state.vehicle);
  return state.cart.coverId === state.coverId
    && state.cart.vehicleSearchChoice === selected.searchChoice
    && (state.cart.vehicle?.rego ?? null) === (selected.rego?.replace(/\s+/g, "").toUpperCase() ?? null);
}

export function toCreateCartRequest(state) {
  return { crmId: state.crmId, isBrokenDown: state.isBrokenDown, vehicle: toVehicleSelection(state.vehicle), coverId: state.coverId };
}

export function toUpdateCartRequest(state) {
  return { crmId: state.crmId, version: state.cart.version, vehicle: toVehicleSelection(state.vehicle), coverId: state.coverId };
}

export function toCheckoutRequest(state, returnUrl) {
  return {
    cartId: state.cart.id,
    crmId: state.crmId,
    returnUrl,
    acceptedPaymentAuthTerms: state.acceptedPaymentAuthTerms,
    acceptedRoadsideAssistTerms: state.acceptedRoadsideAssistTerms,
  };
}

function movePage(state, offset) {
  const nextPage = PAGES[PAGES.indexOf(state.page) + offset];
  return nextPage ? { ...state, page: nextPage } : state;
}

function resumeFrom(state, cart) {
  const isLookup = cart.vehicleSearchChoice === "RegoLookup" && cart.vehicle;
  return {
    ...state,
    page: "confirmCover",
    isBrokenDown: "No",
    vehicle: isLookup
      ? { searchChoice: "RegoLookup", rego: cart.vehicle.rego, details: cart.vehicle }
      : { searchChoice: "Skip", rego: "", details: null },
    coverId: cart.coverId,
    cart,
  };
}

export function flowReducer(state, action) {
  switch (action.type) {
    case "answerQuickCheck":
      return { ...state, isBrokenDown: action.value };
    case "newMember":
      return createInitialFlowState(action.crmId);
    case "resumeCart":
      return resumeFrom(state, action.cart);
    case "cartDiscarded":
      return state.cart?.id === action.cartId ? { ...state, cart: null } : state;
    case "chooseVehicleSearch":
      return { ...state, vehicle: { ...state.vehicle, searchChoice: action.value } };
    case "changeRego":
      return { ...state, vehicle: { ...state.vehicle, rego: action.value, details: null } };
    case "vehicleFound":
      return { ...state, vehicle: { ...state.vehicle, details: action.vehicle } };
    case "chooseCover":
      return { ...state, coverId: action.value };
    case "cartSaved":
      return { ...state, cart: action.cart, page: "paymentPlan" };
    // Live flow behaviour: the authorisation wording depends on the plan, so consent must be given again.
    case "choosePaymentPlan":
      return { ...state, paymentPlan: action.value, acceptedPaymentAuthTerms: false, acceptedRoadsideAssistTerms: false };
    case "acceptTerms":
      return { ...state, [action.field]: action.value };
    case "checkoutStarted":
      return { ...state, page: "pay", checkout: action.checkout, paymentResult: null };
    // The browser result is only an interim status; the order is confirmed from the backend status.
    case "paymentSubmitted":
      return state.checkout ? { ...state, page: "confirmation", paymentResult: action.resultCode ?? null } : state;
    case "retryPayment":
      return state.page === "confirmation" ? { ...state, page: "paymentPlan", checkout: null, paymentResult: null } : state;
    case "next":
      return canContinue(state) && state.page !== "confirmCover" && state.page !== "paymentPlan" ? movePage(state, 1) : state;
    case "back":
      return state.page === "confirmation" || state.page === "quickCheck" ? state : { ...movePage(state, -1), checkout: null };
    case "goToStep": {
      const target = STEPPER_STEPS[action.index]?.page;
      const isEarlierStep = target && action.index < stepperIndexFor(state.page);
      return isEarlierStep && state.page !== "confirmation" ? { ...state, page: target, checkout: null } : state;
    }
    case "restart":
      return createInitialFlowState(state.crmId);
    default:
      return state;
  }
}

export function restoreFlowState(serialized) {
  try {
    const restored = JSON.parse(serialized ?? "null");
    return restored && PAGES.includes(restored.page) && /^CRM-\d{8}$/.test(restored.crmId)
      ? { ...createInitialFlowState(restored.crmId), ...restored }
      : null;
  } catch {
    return null;
  }
}

const currencyFormatter = new Intl.NumberFormat("en-AU", { style: "currency", currency: "AUD" });

export function formatCurrency(amount) {
  return currencyFormatter.format(amount);
}

const PAYMENT_PROGRESS = {
  Refused: { severity: "error", title: "Payment declined", done: true },
  Provisioned: { severity: "success", title: "You're covered", done: true },
  OrderPending: { severity: "info", title: "Payment authorised, finalising your cover", done: false },
  Authorised: { severity: "info", title: "Payment authorised, creating your order", done: false },
  NotCreated: { severity: "warning", title: "Payment authorised but no order was created", done: true },
  Pending: { severity: "info", title: "Waiting for payment confirmation", done: false },
};

export function describeCheckoutStatus(status) {
  if (!status) return PAYMENT_PROGRESS.Pending;
  if (status.paymentState === "Refused") return PAYMENT_PROGRESS.Refused;
  if (status.paymentState !== "Authorised") return PAYMENT_PROGRESS.Pending;
  if (status.provisioningStatus === "Provisioned") return PAYMENT_PROGRESS.Provisioned;
  if (status.orderId) return PAYMENT_PROGRESS.OrderPending;
  return status.processingResult && status.processingResult !== "OrderCreated" ? PAYMENT_PROGRESS.NotCreated : PAYMENT_PROGRESS.Authorised;
}
