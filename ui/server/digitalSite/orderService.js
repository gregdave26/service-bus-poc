import { randomUUID } from "node:crypto";
import { findCover, findPaymentPlan } from "./catalog.js";
import { isValidRego, lookupVehicle } from "./vehicleLookup.js";

export const RSA_PRODUCT_TYPE = "roadside-assistance";
export const VEHICLE_SEARCH_CHOICES = ["RegoLookup", "Skip"];

export function validateOrderRequest(body) {
  if (body?.isBrokenDown !== "No") return "Customers who are broken down must call RAC to join";
  const searchChoice = body?.vehicle?.searchChoice;
  if (!VEHICLE_SEARCH_CHOICES.includes(searchChoice)) return "vehicle.searchChoice must be RegoLookup or Skip";
  if (searchChoice === "RegoLookup") {
    if (!isValidRego(body.vehicle.rego)) return "vehicle.rego is not a valid registration";
    if (!lookupVehicle(body.vehicle.rego)) return "vehicle.rego was not found";
  }
  if (!findCover(body?.coverId)) return "coverId is not a known cover";
  if (!findPaymentPlan(body?.paymentPlan)) return "paymentPlan is not a known payment plan";
  if (body?.acceptedPaymentAuthTerms !== true) return "Payment authorisation terms must be accepted";
  if (body?.acceptedRoadsideAssistTerms !== true) return "Roadside Assistance terms must be accepted";
  return null;
}

export function formatOrderId(sequence) {
  return `RSA-${String(sequence).padStart(6, "0")}`;
}

/** Prices always come from the catalog so a client cannot choose its own price. */
export function priceFor(cover, paymentPlanId) {
  return paymentPlanId === "Monthly"
    ? { instalmentAmount: cover.monthlyPrice, frequency: "monthly", totalAnnualCost: cover.monthlyTotal }
    : { instalmentAmount: cover.annualPrice, frequency: "annual", totalAnnualCost: cover.annualPrice };
}

export function createOrder(request, { sequence, now = new Date(), createContactId = randomUUID }) {
  const cover = findCover(request.coverId);
  const paymentPlan = findPaymentPlan(request.paymentPlan);
  const vehicle = request.vehicle.searchChoice === "RegoLookup" ? lookupVehicle(request.vehicle.rego) : null;
  return {
    orderId: formatOrderId(sequence),
    contactId: createContactId(),
    createdAt: now.toISOString(),
    cover: { id: cover.id, name: cover.name },
    paymentPlan: { id: paymentPlan.id, title: paymentPlan.title },
    price: priceFor(cover, paymentPlan.id),
    vehicle,
    publishStatus: "pending",
  };
}

export function toProductHoldingChange(order) {
  return {
    type: "ProductHoldingChange",
    contactId: order.contactId,
    holdingId: order.orderId,
    productType: RSA_PRODUCT_TYPE,
    action: "created",
    holdingData: {
      channel: "digital-site",
      coverId: order.cover.id,
      coverName: order.cover.name,
      paymentPlan: order.paymentPlan.id,
      instalmentAmount: order.price.instalmentAmount,
      totalAnnualCost: order.price.totalAnnualCost,
      vehicleRego: order.vehicle?.rego ?? null,
    },
  };
}

/**
 * The order is persisted before publishing so a Service Bus outage never loses a sale;
 * the publish outcome is recorded against the order instead.
 */
export function createOrderPlacer({ repository, publish, log }) {
  return async function placeOrder(request) {
    const sequence = repository.nextSequenceNumber();
    const order = createOrder(request, { sequence });
    repository.insertOrder(order, sequence);
    log("order.created", { orderId: order.orderId, coverId: order.cover.id, paymentPlan: order.paymentPlan.id });

    try {
      const event = await publish(toProductHoldingChange(order));
      repository.updatePublishStatus(order.orderId, { publishStatus: "published", eventId: event.id });
      log("order.published", { orderId: order.orderId, eventId: event.id });
    } catch (error) {
      const publishError = error instanceof Error ? error.message : String(error);
      repository.updatePublishStatus(order.orderId, { publishStatus: "publish_failed", publishError });
      log("order.publish_failed", { orderId: order.orderId, error: publishError });
    }

    return repository.getOrderById(order.orderId);
  };
}
