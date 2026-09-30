import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const catalogPath = path.resolve(__dirname, "..", "data", "pos-catalog.json");

function loadCatalog() {
  return JSON.parse(readFileSync(catalogPath, "utf8"));
}

const catalog = loadCatalog();

function findById(entries, id) {
  return entries.find((entry) => entry.id === id);
}

export function getCatalog() {
  return catalog;
}

export function findStore(storeId) {
  return findById(catalog.stores, storeId);
}

export function findTill(tillId) {
  return findById(catalog.tills, tillId);
}

export function findOperator(operatorId) {
  return findById(catalog.operators, operatorId);
}

export function findPaymentMethod(paymentTypeId) {
  return findById(catalog.paymentMethods, paymentTypeId);
}

export function findMembershipLevel(membershipLevelId) {
  return findById(catalog.membershipLevels, membershipLevelId);
}

export function findProduct(itemCode) {
  return catalog.products.find((product) => product.itemCode === itemCode);
}

export function gstRateFor(gstCategory) {
  return catalog.gstCategories[gstCategory]?.rate ?? 0;
}
