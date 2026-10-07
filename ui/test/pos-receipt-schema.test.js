import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { generateReceipt } from "../server/pos/receiptGenerator.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const schemaPath = path.resolve(__dirname, "..", "..", "contracts", "pos-receipt-v1.schema.json");
const schema = JSON.parse(readFileSync(schemaPath, "utf8"));
const { customer: customerSchema, financials: financialsSchema, payment: paymentSchema, lineItem: lineItemSchema, discount: discountSchema } = schema.definitions;

const validRequest = { storeId: "STORE-NORTH", tillId: "TILL-1", operatorId: "OP-100", paymentType: "EFTPOS" };

/**
 * Every object definition declares additionalProperties: false and requires
 * every property it defines, so a conforming value's key set must equal
 * `required` exactly - not merely be a subset of `properties`.
 */
function assertExactKeys(value, objectSchema) {
  assert.equal(objectSchema.additionalProperties, false, "schema must forbid additional properties for this assertion to be meaningful");
  assert.deepEqual(Object.keys(value).sort(), [...objectSchema.required].sort());
}

function assertMatchesReceiptSchema(receipt) {
  assertExactKeys(receipt, schema);
  assert.match(receipt.receiptBarcode, new RegExp(schema.properties.receiptBarcode.pattern));
  assert.equal(typeof receipt.eventId, "string");

  assertExactKeys(receipt.customer, customerSchema);
  assert.equal(typeof receipt.customer.membershipLevel, "string");

  assertExactKeys(receipt.financials, financialsSchema);
  assert.equal(typeof receipt.financials.totalExGst, "number");
  assert.ok(["INVOICE", "CREDIT"].includes(receipt.financials.refundFlag));

  assert.ok(Array.isArray(receipt.payments) && receipt.payments.length >= schema.properties.payments.minItems);
  for (const payment of receipt.payments) {
    assertExactKeys(payment, paymentSchema);
    assert.equal(typeof payment.paymentType, "string");
    assert.equal(typeof payment.amount, "number");
  }

  assert.ok(Array.isArray(receipt.lineItems) && receipt.lineItems.length >= schema.properties.lineItems.minItems);
  for (const lineItem of receipt.lineItems) {
    assertExactKeys(lineItem, lineItemSchema);
    assert.equal(typeof lineItem.itemCode, "string");
    assert.equal(typeof lineItem.itemDescription, "string");
    assert.ok(Number.isInteger(lineItem.quantity) && lineItem.quantity >= lineItemSchema.properties.quantity.minimum);
    assert.equal(typeof lineItem.unitPrice, "number");
    assert.equal(typeof lineItem.lineAmount, "number");
    assert.ok(lineItemSchema.properties.gstCategory.enum.includes(lineItem.gstCategory));
    assert.equal(typeof lineItem.dispositionCodes, "string");
    assertExactKeys(lineItem.discount, discountSchema);
  }
}

test("randomly generated receipts contain exactly the schema's required keys and no extras", () => {
  assertMatchesReceiptSchema(generateReceipt({ ...validRequest, seed: 3, itemCount: 1 }, { sequence: 1 }));
  assertMatchesReceiptSchema(generateReceipt({ ...validRequest, seed: 9, itemCount: 5 }, { sequence: 2 }));
  assertMatchesReceiptSchema(generateReceipt({ ...validRequest, paymentType: "CASH", seed: 12, itemCount: 2 }, { sequence: 3 }));
});

test("receipts built from explicit line items also contain exactly the schema's required keys and no extras", () => {
  assertMatchesReceiptSchema(generateReceipt({
    ...validRequest,
    lineItems: [{ itemCode: "ROADSIDE-ASSIST", quantity: 1 }, { itemCode: "DRINK-BOT", quantity: 4 }],
  }, { sequence: 4 }));
});
