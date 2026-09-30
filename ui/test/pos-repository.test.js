import assert from "node:assert/strict";
import test from "node:test";
import { DatabaseSync } from "node:sqlite";
import { createReceiptRepository } from "../pos/receiptRepository.js";
import { generateReceipt } from "../pos/receiptGenerator.js";

function buildRepository() {
  return createReceiptRepository(new DatabaseSync(":memory:"));
}

const request = { storeId: "STORE-NORTH", tillId: "TILL-1", operatorId: "OP-100", paymentType: "EFTPOS", seed: 11, itemCount: 2 };

test("assigns sequential sequence numbers starting from one", () => {
  const repository = buildRepository();
  assert.equal(repository.nextSequenceNumber(), 1);
  repository.insertReceipt(generateReceipt(request, { sequence: repository.nextSequenceNumber() }));
  assert.equal(repository.nextSequenceNumber(), 2);
});

test("persists the header and every line item together", () => {
  const repository = buildRepository();
  const receipt = generateReceipt(request, { sequence: repository.nextSequenceNumber() });
  repository.insertReceipt(receipt);

  const listed = repository.listReceipts();
  assert.equal(listed.length, 1);
  assert.equal(listed[0].receiptBarcode, receipt.receiptBarcode);
  assert.equal(listed[0].totalIncGst, receipt.financials.totalIncGst);
  assert.equal(listed[0].refundFlag, receipt.financials.refundFlag);

  const detail = repository.getReceiptById(receipt.eventId);
  assert.equal(detail.lineItems.length, receipt.lineItems.length);
  assert.deepEqual(detail.lineItems.map((lineItem) => lineItem.itemCode), receipt.lineItems.map((lineItem) => lineItem.itemCode));
  assert.deepEqual(detail.lineItems[0].dispositionCodes, receipt.lineItems[0].dispositionCodes);
});

test("uses the authoritative Cardzoids normalized columns and mappings", () => {
  const db = new DatabaseSync(":memory:");
  const repository = createReceiptRepository(db);
  const receipt = generateReceipt({
    ...request,
    paymentType: "CASH",
    customer: { membershipNumber: "MB123", membershipLevel: "GOLD", vehicleRegistration: "ABC123", vehicleVin: "VIN123" },
    lineItems: [{ itemCode: "CW-BASIC", quantity: 1 }],
  }, { sequence: 1 });
  receipt.customer.membershipLevel = "6";
  repository.insertReceipt(receipt);

  assert.deepEqual(db.prepare("PRAGMA table_info(POS_RECEIPT_EVENT)").all().map((column) => column.name), [
    "ReceiptBarcode", "EventNumber", "InvoiceDate", "StoreNumber", "TerminalID", "EFTPOSID", "RefNo",
    "PaymentMethod", "CustomerNumber", "TotalIncGST", "TotalGST", "TotalExGST", "RefundFlag", "UserID", "CanonicalJson",
  ]);
  assert.deepEqual(db.prepare("PRAGMA table_info(POS_RECEIPT_LINE_ITEM)").all().map((column) => column.name), [
    "ReceiptBarcode", "ItemCode", "ItemDesc", "ItemQty", "UnitPrice", "LineAmount", "GstAmount", "GstCategory",
    "PaymentGL", "PricingLevel", "PricingLevel1", "LineDiscountType", "LineDiscountAmount", "ItemParentCategory",
    "ItemCategory", "ItemSubCategory", "MembershipProductGLCode", "MembershipNumber", "MembershipProductID",
    "VehicleRegistration", "VehicleVIN", "DispositionCodes",
  ]);
  const event = db.prepare("SELECT * FROM POS_RECEIPT_EVENT").get();
  const line = db.prepare("SELECT * FROM POS_RECEIPT_LINE_ITEM").get();
  assert.deepEqual({
    StoreNumber: event.StoreNumber, TerminalID: event.TerminalID, PaymentMethod: event.PaymentMethod,
    CustomerNumber: event.CustomerNumber, RefundFlag: event.RefundFlag, UserID: event.UserID,
  }, { StoreNumber: "STORE-NORTH", TerminalID: "001", PaymentMethod: "CASH", CustomerNumber: "MB123", RefundFlag: "INVOICE", UserID: "OP-100" });
  assert.deepEqual({
    GstCategory: line.GstCategory, PaymentGL: line.PaymentGL, PricingLevel: line.PricingLevel,
    PricingLevel1: line.PricingLevel1, MembershipNumber: line.MembershipNumber,
  }, { GstCategory: "TAXABLE SUPPLY", PaymentGL: "001-", PricingLevel: "6", PricingLevel1: "Y", MembershipNumber: "MB123" });
});

test("derives pricing fields from membershipLevel rather than product pricing metadata", () => {
  const db = new DatabaseSync(":memory:");
  const repository = createReceiptRepository(db);
  const receipt = generateReceipt({
    ...request,
    customer: { membershipNumber: "MB456", membershipLevel: "GOLD" },
    lineItems: [{ itemCode: "CW-BASIC", quantity: 1 }],
  }, { sequence: 1 });
  receipt.customer.membershipLevel = "7";
  receipt.lineItems[0].pricingLevel1 = "RETAIL";

  repository.insertReceipt(receipt);

  assert.deepEqual({ ...db.prepare("SELECT PricingLevel, PricingLevel1 FROM POS_RECEIPT_LINE_ITEM").get() }, {
    PricingLevel: "7",
    PricingLevel1: "Y",
  });
});

test("marks multiple canonical tenders as SPLIT", () => {
  const db = new DatabaseSync(":memory:");
  const repository = createReceiptRepository(db);
  const receipt = generateReceipt(request, { sequence: 1 });
  receipt.payments = [
    { ...receipt.payments[0], amount: receipt.financials.totalIncGst / 2 },
    { ...receipt.payments[0], amount: receipt.financials.totalIncGst / 2, paymentType: "CASH" },
  ];
  repository.insertReceipt(receipt);
  assert.equal(db.prepare("SELECT PaymentMethod FROM POS_RECEIPT_EVENT").get().PaymentMethod, "SPLIT");
});

test("returns the exact canonical JSON that was generated", () => {
  const repository = buildRepository();
  const receipt = generateReceipt(request, { sequence: repository.nextSequenceNumber() });
  repository.insertReceipt(receipt);

  const detail = repository.getReceiptById(receipt.eventId);
  assert.equal(detail.receiptJson, JSON.stringify(receipt));
  assert.deepEqual(detail.receipt, receipt);
});

test("returns null for an unknown receipt id", () => {
  const repository = buildRepository();
  assert.equal(repository.getReceiptById("missing"), null);
});

test("rolls back the entire insert when the header write fails, leaving no orphaned line items", () => {
  const repository = buildRepository();
  const receipt = generateReceipt(request, { sequence: repository.nextSequenceNumber() });
  repository.insertReceipt(receipt);

  assert.throws(() => repository.insertReceipt(receipt)); // duplicate receipt_barcode violates its UNIQUE constraint
  assert.equal(repository.listReceipts().length, 1);
  assert.equal(repository.getReceiptById(receipt.eventId).lineItems.length, receipt.lineItems.length);
});

test("rolls back an already-written header when a later line item write fails, leaving no partial receipt", () => {
  const repository = buildRepository();
  const receipt = generateReceipt(request, { sequence: repository.nextSequenceNumber() });
  // The second line item violates POS_RECEIPT_LINE_ITEM's NOT NULL constraint on item_code, forcing a
  // failure after the header row (and the first line item) have already been written in the
  // same transaction - proving the whole receipt is rolled back, not just the line items.
  const receiptWithInvalidLineItem = {
    ...receipt,
    lineItems: [receipt.lineItems[0], { ...receipt.lineItems[0], lineNumber: 2, itemCode: null }],
  };

  assert.throws(() => repository.insertReceipt(receiptWithInvalidLineItem));
  assert.equal(repository.listReceipts().length, 0);
  assert.equal(repository.getReceiptById(receipt.eventId), null);
});
