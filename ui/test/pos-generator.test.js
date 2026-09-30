import assert from "node:assert/strict";
import test from "node:test";
import { generateReceipt, validateGenerateRequest } from "../pos/receiptGenerator.js";
import { getCatalog } from "../pos/catalog.js";

const validRequest = { storeId: "STORE-NORTH", tillId: "TILL-1", operatorId: "OP-100", paymentType: "EFTPOS" };

test("rejects a request with an unknown store", () => {
  assert.equal(
    validateGenerateRequest({ ...validRequest, storeId: "NOPE" }),
    "A valid storeId is required",
  );
});

test("rejects a request with an unknown till, operator, or payment type", () => {
  assert.equal(validateGenerateRequest({ ...validRequest, tillId: "NOPE" }), "A valid tillId is required");
  assert.equal(validateGenerateRequest({ ...validRequest, operatorId: "NOPE" }), "A valid operatorId is required");
  assert.equal(validateGenerateRequest({ ...validRequest, paymentType: "NOPE" }), "A valid paymentType is required");
});

test("rejects an out-of-range seed", () => {
  assert.match(validateGenerateRequest({ ...validRequest, seed: -1 }), /seed must be an integer/);
  assert.match(validateGenerateRequest({ ...validRequest, seed: 1.5 }), /seed must be an integer/);
  assert.match(validateGenerateRequest({ ...validRequest, seed: 2 ** 32 }), /seed must be an integer/);
});

test("rejects an invalid refundFlag", () => {
  assert.equal(validateGenerateRequest({ ...validRequest, refundFlag: "yes" }), "refundFlag must be INVOICE or CREDIT");
});

test("rejects an invalid explicit customer", () => {
  assert.equal(validateGenerateRequest({ ...validRequest, customer: "nope" }), "customer must be an object");
  assert.equal(
    validateGenerateRequest({ ...validRequest, customer: { membershipLevel: "PLATINUM" } }),
    "Unknown membershipLevel: PLATINUM",
  );
  assert.equal(
    validateGenerateRequest({ ...validRequest, customer: { vehicleRegistration: 123 } }),
    "customer.vehicleRegistration must be a string or null",
  );
});

test("rejects explicit line items referencing an unknown itemCode or an invalid quantity", () => {
  assert.equal(
    validateGenerateRequest({ ...validRequest, lineItems: [{ itemCode: "NOPE", quantity: 1 }] }),
    "Unknown product itemCode: NOPE",
  );
  assert.match(
    validateGenerateRequest({ ...validRequest, lineItems: [{ itemCode: "BAT-REPL-001", quantity: 0 }] }),
    /Quantity for BAT-REPL-001/,
  );
  assert.equal(validateGenerateRequest({ ...validRequest, lineItems: [] }), "lineItems must be a non-empty array");
  assert.equal(validateGenerateRequest({ ...validRequest, lineItems: "not-an-array" }), "lineItems must be a non-empty array");
  assert.equal(validateGenerateRequest({ ...validRequest, lineItems: [{ quantity: 1 }] }), "Each line item requires an itemCode");
  assert.match(
    validateGenerateRequest({ ...validRequest, lineItems: Array.from({ length: 21 }, () => ({ itemCode: "BAT-REPL-001" })) }),
    /lineItems cannot exceed 20 entries/,
  );
  assert.equal(
    validateGenerateRequest({ ...validRequest, lineItems: [{ itemCode: "BAT-REPL-001", discount: { discountAmount: "5" } }] }),
    "discount.discountAmount for BAT-REPL-001 must be a number",
  );
});

test("rejects an out-of-range itemCount", () => {
  assert.match(validateGenerateRequest({ ...validRequest, itemCount: 0 }), /itemCount must be an integer/);
  assert.match(validateGenerateRequest({ ...validRequest, itemCount: 21 }), /itemCount must be an integer/);
  assert.match(validateGenerateRequest({ ...validRequest, itemCount: 2.5 }), /itemCount must be an integer/);
});

test("accepts a fully valid request", () => {
  assert.equal(validateGenerateRequest(validRequest), null);
});

test("generates identical line items, customer, and totals for the same seed and inputs", () => {
  const request = { ...validRequest, seed: 42, itemCount: 3 };
  const first = generateReceipt(request, { sequence: 1 });
  const second = generateReceipt(request, { sequence: 1 });
  assert.deepEqual(first.lineItems, second.lineItems);
  assert.deepEqual(first.customer, second.customer);
  assert.deepEqual(first.financials, second.financials);
  assert.deepEqual(first.payments, second.payments);
});

test("generates different line items for different seeds", () => {
  const first = generateReceipt({ ...validRequest, seed: 1, itemCount: 3 }, { sequence: 1 });
  const second = generateReceipt({ ...validRequest, seed: 2, itemCount: 3 }, { sequence: 1 });
  assert.notDeepEqual(first.lineItems, second.lineItems);
});

test("computes mathematically consistent totals from randomly selected line items", () => {
  const receipt = generateReceipt({ ...validRequest, seed: 7, itemCount: 5 }, { sequence: 3 });
  for (const lineItem of receipt.lineItems) {
    assert.equal(Math.round(lineItem.unitPrice * lineItem.quantity * 100) / 100, lineItem.lineAmount);
    const expectedGstRate = lineItem.gstCategory === "FREE" ? 0 : 0.1;
    assert.equal(Math.round(lineItem.lineAmount * expectedGstRate * 100) / 100, lineItem.gstAmount);
  }
  const expectedExGst = Math.round(receipt.lineItems.reduce((sum, lineItem) => sum + lineItem.lineAmount * 100, 0)) / 100;
  assert.equal(receipt.financials.totalExGst, expectedExGst);
  const expectedGst = Math.round(receipt.lineItems.reduce((sum, lineItem) => sum + lineItem.gstAmount * 100, 0)) / 100;
  assert.equal(receipt.financials.totalGst, expectedGst);
  assert.equal(Math.round((receipt.financials.totalExGst + receipt.financials.totalGst) * 100) / 100, receipt.financials.totalIncGst);
  assert.equal(receipt.payments[0].amount, receipt.financials.totalIncGst);
});

test("honors explicit line items exactly, including quantities", () => {
  const receipt = generateReceipt({
    ...validRequest,
    lineItems: [
      { itemCode: "BAT-REPL-001", quantity: 2 },
      { itemCode: "PARTS-FUSE-KIT" },
    ],
  }, { sequence: 5 });

  assert.deepEqual(receipt.lineItems.map((lineItem) => [lineItem.itemCode, lineItem.quantity]), [
    ["BAT-REPL-001", 2],
    ["PARTS-FUSE-KIT", 1],
  ]);
  assert.equal(receipt.financials.totalExGst, 189 * 2 + 24.95);
});

test("supports roadside battery, parts, fuel, service, and JOR line items", () => {
  const supportedCategories = new Set([
    "Battery Replacements",
    "Parts and Consumables",
    "Fuel and Roadside Services",
    "Join-On-Road",
  ]);
  const products = getCatalog().products.filter((product) => supportedCategories.has(product.itemCategory));

  assert.ok(products.length > 0);
  const receipt = generateReceipt({
    ...validRequest,
    lineItems: products.map((product) => ({ itemCode: product.itemCode })),
  }, { sequence: 12 });

  assert.deepEqual(
    new Set(receipt.lineItems.map((lineItem) => lineItem.itemCategory)),
    supportedCategories,
  );
  assert.ok(receipt.lineItems.every((lineItem) => lineItem.itemParentCategory === "Roadside Services" || lineItem.itemParentCategory === "Membership"));
});

test("applies an explicit line item discount before computing GST", () => {
  const receipt = generateReceipt({
    ...validRequest,
    lineItems: [{ itemCode: "BAT-REPL-001", quantity: 1, discount: { discountType: "PROMO", discountAmount: 2 } }],
  }, { sequence: 6 });

  const [lineItem] = receipt.lineItems;
  assert.deepEqual(lineItem.discount, { discountType: "PROMO", discountAmount: 2 });
  assert.equal(lineItem.lineAmount, 187);
  assert.equal(lineItem.gstAmount, 18.7);
});

test("honors an explicit customer and defaults omitted fields", () => {
  const receipt = generateReceipt({
    ...validRequest,
    customer: { membershipLevel: "6", membershipNumber: "MB1234567" },
    lineItems: [{ itemCode: "BAT-REPL-001" }],
  }, { sequence: 7 });

  assert.equal(receipt.customer.membershipLevel, "6");
  assert.equal(receipt.customer.membershipNumber, "MB1234567");
  assert.equal(receipt.customer.customerNumber, null);
  assert.equal(receipt.customer.vehicleVin, null);
});

test("keeps a random receipt customer profile stable for the same membership number", () => {
  const first = generateReceipt({
    ...validRequest,
    seed: 1,
    customer: { membershipNumber: "MB1234567", membershipLevel: "0" },
  }, { sequence: 13 });
  const second = generateReceipt({
    ...validRequest,
    seed: 999,
    customer: { membershipNumber: "MB1234567", membershipLevel: "0" },
  }, { sequence: 14 });

  assert.equal(first.customer.membershipNumber, "MB1234567");
  assert.deepEqual(
    [first.customer.vehicleRegistration, first.customer.vehicleVin, first.customer.membershipLevel],
    [second.customer.vehicleRegistration, second.customer.vehicleVin, second.customer.membershipLevel],
  );
  assert.notEqual(first.customer.vehicleRegistration, null);
  assert.notEqual(first.customer.vehicleVin, null);
  assert.notEqual(first.customer.membershipLevel, "0");
});

test("accepts numeric membership levels and canonical string flags", () => {
  const receipt = generateReceipt({
    ...validRequest,
    refundFlag: "CREDIT",
    customer: { membershipLevel: "2" },
    lineItems: [{ itemCode: "BAT-REPL-001" }],
  }, { sequence: 11 });
  assert.equal(receipt.customer.membershipLevel, "2");
  assert.equal(receipt.financials.refundFlag, "CREDIT");
  assert.equal(receipt.lineItems[0].gstCategory, "TAXABLE SUPPLY");
  assert.equal(typeof receipt.lineItems[0].dispositionCodes, "string");
});

test("sets pricingLevel1 only for membership levels 6, 7, and 8", () => {
  for (const level of ["0", "2", "6", "7", "8"]) {
    const receipt = generateReceipt({
      ...validRequest,
      customer: { membershipLevel: level },
      lineItems: [{ itemCode: "BAT-REPL-001" }],
    }, { sequence: Number(level) + 20 });
    assert.equal(receipt.lineItems[0].pricingLevel1, ["6", "7", "8"].includes(level) ? "Y" : "N");
  }
});

test("only populates eftpos payment details for card-based payment types", () => {
  const cardReceipt = generateReceipt({ ...validRequest, paymentType: "EFTPOS", seed: 3 }, { sequence: 8 });
  assert.ok(cardReceipt.payments[0].eftposTerminalId);
  assert.ok(cardReceipt.payments[0].eftposRefNo);

  const cashReceipt = generateReceipt({ ...validRequest, paymentType: "CASH", seed: 3 }, { sequence: 9 });
  assert.equal(cashReceipt.payments[0].eftposTerminalId, null);
  assert.equal(cashReceipt.payments[0].eftposRefNo, null);
});

test("defaults omitted payment type to cash", () => {
  const receipt = generateReceipt({ ...validRequest, paymentType: undefined, seed: 3 }, { sequence: 10 });
  assert.equal(receipt.payments[0].paymentType, "CASH");
  assert.equal(receipt.payments[0].eftposTerminalId, null);
});

test("formats receipt barcodes from the supplied sequence", () => {
  const receipt = generateReceipt(validRequest, { sequence: 1 });
  assert.equal(receipt.receiptBarcode, "200000000001");
  const receiptFar = generateReceipt(validRequest, { sequence: 250 });
  assert.equal(receiptFar.receiptBarcode, "200000000250");
});
