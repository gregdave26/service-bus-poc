import { randomUUID } from "node:crypto";
import { getCatalog, findStore, findTill, findOperator, findPaymentMethod, findMembershipLevel, findProduct, gstRateFor } from "./catalog.js";
import { createSeededRandom, generateRandomSeed, pickInt, pickItem } from "./rng.js";
import { toAmount, toCents } from "./money.js";

const maxSeed = 2 ** 32 - 1;
const maxExplicitLineItems = 20;
const maxQuantityPerLine = 99;
const randomLineItemRange = { min: 1, max: 5 };
const randomQuantityRange = { min: 1, max: 3 };
const vehicleRegistrationLetters = "ABCDEFGHJKLMNPRSTUVWXYZ";
const digitCharacters = "0123456789";
const vinCharacters = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";

function isNonEmptyString(value) {
  return typeof value === "string" && value.trim() !== "";
}

function isNullableString(value) {
  return value === null || value === undefined || typeof value === "string";
}

function canonicalMembershipLevel(value) {
  const normalized = String(value ?? "NONE").toUpperCase();
  return { NONE: "0", BRONZE: "1", SILVER: "2", GOLD: "6" }[normalized] ?? normalized;
}

function isValidSeed(seed) {
  return Number.isInteger(seed) && seed >= 0 && seed <= maxSeed;
}

function validateExplicitLineItems(lineItems) {
  if (!Array.isArray(lineItems) || lineItems.length === 0) return "lineItems must be a non-empty array";
  if (lineItems.length > maxExplicitLineItems) return `lineItems cannot exceed ${maxExplicitLineItems} entries`;
  for (const lineItem of lineItems) {
    if (!isNonEmptyString(lineItem?.itemCode)) return "Each line item requires an itemCode";
    if (!findProduct(lineItem.itemCode)) return `Unknown product itemCode: ${lineItem.itemCode}`;
    const quantity = lineItem.quantity ?? 1;
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > maxQuantityPerLine) {
      return `Quantity for ${lineItem.itemCode} must be an integer between 1 and ${maxQuantityPerLine}`;
    }
    if (lineItem.discount?.discountAmount !== undefined && typeof lineItem.discount.discountAmount !== "number") {
      return `discount.discountAmount for ${lineItem.itemCode} must be a number`;
    }
  }
  return null;
}

function validateExplicitCustomer(customer) {
  if (typeof customer !== "object" || customer === null || Array.isArray(customer)) return "customer must be an object";
  if (customer.membershipLevel !== undefined && !findMembershipLevel(customer.membershipLevel)) {
    return `Unknown membershipLevel: ${customer.membershipLevel}`;
  }
  for (const field of ["membershipNumber", "customerNumber", "vehicleRegistration", "vehicleVin"]) {
    if (customer[field] !== undefined && !isNullableString(customer[field])) {
      return `customer.${field} must be a string or null`;
    }
  }
  return null;
}

export function validateGenerateRequest(body) {
  if (!isNonEmptyString(body?.storeId) || !findStore(body.storeId)) return "A valid storeId is required";
  if (!isNonEmptyString(body?.tillId) || !findTill(body.tillId)) return "A valid tillId is required";
  if (!isNonEmptyString(body?.operatorId) || !findOperator(body.operatorId)) return "A valid operatorId is required";
  if (body?.transactionDate !== undefined &&
      (typeof body.transactionDate !== "string" || Number.isNaN(Date.parse(body.transactionDate)))) {
    return "transactionDate must be a valid ISO date";
  }
  if (body?.paymentType !== undefined && (!isNonEmptyString(body.paymentType) || !findPaymentMethod(body.paymentType))) return "A valid paymentType is required";
  if (body?.seed !== undefined && !isValidSeed(body.seed)) return `seed must be an integer between 0 and ${maxSeed}`;
  if (body?.refundFlag !== undefined && !["INVOICE", "CREDIT"].includes(body.refundFlag)) {
    return "refundFlag must be INVOICE or CREDIT";
  }
  if (body?.customer !== undefined) {
    const customerError = validateExplicitCustomer(body.customer);
    if (customerError) return customerError;
  }
  if (body?.lineItems !== undefined) return validateExplicitLineItems(body.lineItems);
  if (body?.itemCount !== undefined && (!Number.isInteger(body.itemCount) || body.itemCount < 1 || body.itemCount > maxExplicitLineItems)) {
    return `itemCount must be an integer between 1 and ${maxExplicitLineItems}`;
  }
  return null;
}

function buildExplicitLineItems(lineItems) {
  return lineItems.map((lineItem) => ({
    product: findProduct(lineItem.itemCode),
    quantity: lineItem.quantity ?? 1,
    discount: lineItem.discount,
  }));
}

function buildRandomLineItems(random, itemCount) {
  const { products } = getCatalog();
  const count = itemCount ?? pickInt(random, randomLineItemRange.min, randomLineItemRange.max);
  return Array.from({ length: count }, () => ({
    product: pickItem(random, products),
    quantity: pickInt(random, randomQuantityRange.min, randomQuantityRange.max),
  }));
}

function toReceiptLineItem({ product, quantity, discount }, lineNumber, pricingLevel) {
  const unitPriceCents = toCents(product.unitPrice);
  const grossCents = unitPriceCents * quantity;
  const discountAmountCents = toCents(discount?.discountAmount ?? 0);
  const lineAmountCents = grossCents - discountAmountCents;
  const gstAmountCents = Math.round(lineAmountCents * gstRateFor(product.gstCategory));
  return {
    lineNumber,
    itemCode: product.itemCode,
    itemDescription: product.itemDescription,
    quantity,
    unitPrice: toAmount(unitPriceCents),
    lineAmount: toAmount(lineAmountCents),
    gstAmount: toAmount(gstAmountCents),
    gstCategory: product.gstCategory === "GST_FREE" ? "FREE" : "TAXABLE SUPPLY",
    itemParentCategory: product.itemParentCategory,
    itemCategory: product.itemCategory,
    itemSubCategory: product.itemSubCategory,
    discount: {
      discountType: discount?.discountType ?? null,
      discountAmount: toAmount(discountAmountCents),
    },
    pricingLevel1: ["6", "7", "8"].includes(pricingLevel) ? "Y" : "N",
    dispositionCodes: product.dispositionCodes.join(","),
    membershipProductGlCode: product.membershipProductGlCode,
  };
}

function buildDigitString(random, length) {
  return Array.from({ length }, () => digitCharacters[pickInt(random, 0, digitCharacters.length - 1)]).join("");
}

function buildVehicleRegistration(random) {
  const letters = Array.from({ length: 3 }, () => vehicleRegistrationLetters[pickInt(random, 0, vehicleRegistrationLetters.length - 1)]).join("");
  return `${letters}${buildDigitString(random, 3)}`;
}

function buildVehicleVin(random) {
  return Array.from({ length: 17 }, () => vinCharacters[pickInt(random, 0, vinCharacters.length - 1)]).join("");
}

function buildMembershipNumber(random) {
  return `MB${buildDigitString(random, 7)}`;
}

function buildCustomerNumber(random) {
  return `CUS${buildDigitString(random, 6)}`;
}

/**
 * A carwash transaction is always tied to a vehicle, but the customer is not
 * necessarily a loyalty member; membership fields are only populated when a
 * membership is actually present so unregistered walk-ins are represented
 * honestly rather than with fabricated membership data.
 */
function buildRandomCustomer(random, catalog) {
  const hasMembership = pickInt(random, 0, 1) === 1;
  const membershipLevels = catalog.membershipLevels.filter((level) => level.id !== "0");
  const   membershipLevel = hasMembership ? canonicalMembershipLevel(pickItem(random, membershipLevels).id) : "0";
  const hasVin = pickInt(random, 0, 1) === 1;
  return {
    membershipNumber: hasMembership ? buildMembershipNumber(random) : null,
    membershipLevel,
    customerNumber: hasMembership ? buildCustomerNumber(random) : null,
    vehicleRegistration: buildVehicleRegistration(random),
    vehicleVin: hasVin ? buildVehicleVin(random) : null,
  };
}

function buildCustomer(random, catalog, explicitCustomer) {
  if (!explicitCustomer) return buildRandomCustomer(random, catalog);
  return {
    membershipNumber: explicitCustomer.membershipNumber ?? null,
    membershipLevel: canonicalMembershipLevel(explicitCustomer.membershipLevel),
    customerNumber: explicitCustomer.customerNumber ?? null,
    vehicleRegistration: explicitCustomer.vehicleRegistration ?? buildVehicleRegistration(random),
    vehicleVin: explicitCustomer.vehicleVin ?? null,
  };
}

function buildEftposTerminalId(random) {
  return `TERM-${buildDigitString(random, 6)}`;
}

function buildEftposRefNo(random) {
  return `REF-${buildDigitString(random, 9)}`;
}

function buildPayments(random, request, totalIncGstCents) {
  const paymentType = request.paymentType ?? "CASH";
  const paymentMethod = findPaymentMethod(paymentType);
  return [{
    paymentType,
    amount: toAmount(totalIncGstCents),
    eftposTerminalId: paymentMethod.requiresEftposDetails ? buildEftposTerminalId(random) : null,
    eftposRefNo: paymentMethod.requiresEftposDetails ? buildEftposRefNo(random) : null,
    paymentGl: paymentMethod.paymentGl,
  }];
}

function nextReceiptBarcode(sequence) {
  return `20${String(sequence).padStart(10, "0")}`;
}

/**
 * Produces the canonical POS receipt JSON. Given the same seed and the same
 * selection inputs (or the same explicit line items/customer) this always
 * generates identical line items, customer details, and totals, which is
 * what makes the receipt reproducible from its stored seed.
 */
export function generateReceipt(request, { sequence }) {
  const catalog = getCatalog();
  const seed = request.seed ?? generateRandomSeed();
  const random = createSeededRandom(seed);
  const lineItemInputs = Array.isArray(request.lineItems)
    ? buildExplicitLineItems(request.lineItems)
    : buildRandomLineItems(random, request.itemCount);

  const customer = buildCustomer(random, catalog, request.customer);
  const lineItems = lineItemInputs.map((lineItemInput, index) => toReceiptLineItem(lineItemInput, index + 1, customer.membershipLevel));
  const totalExGstCents = lineItems.reduce((sum, lineItem) => sum + toCents(lineItem.lineAmount), 0);
  const totalGstCents = lineItems.reduce((sum, lineItem) => sum + toCents(lineItem.gstAmount), 0);
  const totalIncGstCents = totalExGstCents + totalGstCents;

  return {
    receiptBarcode: nextReceiptBarcode(sequence),
    eventId: randomUUID(),
    storeId: request.storeId,
    tillId: request.tillId,
    operatorId: request.operatorId,
    transactionDate: request.transactionDate ?? new Date().toISOString(),
    customer,
    financials: {
      totalExGst: toAmount(totalExGstCents),
      totalGst: toAmount(totalGstCents),
      totalIncGst: toAmount(totalIncGstCents),
      refundFlag: request.refundFlag ?? "INVOICE",
    },
    payments: buildPayments(random, request, totalIncGstCents),
    lineItems,
  };
}
