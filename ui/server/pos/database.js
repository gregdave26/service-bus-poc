import { mkdirSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";
import path from "node:path";
import { uiRoot } from "../shared/paths.js";
import { createReceiptRepository } from "./receiptRepository.js";

const posEventsPath = process.env.POS_EVENTS_DB_PATH ?? path.join(uiRoot, "data", "pos-events.db");
mkdirSync(path.dirname(posEventsPath), { recursive: true });
const posEventsDb = new DatabaseSync(posEventsPath);
const posReceipts = createReceiptRepository(posEventsDb);
posEventsDb.exec(`
  CREATE TABLE IF NOT EXISTS PosEvents (
    id TEXT PRIMARY KEY,
    event_type TEXT NOT NULL,
    receipt_number TEXT NOT NULL,
    occurred_at TEXT NOT NULL,
    status TEXT NOT NULL,
    payload TEXT NOT NULL
  )
`);
posEventsDb.prepare(`
  INSERT OR IGNORE INTO PosEvents (id, event_type, receipt_number, occurred_at, status, payload)
  VALUES (?, ?, ?, ?, ?, ?)
`).run(
  "pos-transaction-created-example",
  "POS Transaction Created",
  "POS-10042",
  "2026-09-25T07:00:00.000Z",
  "Persisted locally",
  JSON.stringify({ transactionId: "txn-10042", total: 42.5, currency: "AUD", items: 2 }),
);

export { posEventsDb, posReceipts };
