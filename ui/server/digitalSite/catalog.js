import { readFileSync } from "node:fs";
import path from "node:path";
import { uiRoot } from "../shared/paths.js";

const catalogPath = path.resolve(uiRoot, "data", "rsa-catalog.json");
const catalog = JSON.parse(readFileSync(catalogPath, "utf8"));

export function getCatalog() {
  return { covers: catalog.covers, paymentPlans: catalog.paymentPlans };
}

export function findCover(coverId) {
  return catalog.covers.find((cover) => cover.id === coverId);
}

export function findPaymentPlan(paymentPlanId) {
  return catalog.paymentPlans.find((plan) => plan.id === paymentPlanId);
}

export function getDemoVehicles() {
  return catalog.vehicles;
}
