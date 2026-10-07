import express from "express";
import { getCatalog } from "./catalog.js";
import { posEventsDb, posReceipts } from "./database.js";
import { logPos } from "./log.js";
import { formatReceiptForPsv } from "./receiptExport.js";
import { generateReceipt, validateGenerateRequest } from "./receiptGenerator.js";

export const posRouter = express.Router();
const router = posRouter;

router.get("/api/pos/catalog", (_request, response) => response.json(getCatalog()));

function generatePosReceipt(request) {
  logPos("receipt.generation_started", {
    storeId: request?.storeId ?? null,
    tillId: request?.tillId ?? null,
    operatorId: request?.operatorId ?? null,
    paymentType: request?.paymentType ?? null,
  });
  const sequence = posReceipts.nextSequenceNumber();
  const receipt = generateReceipt(request, { sequence });
  posReceipts.insertReceipt(receipt);
  logPos("receipt.generation_completed", {
    receiptBarcode: receipt.receiptBarcode,
    lineItemCount: receipt.lineItems.length,
    totalIncGst: receipt.financials.totalIncGst,
  });
  return receipt;
}

function handlePosReceiptGeneration(request, response) {
  const validationError = validateGenerateRequest(request.body);
  if (validationError) {
    logPos("receipt.generation_rejected", { reason: validationError });
    return response.status(400).json({ error: validationError });
  }

  try {
    return response.status(201).json(generatePosReceipt(request.body));
  } catch (error) {
    logPos("receipt.generation_failed", { error: error instanceof Error ? error.message : String(error) });
    console.error("Failed to generate POS receipt", error);
    return response.status(500).json({ error: "Failed to generate POS receipt" });
  }
}

router.post("/api/pos/receipts", handlePosReceiptGeneration);
router.post("/api/pos/generate", handlePosReceiptGeneration);
router.post("/api/pos/process", handlePosReceiptGeneration);

router.get("/api/pos/receipts", (_request, response) => response.json(
  posReceipts.listReceipts().map((receipt) => ({ ...receipt, lineItemCount: Number(receipt.lineItemCount) })),
));

router.get("/api/pos/receipts/:id", (request, response) => {
  const receipt = posReceipts.getReceiptById(request.params.id);
  return receipt ? response.json(receipt) : response.status(404).json({ error: "Receipt not found" });
});

router.get("/api/pos/receipts/:id/export", (request, response) => {
  const persistedReceipt = posReceipts.getReceiptById(request.params.id);
  if (!persistedReceipt) return response.status(404).json({ error: "Receipt not found" });
  return response.json(formatReceiptForPsv(persistedReceipt.receipt));
});

router.get("/api/pos-events", (_request, response) => {
  const events = posEventsDb.prepare(`
    SELECT id, event_type AS eventType, receipt_number AS receiptNumber,
      occurred_at AS occurredAt, status, payload
    FROM PosEvents ORDER BY occurred_at DESC
  `).all().map((event) => ({ ...event, payload: JSON.parse(event.payload) }));
  return response.json(events);
});
