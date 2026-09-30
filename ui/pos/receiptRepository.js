/**
 * Persists the canonical receipt and the Cardzoids normalized POS tables.
 * The canonical JSON is never rebuilt from the normalized rows: it is stored
 * exactly as generated so callers can retrieve the original payload.
 */
export function createReceiptRepository(db) {
  const existingColumns = db.prepare("PRAGMA table_info(POS_RECEIPT_EVENT)").all();
  const existingSchema = db.prepare(
    "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'POS_RECEIPT_LINE_ITEM'",
  ).get()?.sql ?? "";
  if (
    existingColumns.length > 0
    && (
      !existingColumns.some((column) => column.name === "ReceiptBarcode")
      || !existingSchema.includes("GstCategory IN ('TAXABLE SUPPLY', 'FREE')")
      || !existingColumns.some((column) => column.name === "EventNumber" && column.type === "VARCHAR(20)")
    )
  ) {
    db.exec("DROP TABLE IF EXISTS POS_RECEIPT_LINE_ITEM; DROP TABLE IF EXISTS POS_RECEIPT_EVENT;");
  }

  function membershipLevelCode(level) {
    return { NONE: "0", BRONZE: "1", SILVER: "2", GOLD: "6" }[String(level).toUpperCase()] ?? String(level);
  }
  db.exec(`
    PRAGMA foreign_keys = ON;
    CREATE TABLE IF NOT EXISTS POS_RECEIPT_EVENT (
      ReceiptBarcode VARCHAR(30) PRIMARY KEY,
      EventNumber VARCHAR(20) NOT NULL UNIQUE,
      InvoiceDate datetime NOT NULL,
      StoreNumber VARCHAR(10) NOT NULL,
      TerminalID VARCHAR(10) NOT NULL,
      EFTPOSID VARCHAR(30),
      RefNo VARCHAR(20),
      PaymentMethod VARCHAR(20) NOT NULL DEFAULT 'CASH',
      CustomerNumber VARCHAR(20),
      TotalIncGST DECIMAL(12,2) NOT NULL,
      TotalGST DECIMAL(12,2) NOT NULL,
      TotalExGST DECIMAL(12,2) NOT NULL,
      RefundFlag VARCHAR(10) NOT NULL CHECK (RefundFlag IN ('CREDIT', 'INVOICE')),
      UserID VARCHAR(50) NOT NULL,
      CanonicalJson TEXT NOT NULL
    );
    CREATE TABLE IF NOT EXISTS POS_RECEIPT_LINE_ITEM (
      ReceiptBarcode VARCHAR(30) NOT NULL REFERENCES POS_RECEIPT_EVENT(ReceiptBarcode) ON DELETE CASCADE,
      ItemCode VARCHAR(50) NOT NULL,
      ItemDesc VARCHAR(200) NOT NULL,
      ItemQty INTEGER NOT NULL,
      UnitPrice DECIMAL(12,2) NOT NULL,
      LineAmount DECIMAL(12,2) NOT NULL,
      GstAmount DECIMAL(12,2) NOT NULL,
      GstCategory VARCHAR(20) NOT NULL CHECK (GstCategory IN ('TAXABLE SUPPLY', 'FREE')),
      PaymentGL VARCHAR(50) NOT NULL,
      PricingLevel VARCHAR(10),
      PricingLevel1 VARCHAR(1) NOT NULL CHECK (PricingLevel1 IN ('Y', 'N')),
      LineDiscountType VARCHAR(30),
      LineDiscountAmount DECIMAL(12,2) NOT NULL,
      ItemParentCategory VARCHAR(100) NOT NULL,
      ItemCategory VARCHAR(100) NOT NULL,
      ItemSubCategory VARCHAR(100) NOT NULL,
      MembershipProductGLCode VARCHAR(50) NOT NULL,
      MembershipNumber VARCHAR(20),
      MembershipProductID VARCHAR(50),
      VehicleRegistration VARCHAR(20),
      VehicleVIN VARCHAR(50),
      DispositionCodes VARCHAR(200) NOT NULL
    );
  `);

  const insertHeader = db.prepare(`
    INSERT INTO POS_RECEIPT_EVENT (
      ReceiptBarcode, EventNumber, InvoiceDate, StoreNumber, TerminalID, EFTPOSID, RefNo,
      PaymentMethod, CustomerNumber, TotalIncGST, TotalGST, TotalExGST, RefundFlag, UserID, CanonicalJson
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `);
  const insertLine = db.prepare(`
    INSERT INTO POS_RECEIPT_LINE_ITEM (
      ReceiptBarcode, ItemCode, ItemDesc, ItemQty, UnitPrice, LineAmount, GstAmount, GstCategory,
      PaymentGL, PricingLevel, PricingLevel1, LineDiscountType, LineDiscountAmount,
      ItemParentCategory, ItemCategory, ItemSubCategory, MembershipProductGLCode, MembershipNumber,
      MembershipProductID, VehicleRegistration, VehicleVIN, DispositionCodes
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `);
  const count = db.prepare("SELECT COUNT(*) AS count FROM POS_RECEIPT_EVENT");
  const list = db.prepare(`
    SELECT ReceiptBarcode AS receiptBarcode, EventNumber AS eventNumber, EventNumber AS eventId,
      InvoiceDate AS transactionDate,
      StoreNumber AS storeId, TerminalID AS terminalId, EFTPOSID AS eftposId, RefNo AS refNo,
      PaymentMethod AS paymentType, CustomerNumber AS customerNumber, TotalIncGST AS totalIncGst,
      TotalGST AS totalGst, TotalExGST AS totalExGst, RefundFlag AS refundFlag, UserID AS operatorId,
      (SELECT COUNT(*) FROM POS_RECEIPT_LINE_ITEM line
       WHERE line.ReceiptBarcode = event.ReceiptBarcode) AS lineItemCount
    FROM POS_RECEIPT_EVENT event ORDER BY InvoiceDate DESC
  `);
  const header = db.prepare(`
    SELECT ReceiptBarcode AS receiptBarcode, EventNumber AS eventNumber, EventNumber AS eventId,
      InvoiceDate AS transactionDate,
      StoreNumber AS storeId, TerminalID AS terminalId, EFTPOSID AS eftposId, RefNo AS refNo,
      PaymentMethod AS paymentType, CustomerNumber AS customerNumber, TotalIncGST AS totalIncGst,
      TotalGST AS totalGst, TotalExGST AS totalExGst, RefundFlag AS refundFlag, UserID AS operatorId,
      CanonicalJson AS receiptJson
    FROM POS_RECEIPT_EVENT
    WHERE ReceiptBarcode = ? OR EventNumber = ?
  `);
  const lines = db.prepare(`
    SELECT ItemCode AS itemCode, ItemDesc AS itemDescription, ItemQty AS quantity, UnitPrice AS unitPrice,
      LineAmount AS lineAmount, GstAmount AS gstAmount, GstCategory AS gstCategory, PaymentGL AS paymentGl,
      PricingLevel AS pricingLevel, PricingLevel1 AS pricingLevel1, LineDiscountType AS discountType,
      LineDiscountAmount AS discountAmount, ItemParentCategory AS itemParentCategory,
      ItemCategory AS itemCategory, ItemSubCategory AS itemSubCategory,
      MembershipProductGLCode AS membershipProductGlCode, MembershipNumber AS membershipNumber,
      MembershipProductID AS membershipProductId, VehicleRegistration AS vehicleRegistration,
      VehicleVIN AS vehicleVin, DispositionCodes AS dispositionCodes
    FROM POS_RECEIPT_LINE_ITEM WHERE ReceiptBarcode = ? ORDER BY rowid
  `);

  function nextSequenceNumber() {
    return Number(count.get().count) + 1;
  }

  function terminalId(tillId) {
    const digits = String(tillId).match(/\d+/)?.[0] ?? "0";
    return digits.padStart(3, "0").slice(-3);
  }

  function storeNumber(storeId) {
    return String(storeId).replace(/^VU[-_]?/i, "");
  }

  function eventNumber(eventId) {
    return String(eventId).replaceAll("-", "").slice(0, 20);
  }

  function insertReceipt(receipt) {
    const payments = receipt.payments ?? [];
    const firstPayment = payments[0] ?? {};
    const total = receipt.financials.totalIncGst;
    const paymentMethod = payments.length > 1 ? "SPLIT" : (firstPayment.paymentType ?? "CASH");
    db.exec("BEGIN IMMEDIATE");
    try {
      insertHeader.run(
        receipt.receiptBarcode, eventNumber(receipt.eventId), receipt.transactionDate, storeNumber(receipt.storeId),
        terminalId(receipt.tillId), firstPayment.eftposTerminalId ?? null, firstPayment.eftposRefNo ?? null,
        paymentMethod, receipt.customer.membershipNumber ?? null, total,
        receipt.financials.totalGst, receipt.financials.totalExGst,
        total < 0 ? "CREDIT" : "INVOICE", receipt.operatorId, JSON.stringify(receipt),
      );
      receipt.lineItems.forEach((line, index) => {
        insertLine.run(
          receipt.receiptBarcode, line.itemCode, line.itemDescription, line.quantity, line.unitPrice,
          line.lineAmount, line.gstAmount, line.gstCategory,
          `${terminalId(receipt.tillId)}-${firstPayment.eftposRefNo ?? ""}`,
          receipt.customer.membershipLevel ? membershipLevelCode(receipt.customer.membershipLevel) : null,
          new Set(["6", "7", "8"]).has(membershipLevelCode(receipt.customer.membershipLevel)) ? "Y" : "N",
          line.discount?.discountType ?? null, line.discount?.discountAmount ?? 0,
          line.itemParentCategory, line.itemCategory, line.itemSubCategory, line.membershipProductGlCode,
          receipt.customer.membershipNumber ?? null, line.membershipProductId ?? null,
          receipt.customer.vehicleRegistration ?? null, receipt.customer.vehicleVin ?? null,
          line.dispositionCodes,
        );
      });
      db.exec("COMMIT");
    } catch (error) {
      db.exec("ROLLBACK");
      throw error;
    }
  }

  function listReceipts() {
    return list.all();
  }

  function getReceiptById(receiptBarcode) {
    const row = header.get(receiptBarcode, receiptBarcode) ?? db.prepare(`
      SELECT ReceiptBarcode AS receiptBarcode, EventNumber AS eventNumber, EventNumber AS eventId,
        InvoiceDate AS transactionDate, StoreNumber AS storeId, TerminalID AS terminalId,
        EFTPOSID AS eftposId, RefNo AS refNo, PaymentMethod AS paymentType,
        CustomerNumber AS customerNumber, TotalIncGST AS totalIncGst, TotalGST AS totalGst,
        TotalExGST AS totalExGst, RefundFlag AS refundFlag, UserID AS operatorId,
        CanonicalJson AS receiptJson
      FROM POS_RECEIPT_EVENT WHERE json_extract(CanonicalJson, '$.eventId') = ?
    `).get(receiptBarcode);
    if (!row) return null;
    const receipt = JSON.parse(row.receiptJson);
    return {
      ...row,
      refundFlag: row.refundFlag,
      receiptJson: row.receiptJson,
      receipt,
      lineItems: lines.all(row.receiptBarcode).map((line) => ({
        ...line,
        dispositionCodes: line.dispositionCodes ?? "",
        discount: { discountType: line.discountType, discountAmount: line.discountAmount },
      })),
    };
  }

  return { nextSequenceNumber, insertReceipt, listReceipts, getReceiptById };
}
