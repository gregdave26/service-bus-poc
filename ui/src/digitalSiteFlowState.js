export const PAGES = ["yourDetails", "confirmPay", "finished"];

export const STEPPER_STEPS = [
  { name: "Your details", page: "yourDetails" },
  { name: "Confirm & pay", page: "confirmPay" },
  { name: "Finished", page: "finished" },
];

export const FLOW_STORAGE_KEY = "digitalSite.flow";

const REQUIRED_PERSONAL_DETAILS = [
  "title",
  "firstName",
  "lastName",
  "dateOfBirth",
  "email",
  "phone",
  "addressLine1",
  "suburb",
  "state",
  "postcode",
];

export function generateCrmId(random = Math.random) {
  return `CRM-${String(Math.floor(random() * 100_000_000)).padStart(8, "0")}`;
}

export function createInitialFlowState(crmId = generateCrmId()) {
  return {
    page: "yourDetails",
    crmId,
    personalDetails: {
      title: "",
      firstName: "",
      lastName: "",
      dateOfBirth: "",
      email: "",
      phone: "",
      addressLine1: "",
      suburb: "",
      state: "WA",
      postcode: "",
    },
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
  return STEPPER_STEPS.findIndex((step) => step.page === page);
}

function hasCompletePersonalDetails(personalDetails) {
  return REQUIRED_PERSONAL_DETAILS.every((field) => personalDetails[field]?.trim());
}

export function canContinue(state) {
  switch (state.page) {
    case "yourDetails":
      return hasCompletePersonalDetails(state.personalDetails)
        && (state.vehicle.searchChoice === "Skip"
          || (state.vehicle.searchChoice === "RegoLookup" && state.vehicle.details !== null))
        && state.coverId !== null;
    case "confirmPay":
      return state.cart !== null
        && state.paymentPlan !== null
        && state.acceptedPaymentAuthTerms
        && state.acceptedRoadsideAssistTerms;
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
    && (state.cart.vehicle?.rego ?? null) === (selected.rego?.replace(/\s+/g, "").toUpperCase() ?? null)
    && JSON.stringify(state.cart.personalDetails ?? null) === JSON.stringify(state.personalDetails);
}

export function toCreateCartRequest(state) {
  return {
    crmId: state.crmId,
    isBrokenDown: "No",
    personalDetails: state.personalDetails,
    vehicle: toVehicleSelection(state.vehicle),
    coverId: state.coverId,
  };
}

export function toUpdateCartRequest(state) {
  return {
    crmId: state.crmId,
    version: state.cart.version,
    personalDetails: state.personalDetails,
    vehicle: toVehicleSelection(state.vehicle),
    coverId: state.coverId,
  };
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

function resumeFrom(state, cart) {
  const isLookup = cart.vehicleSearchChoice === "RegoLookup" && cart.vehicle;
  return {
    ...state,
    page: "yourDetails",
    vehicle: isLookup
      ? { searchChoice: "RegoLookup", rego: cart.vehicle.rego, details: cart.vehicle }
      : { searchChoice: "Skip", rego: "", details: null },
    personalDetails: cart.personalDetails ?? state.personalDetails,
    coverId: cart.coverId,
    cart,
  };
}

export function flowReducer(state, action) {
  switch (action.type) {
    case "changePersonalDetails":
      return { ...state, personalDetails: { ...state.personalDetails, [action.field]: action.value } };
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
      return { ...state, cart: action.cart, page: "confirmPay" };
    case "choosePaymentPlan":
      return { ...state, paymentPlan: action.value, acceptedPaymentAuthTerms: false, acceptedRoadsideAssistTerms: false };
    case "acceptTerms":
      return { ...state, [action.field]: action.value };
    case "checkoutStarted":
      return { ...state, checkout: action.checkout, paymentResult: null, page: "confirmPay" };
    case "paymentSubmitted":
      return state.checkout ? { ...state, page: "finished", paymentResult: action.resultCode ?? null } : state;
    case "retryPayment":
      return state.page === "finished" ? { ...state, page: "confirmPay", checkout: null, paymentResult: null } : state;
    case "next":
      if (!canContinue(state)) return state;
      return state.page === "yourDetails" ? { ...state, page: "confirmPay" } : state;
    case "back":
      return state.page === "confirmPay" ? { ...state, page: "yourDetails", checkout: null } : state;
    case "goToStep": {
      const target = STEPPER_STEPS[action.index]?.page;
      const isEarlierStep = target && action.index < stepperIndexFor(state.page);
      return isEarlierStep && state.page !== "finished" ? { ...state, page: target, checkout: null } : state;
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
