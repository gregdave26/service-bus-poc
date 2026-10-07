# Local POS Processing (Dashboard-only simulator)

> POS Processing tab in the dashboard UI. This is a self-contained local simulation for
> demonstrating catalog-driven, deterministic data generation and normalized persistence; it
> does not publish to the `contact.events` topic and is not part of the Service Bus POC's
> in-scope messaging architecture (see [`PROJECT_BRIEF.md`](../PROJECT_BRIEF.md)).

## Purpose

Lets a developer generate realistic, reproducible point-of-sale receipts from the dashboard,
inspect how a single receipt normalizes into header/line-item rows, and see the exact JSON
that was generated and persisted.

The final process-flow stage simulates conversion of the persisted POS event into the
pipe-delimited PSV shape consumed by D365. The browser can preview and download that simulated
file; it does not execute the production queue, Azure Function, ADF/database procedures, or
Blob Storage delivery.

Completed process-flow stages and connectors use the same green indication as Contact Events
and Rostering. Previously completed POS stages stay highlighted while the export stage runs.

## Design

### Catalog

[`ui/data/pos-catalog.json`](../../ui/data/pos-catalog.json) is the selectable metadata: stores,
tills, operators, payment methods (with their GL code and whether they require EFTPOS
details), membership levels, pricing levels, GST categories and rates, and a product list
(item code/description/unit price/category hierarchy/GST category/pricing level/membership GL
code/disposition codes). `GET /api/pos/catalog` serves it to the UI so the generation form's
dropdowns and product choices always match the server's data.

### Deterministic, seeded generation

[`ui/server/pos/rng.js`](../../ui/server/pos/rng.js) implements Mulberry32, a small seeded PRNG. When a
request does not specify explicit line items or an explicit customer,
[`ui/server/pos/receiptGenerator.js`](../../ui/server/pos/receiptGenerator.js) seeds it from either a
caller-supplied `seed` or a freshly generated one, then uses it to pick how many line items to
include, which catalog products/quantities fill them, and the customer's membership/vehicle
details. Supplying the same seed (and the same store/till/operator/payment type) reproduces an
identical receipt. Callers can also bypass randomization entirely by supplying
`lineItems: [{ itemCode, quantity, discount }]` and/or `customer: { ... }` explicitly. The seed
itself is an internal generation input only - it is not part of the persisted receipt shape.

### Mathematically consistent totals

[`ui/server/pos/money.js`](../../ui/server/pos/money.js) converts amounts to integer cents before any
arithmetic. Each line's `lineAmount = (quantity * unitPrice) - discount.discountAmount`,
`gstAmount = round(lineAmount * gstCategory's rate)`, `financials.totalExGst` and
`financials.totalGst` are the exact sums of the lines' `lineAmount`/`gstAmount`, and
`financials.totalIncGst = totalExGst + totalGst` - all computed in cents and only converted
back to decimal for the final JSON, so the totals always reconcile exactly (no floating-point
drift). The receipt's single `payments` entry is generated for the full `totalIncGst`.

### Atomic, normalized persistence with exact JSON preserved

[`ui/server/pos/receiptRepository.js`](../../ui/server/pos/receiptRepository.js) persists each generated
receipt as one `POS_RECEIPT_EVENT` header row plus one `POS_RECEIPT_LINE_ITEM` row per line,
wrapped in a single `BEGIN IMMEDIATE` / `COMMIT` transaction (rolled back on any failure) so a
receipt is never partially written. The tables use the authoritative Cardzoids names and
capitalization: the header contains `ReceiptBarcode`, `EventNumber`, `InvoiceDate`,
`StoreNumber`, three-digit `TerminalID`, `EFTPOSID`, `RefNo`, `PaymentMethod`,
`CustomerNumber`, the three GST totals, `RefundFlag` (`CREDIT` for negative totals, otherwise
`INVOICE`), and `UserID`. `PaymentMethod` defaults to `CASH` and is `SPLIT` when the canonical
receipt contains multiple tenders. The line table is keyed to the header by `ReceiptBarcode`
and contains the Cardzoids item, GST, GL, pricing, discount, membership, vehicle, and
disposition fields; `GstCategory` is mapped to `TAXABLE SUPPLY` or `FREE`, and `PricingLevel1`
is `Y` only for membership levels 6, 7, or 8. `CanonicalJson` stores the exact canonical JSON,
so `GET /api/pos/receipts/:id` can return the byte-for-byte payload independently of the
normalized query columns.

### Canonical JSON contract

The receipt shape is documented as a JSON Schema at
[`contracts/pos-receipt-v1.schema.json`](../../contracts/pos-receipt-v1.schema.json):
top-level `receiptBarcode`, `eventId`, `storeId`, `tillId`, `operatorId`, `transactionDate`,
`customer` (numeric membership and vehicle details), `financials` (ex/inc-GST totals and an
`INVOICE`/`CREDIT` refund flag), `payments` (payment type, amount, EFTPOS details when
applicable, and GL code), and
`lineItems` (item code/description, quantity, pricing, GST, category hierarchy, discount,
pricing level, comma-separated disposition codes, and membership GL code). Canonical GST
categories are exactly `TAXABLE SUPPLY` or `FREE`; `PricingLevel1` is `Y` only for
membership levels 6, 7, and 8.

## API

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/pos/catalog` | Selectable metadata catalog (stores, tills, operators, payment methods, membership levels, pricing levels, GST categories, products, currency) |
| `POST` | `/api/pos/receipts` | Validates the request, generates a receipt, persists it atomically, and returns the canonical JSON |
| `POST` | `/api/pos/generate` | Backward-compatible alias for receipt generation |
| `POST` | `/api/pos/process` | Backward-compatible alias for receipt generation and persistence |
| `GET` | `/api/pos/receipts` | Lists persisted receipts (header fields only) ordered by most recent |
| `GET` | `/api/pos/receipts/:id` | Returns a persisted receipt's header, line items, and exact canonical JSON |
| `GET` | `/api/pos/receipts/:id/export` | Converts a persisted receipt to SQL-derived PSV and returns filename/content/record metadata |

## UI

The POS Processing tab (`PosProcessing` in [`ui/src/main.jsx`](../../ui/src/main.jsx)) provides:
- A generation form: store/till/operator/payment type dropdowns from the catalog, a toggle
  between random generation (optional item count and seed) and explicit product/quantity
  selection. Customer/membership/vehicle details are always generated automatically unless
  supplied via the API's `customer` field.
- A table of persisted receipts.
- A detail dialog showing the normalized header, payments, and line items alongside the exact
  canonical JSON.
- A Reporting / Finance Export stage that indicates when the PSV file is available, opens an
  inline preview, and downloads the generated `.psv` file to disk for viewing in Excel.

## Local data

The SQLite database lives at `ui/data/pos-events.db` (gitignored; override with
`POS_EVENTS_DB_PATH`). It is created and its tables initialized on server startup.

## PSV export simulation

[`ui/server/pos/receiptExport.js`](../../ui/server/pos/receiptExport.js) mirrors the field order and
transformations from `sp_Publish_POSReceipts` in the supplied SQL reference. It emits one type-1
header record followed by type-2 detail records. The header contains the receipt barcode, event
ID, invoice date, store, terminal, payment method, EFTPOS reconciliation keys, customer debtor,
and totals. Detail rows contain the receipt barcode, line number, product SKU, description,
quantity, unit price, net amount, GST amount, and GL coding. Refunds are emitted as `CREDIT`
with negative totals and line amounts, and values are sanitized so pipes and line breaks cannot
corrupt PSV rows. The generated filename follows
`CARSPOS_AR_INV_001_<yyyyMMddHHmmss>.psv`. Production queue publication, the Azure Function,
ADF procedures, and Blob Storage are outside this dashboard's scope.
