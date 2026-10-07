import { getDemoVehicles } from "./catalog.js";

const regoPattern = /^[A-Z0-9]{1,9}$/;

export function normaliseRego(rego) {
  return typeof rego === "string" ? rego.toUpperCase().replace(/\s+/g, "") : "";
}

export function isValidRego(rego) {
  return regoPattern.test(normaliseRego(rego));
}

/** Mock of the Vehicle API: only the demo vehicles in the catalog are "registered". */
export function lookupVehicle(rego) {
  const normalisedRego = normaliseRego(rego);
  return getDemoVehicles().find((vehicle) => vehicle.rego === normalisedRego) ?? null;
}
